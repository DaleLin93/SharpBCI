// ==UserScript==
// @name         Web Browser Assistant Client
// @run-at       document-end
// @namespace    http://tampermonkey.net/
// @version      0.1
// @description  Access the websites!
// @author       maruko
// @match        *://*/*
// @grant        none
// ==/UserScript==

(function() {
  'use strict';

  var serverPort = 12315;
  var debug = false;
  var initialized = false;
  var $jQuery = undefined;

  var maxActiveDistance = 500;
  var confirmationDelay = 600;
  var homePage = 'about:blank';

  var customStyle = undefined;
  var gazePointElement = undefined;
  var glyphStylesheet = undefined;
  var scrollUpButton = undefined, scrollDownButton = undefined;
  var backwardButton = undefined, forwardButton = undefined, homeButton = undefined, availabilitySwitchButton = undefined;
  var keyboardBackdrop = undefined, keyboard = undefined, keyboardText = undefined, capsLockIndicator = undefined;

  var acceptTrial = true;
  var windowFocused = true;
  var systemAvailability = true;
  var capsLock = true;

  function nanAs(value, nanValue) {
    return isNaN(value) ? nanValue : value;
  }

  function isSameValue(a, b, tolerance) {
    return Math.abs(a - b) < tolerance;
  }

  function interval(a, b) {
    return a > b ? {min: b, max: a} : {min: b, max: a};
  }

  function getCenterOfInterval(interval) {
    return (interval.min + interval.max) / 2;
  }

  function getExtentOfInterval(interval) {
    return interval.max - interval.min;
  }

  function isIntervalSame(intervalA, intervalB, tolerance) {
    return isSameValue(intervalA.min, intervalB.min, tolerance)
        && isSameValue(intervalA.max, intervalB.max, tolerance);
  }

  function isIntervalContainsAnother(intervalA, intervalB) {
    return intervalA.min <= intervalB.min && intervalA.max >= intervalB.max;
  }

  function isIntervalsOverlapped(intervalA, intervalB) {
    return !(intervalA.max <= intervalB.min || intervalA.min >= intervalB.max);
  }

  function isIntervalsAdjacent(intervalA, intervalB, tolerance) {
    return isSameValue(intervalA.min, intervalB.max, tolerance)
        || isSameValue(intervalA.max, intervalB.min, tolerance);
  }

  function getManhattanDistanceToInterval(interval, value) {
      if (interval.min > value) {
          return interval.min - value;
      } else if (interval.max < value) {
          return value - interval.max;
      } else {
          return 0;
      }
  }

  function mergeIntervals(intervalA, intervalB) {
    return {
      min: Math.min(intervalA.min, intervalB.min),
      max: Math.max(intervalA.max, intervalB.max)
    };
  }

  function isBoundingBoxIntersected(bbA, bbB) {
    return isIntervalsOverlapped(bbA.xInterval, bbB.xInterval)
        && isIntervalsOverlapped(bbA.yInterval, bbB.yInterval);
  }

  function isBoundingBoxVisible(boundingBox, tolerance) {
    return getExtentOfInterval(boundingBox.xInterval) >= 5
        && getExtentOfInterval(boundingBox.yInterval) >= 5;
  }

  function isBoundingBoxContainsAnother(bbA, bbB) {
    return isIntervalContainsAnother(bbA.xInterval, bbB.xInterval)
        && isIntervalContainsAnother(bbA.yInterval, bbB.yInterval);
  }

  function getManhattanDistanceToBoundingBox(boundingBox, point) {
    return getManhattanDistanceToInterval(boundingBox.xInterval, point.x)
         + getManhattanDistanceToInterval(boundingBox.yInterval, point.y)
  }

  function getBoundingBoxOfElement($el) {
    var zoom = nanAs(parseFloat($el[0].style.zoom), 1);
    $jQuery.each($el.parents(), function(i, el, array) {
      if (el.tagName.toLowerCase() === 'html') {
        return;
      }
      zoom *= nanAs(parseFloat($jQuery(el).css('zoom')), 1);
    });
    if (isNaN(zoom)) {
      zoom = 1;
    }
    var offset = $el.offset();
    return {
      xInterval: {
        min: offset.left,
        max: offset.left + $el.outerWidth() * zoom
      },
      yInterval: {
        min: offset.top,
        max: offset.top + $el.outerHeight() * zoom
      }
    };
  }

  function mergeBoundingBoxes(bbA, bbB) {
    return {
      xInterval: mergeIntervals(bbA.xInterval, bbB.xInterval),
      yInterval: mergeIntervals(bbA.yInterval, bbB.yInterval)
    }
  }

  function canMergeBoundingBoxeses(bbA, bbB, tolerance) {
    if (isBoundingBoxContainsAnother(bbA, bbB)
     || isBoundingBoxContainsAnother(bbB, bbA)) {
       return true;
    } else if (isIntervalSame(bbA.xInterval, bbB.xInterval, tolerance)) {
       return isIntervalsAdjacent(bbA.yInterval, bbB.yInterval, tolerance);
    } else if (isIntervalSame(bbA.yInterval, bbB.yInterval, tolerance)) {
       return isIntervalsAdjacent(bbA.xInterval, bbB.xInterval, tolerance);
    } else {
       return false;
    }
  }

  function repeat(func, count, intervalMs) {
    func();
    if (count <= 1) return;
    setTimeout(function() {
      repeat(func, count - 1, intervalMs);
    }, intervalMs);
  }

  function isSystemAvailable() {
    return $jQuery && initialized;
  }

  function canStartTrial() {
    return acceptTrial && windowFocused;
  }

  function getGlyphClassName(name) {
    return 'glyphicon glyphicon-' + name;
  }

  function createGlyphElement(name) {
    var el = document.createElement('span');
    el.className = getGlyphClassName(name);
    return el;
  }

  function addGlyphStylesheet() {
     if (glyphStylesheet) return;
     glyphStylesheet = document.createElement("link");
     glyphStylesheet.setAttribute('rel', "stylesheet");
     glyphStylesheet.setAttribute('type', "text/css");
     glyphStylesheet.setAttribute('href', "http://localhost:" + serverPort + "/static/bootstrap-glyphicon/css/bootstrap-glyphicon.css");
     document.head.appendChild(glyphStylesheet);
  }

  function addFunctionButtons() {
    if (!scrollUpButton) {
      scrollUpButton = document.createElement("fbtn");
      scrollUpButton.className = 'scroller flexcenter';
      scrollUpButton.style.left = "50px";
      scrollUpButton.style.top = "50px";
      scrollUpButton.innerHTML = "\u25b2";
      scrollUpButton.addEventListener('click', function() {
        var scroll = -window.innerHeight * 0.06;
        repeat(function() {
          window.scrollBy(0, scroll);
        }, 10, 50)
      }, false);
      document.body.appendChild(scrollUpButton);
    }

    if (!scrollDownButton) {
      scrollDownButton = document.createElement("fbtn");
      scrollDownButton.className = 'scroller flexcenter';
      scrollDownButton.style.left = "50px";
      scrollDownButton.style.bottom = "50px";
      scrollDownButton.innerHTML = "\u25bc";
      scrollDownButton.addEventListener('click', function() {
        var scroll = +window.innerHeight * 0.06;
        repeat(function() {
          window.scrollBy(0, scroll);
        }, 10, 50)
      }, false);
      document.body.appendChild(scrollDownButton);
    }

    updateScrollerVisibility();

    if (!backwardButton) {
      backwardButton = document.createElement("fbtn");
      backwardButton.className = 'scroller flexcenter';
      backwardButton.style.left = "50px";
      backwardButton.style.top = "calc(50% - 20px)";
      backwardButton.innerHTML = "\u25c0";
      backwardButton.addEventListener('click', function() {
        window.history.back();
      }, false);
      document.body.appendChild(backwardButton);
    }

    if (!forwardButton) {
      forwardButton = document.createElement("fbtn");
      forwardButton.className = 'scroller flexcenter';
      forwardButton.style.right = "50px";
      forwardButton.style.top = "calc(50% - 20px)";
      forwardButton.innerHTML = "\u25b6";
      forwardButton.addEventListener('click', function() {
        window.history.forward();
      }, false);
      document.body.appendChild(forwardButton);
    }

    updateNavigatorVisibility();

    if (!homeButton) {
      homeButton = document.createElement("fbtn");
      homeButton.className = 'scroller flexcenter';
      homeButton.style.left = "50px";
      homeButton.style.top = "calc(25% - 20px)";
      homeButton.appendChild(createGlyphElement('home'));
      homeButton.addEventListener('click', function() {
        window.location = homePage;
      }, false);
      document.body.appendChild(homeButton);
    }

    if (!availabilitySwitchButton) {
      availabilitySwitchButton = document.createElement("fbtn");
      availabilitySwitchButton.className = 'scroller flexcenter';
      availabilitySwitchButton.style.left = "50px";
      availabilitySwitchButton.style.top = "calc(75% - 20px)";
      availabilitySwitchButton.appendChild(createGlyphElement('ban-circle'));
      availabilitySwitchButton.addEventListener('click', function() {
        switchAvailability();
      }, false);
      document.body.appendChild(availabilitySwitchButton);
    }

    updateAvailabilitySwitchButtonStyle();
  }

  function addKeyboard() {
    if (keyboardBackdrop) return;
    var keypressFunc = function (e) {
      var el = e.currentTarget;
      var type = el.getAttribute('data-key-type');
      var text = el.innerText;
			if (type === 'cmd') {
				switch (text) {
					case 'Backspace':
						if (keyboardText.innerHTML.length > 0) {
							keyboardText.innerHTML = keyboardText.innerHTML.substring(0, keyboardText.innerHTML.length - 1);
						}
						break;
					case 'Cancel':
            closeKeyboard();
						break;
					case 'Confirm':
            confirmInputAndCloseKeyboard();
						break;
					case 'Caps Lock':
            switchCapsLock();
						break;
				}
			} else {
				keyboardText.innerHTML += text;
			}
		};
    keyboardBackdrop = document.createElement("div");
    keyboardBackdrop.className = 'keyboard-backdrop';

    keyboard = document.createElement("keyboard");
    var keyboardTextBox = document.createElement("div");
    keyboardTextBox.className = "keyboard-textbox";
    var keyboardTextBoxContent = document.createElement("div");
    keyboardTextBoxContent.className = "keyboard-textbox-content";

    var keyboardTextBoxText = keyboardText = document.createElement("span");
    keyboardTextBoxText.className = "keyboard-textbox-text";
    keyboardTextBoxContent.appendChild(keyboardTextBoxText);

    var keyboardTextBoxCurosr = document.createElement("span");
    keyboardTextBoxCurosr.className = "keyboard-textbox-cursor";
    keyboardTextBoxCurosr.innerHTML = '|';
    keyboardTextBoxContent.appendChild(keyboardTextBoxCurosr);

    keyboardTextBox.appendChild(keyboardTextBoxContent);
    keyboard.appendChild(keyboardTextBox);

    /* Char keys */
    var keyboardKeys = document.createElement("div");
    keyboardKeys.className = "keyboard-keys";

    for (var i = 0; i < 10; i++) {
      var key = document.createElement("key");
      key.innerHTML = String.fromCharCode(i + "0".charCodeAt(0));
      key.setAttribute('data-key-type', 'char');
      key.addEventListener('click', keypressFunc);
      keyboardKeys.appendChild(key);
    }

    for (var i = 0; i < 26; i++) {
      var key = document.createElement("key");
      key.innerHTML = String.fromCharCode(i + "A".charCodeAt(0));
      key.setAttribute('data-key-type', 'char');
      key.setAttribute('data-char-upper', key.innerHTML.toUpperCase());
      key.setAttribute('data-char-lower', key.innerHTML.toLowerCase());
      key.addEventListener('click', keypressFunc);
      keyboardKeys.appendChild(key);
    }

    var symbols = " !?#@,.:;$%*^~=+-_()<>{}[]'\"\\/|";
    for (var i = 0; i < symbols.length; i++) {
      var key = document.createElement("key");
      var charater = symbols.substring(i, i + 1);
      if (charater === ' ') charater = '&nbsp;';
      key.innerHTML = charater;
      key.setAttribute('data-key-type', 'char');
      key.addEventListener('click', keypressFunc);
      keyboardKeys.appendChild(key);
    }
    keyboard.appendChild(keyboardKeys);

    /* Command keys */
    var keyboardCommands = document.createElement("div");
    keyboardCommands.className = 'keyboard-commands';
    var commandKeys = ["Cancel", "Caps Lock", "Backspace", "Confirm"];
    for (var i = 0; i < commandKeys.length; i++) {
      var key = document.createElement("key");
      key.className = "command-key";
      key.innerHTML = commandKeys[i];
      key.setAttribute('data-key-type', 'cmd');
      key.addEventListener('click', keypressFunc);
      if (commandKeys[i] === 'Caps Lock') {
        capsLockIndicator = document.createElement("div");
        capsLockIndicator.className = "caps-lock-indicator";
        key.appendChild(capsLockIndicator);
      }
      keyboardCommands.appendChild(key);
    }
    keyboard.appendChild(keyboardCommands);

    keyboardBackdrop.appendChild(keyboard);
    document.body.appendChild(keyboardBackdrop);

    updateCharCase();
  }

  function openKeyboard(inputEl) {
    if (!inputEl) return;
    if (!isSystemAvailable()) return;
    window.keyboardFocusElement = inputEl;
    keyboardText.innerHTML = inputEl.value;
    keyboardBackdrop.style.display = 'block';
    keyboard.style.top = (window.innerHeight - $jQuery(keyboard).outerHeight()) / 2 + "px";
  }

  function isKeyboardVisible() {
    if (!isSystemAvailable()) return false;
    return keyboardBackdrop.style.display === 'block';
  }

  function confirmInputAndCloseKeyboard() {
    if (!isSystemAvailable()) return;
    var keyboardFocus = window.keyboardFocusElement;
    if (keyboardFocus) keyboardFocus.value = $jQuery(keyboardText).text();
    closeKeyboard();
  }

  function closeKeyboard() {
    if (!isSystemAvailable()) return;
    window.keyboardFocusElement = null;
    keyboardBackdrop.style.display = 'none';
  }

  function switchCapsLock() {
    capsLock = !capsLock;
    updateCharCase();
  }

  function updateCharCase() {
    $jQuery.each($jQuery('key[data-key-type="char"]'), function(i, el, array) {
        var upper = el.getAttribute('data-char-upper'),
            lower = el.getAttribute('data-char-lower');
        if (!upper || !lower) return;
        el.innerHTML = capsLock ? upper : lower;
    });
    capsLockIndicator.style.display = capsLock ? 'block' : 'none';
  }

  function updateScrollerVisibility() {
    if (scrollUpButton) scrollUpButton.className = window.scrollY <= 0 ? 'display-none' : 'scroller flexcenter';
    if (scrollDownButton) scrollDownButton.className = window.scrollY + window.innerHeight >= $jQuery(document).outerHeight() ? 'display-none' : 'scroller flexcenter';
  }

  function updateNavigatorVisibility() {
    // cannot do that, see https://stackoverflow.com/questions/3588315/how-to-check-if-the-user-can-go-back-in-browser-history-or-not?answertab=active#tab-top.
  }

  function switchAvailability() {
    setAvailability(!systemAvailability);
  }

  function setAvailability(availability) {
    systemAvailability = availability;
    updateAvailabilitySwitchButtonStyle();
  }

  function updateAvailabilitySwitchButtonStyle() {
    availabilitySwitchButton.children[0].className = getGlyphClassName(systemAvailability ? 'ban-circle' : 'ok-circle');
  }

  function initializeStimulation(visualSchemes, stimulationSize) {
      if (!visualSchemes || visualSchemes.length === 0) return;
      window.schemeCount = visualSchemes.length;
      if (!customStyle) {
        customStyle = document.createElement("style");
        document.head.appendChild(customStyle);
      }

      if (!initialized) {
        addGlyphStylesheet();
        addFunctionButtons();
        addKeyboard();
      }

      if (debug) {
        if (!gazePointElement) {
          document.body.appendChild(gazePointElement = document.createElement("gazepoint"));
        }
      } else if (gazePointElement) {
        gazePointElement.remove();
      }

      var stylesheetContent = "gazepoint { position: absolute; box-sizing: border-box; width: 10px; height: 10px; margin-left: -5px; margin-top: -5px; border: solid 1px red; border-radius: 50%; z-index: 99996; } ";
      stylesheetContent += ".flexcenter { display: flex !important; align-items: center; justify-content: center; } ";
      stylesheetContent += ".scroller { display: block; position: fixed; width: 40px; height: 40px; font-size: 25px; background: #ffffff55; border: 1px solid black; border-radius: 5px; z-index: 99990; } ";
      stylesheetContent += "indicator { position: absolute; box-sizing: border-box; border-radius: 3px; z-index: 99998; pointer-events: none; } ";
      stylesheetContent += "fixation { width: 3px; height: 3px; border-radius: 1.5px; } ";
      stylesheetContent += "flicker { position: absolute; box-sizing: border-box; width: " + stimulationSize.x + "px; height: " + stimulationSize.y + "px; border-radius: 8px; background: black; margin-left: -"
           + (stimulationSize.x / 2) + "px; margin-top: -1px; z-index: 99999; pointer-events: none; }";

      for (var f = 0; f < visualSchemes.length; f++) {
        var visualScheme = visualSchemes[f];
        var styleBorder = visualScheme.borderThickness + " " + visualScheme.color + " " + visualScheme.borderStyle;
        stylesheetContent += " .indicator_" + f + " { border: " + styleBorder + "; } ";
        stylesheetContent += " .flicker_" + f + " { animation: blinker " + (1 / visualScheme.frequency) + "s linear infinite; border: " + styleBorder + "; } ";
        stylesheetContent += " .fixation_" + f + " { background: " + visualScheme.color + "; } ";
      }

  		stylesheetContent += " keyboard { position: fixed; display: flex; box-sizing: border-box; width: 1300px; left: calc((100% - 1300px) / 2); flex-direction: column; align-items: center; background: black; z-index: 99995; border-radius: 15px; border: 1px solid black; padding-bottom: 15px;	}";
  		stylesheetContent += " key { box-sizing: border-box; display: inline-block; width: 68px; height: 65px; border-radius: 5px; border: 1px solid white; background: black; font-size: 30px; color: white; display: inline-flex; align-items: center; justify-content: center; margin: 10px; }";
  		stylesheetContent += " .keyboard-backdrop { display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: #FFFFFFCC; z-index: 99994; }";
  		stylesheetContent += " .keyboard-textbox { margin-top: 0.5px; height: 70px; width: 100%; background: white; text-align: center; padding-top: 15px; padding-bottom: 12px; border-top-left-radius: 14px; border-top-right-radius: 14px; }";
  		stylesheetContent += " .keyboard-textbox-content { font-size: 50px; padding-left: 30px; padding-right: 30px; }";
      stylesheetContent += " .keyboard-textbox-text { width: 100%; background: white; border-top-left-radius: 15px; border-top-right-radius: 15px; }";
  		stylesheetContent += " .keyboard-textbox-cursor { color: gray; margin-left: 1px; animation: cursor 1.5s linear infinite; }";
  		stylesheetContent += " .keyboard-keys { margin: 20px; display: flex; align-items: center; justify-content: center; flex-wrap: wrap; }";
      stylesheetContent += " .keyboard-commands { width: calc(100% - 40px); display: flex; justify-content: space-around; flex-direction: row; }"
      stylesheetContent += " .command-key { width: 150px; font-size: 20px; }";
      stylesheetContent += " .caps-lock-indicator { position: absolute; margin-top: -15px; margin-left: -60px; background: green; width: 6px; height: 6px; border-radius: 50%; pointer-events: none; }"
      stylesheetContent += " .display-none { display: none !important; }"

   		stylesheetContent += " @keyframes cursor { 50% { color: transparent; } }";
      stylesheetContent += " @keyframes blinker { 50% { background: white; } }";
      customStyle.innerHTML = stylesheetContent;

      initialized = true;
    }

    function removeFlickers() {
      $jQuery('flicker').remove();
    }

    function resetTrial() {
      $jQuery('indicator,flicker').remove();
      for (var f = 0; f < window.schemeCount; f++) {
        $jQuery('.flicker_' + f).removeClass('flicker_' + f);
        $jQuery('.indicator_' + f).removeClass('indicator_' + f);
      }
      window.targets = [];
    }

    function startTrial() {
      if (!canStartTrial()) return;
      if (!isSystemAvailable()) return;
      resetTrial();
      var gazePoint0 = window.gazePoint;
      var targets0 = []; // [[$element, position, distance], ...]
      if (gazePoint0) {
        var pageBoundingBox = {
          xInterval: {
            min: window.scrollX,
            max: window.scrollX + window.innerWidth
          },
          yInterval: {
            min: window.scrollY,
            max: window.scrollY + window.innerHeight
          }
        };

        /* Search for elements */
        var links = {};
        var primaryFilterResult = isKeyboardVisible() ? $jQuery('key', keyboard) : (systemAvailability ? $jQuery('a,input,fbtn') : $jQuery('fbtn'));
        $jQuery.each(primaryFilterResult.filter(':visible').filter(':reallyvisible'), function(i, el, array) {
            var tagName = el.tagName.toLowerCase();
            var $el = $jQuery(el);
            var boundingBox = getBoundingBoxOfElement($el);
            if (!isBoundingBoxIntersected(pageBoundingBox, boundingBox)) return;
            if (tagName === 'a' && $el.css('display') === 'inline' && el.childElementCount > 0) {
              $jQuery.each($el.children().filter(':visible').filter(':reallyvisible'), function(i0, el0, array0) {
                var bbOfEl = getBoundingBoxOfElement($jQuery(el0));
                if (!isBoundingBoxVisible(bbOfEl, 5)) {
                  return;
                }
                boundingBox = mergeBoundingBoxes(boundingBox, bbOfEl);
              });
            }
            if (!isBoundingBoxVisible(boundingBox, 5)) {
               return;
            }
            var targetObj = undefined;
            if (tagName === 'a' && el.href && links[el.href.toString()]) {
              targetObj = links[el.href.toString()];
              if (canMergeBoundingBoxeses(targetObj.boundingBox, boundingBox, 8)) {
                targetObj.boundingBox = mergeBoundingBoxes(targetObj.boundingBox, boundingBox);
                targetObj.distance = getManhattanDistanceToBoundingBox(targetObj.boundingBox, gazePoint0);
                return;
              }
            }
            targetObj = {
              element: $el,
              boundingBox: boundingBox,
              distance: getManhattanDistanceToBoundingBox(boundingBox, gazePoint0)
            };
            if (tagName === 'a' && el.href) {
              links[el.href.toString()] = targetObj;
            }
            targets0.push(targetObj);
            // if (!(distance < maxActiveDistance * 2)) return;
        });
        links = undefined;

        /* Filter by distance */
        var targets1 = [];
        for (var i = 0; i < targets0.length; i++) {
          var target = targets0[i];
          if (target.distance > maxActiveDistance) continue;
          targets1.push(target);
        }
        targets0 = undefined;
        targets1.sort(function(a, b) {
          return a.distance - b.distance;
        });

        /* Setup flickers */
        var activedTargets = [];
        for (var i = 0; i < targets1.length && i < window.schemeCount; i++) {
          var target = targets1[i];
          if (debug) console.log(target.element[0]);

          if (target.element[0].tagName.toLocaleLowerCase() === 'key') {
            target.element.addClass('flicker_' + i);
          } else {
            var indicator = target.indicator = document.createElement("indicator");
            indicator.className = 'indicator_' + i;
            indicator.style.left = (target.boundingBox.xInterval.min - 5) + "px";
            indicator.style.top = (target.boundingBox.yInterval.min - 4) + "px";
            indicator.style.width = (getExtentOfInterval(target.boundingBox.xInterval) + 10) + "px";
            indicator.style.height = (getExtentOfInterval(target.boundingBox.yInterval) + 12) + "px";
            document.body.appendChild(indicator);

            var flicker = target.flicker = document.createElement("flicker");
            flicker.className = 'flexcenter flicker_' + i;
            flicker.style.left = getCenterOfInterval(target.boundingBox.xInterval) + "px";
            flicker.style.top = target.boundingBox.yInterval.max + "px";
            document.body.appendChild(flicker);

            var fixation = document.createElement("fixation");
            fixation.className = 'fixation_' + i;
            flicker.appendChild(fixation);
          }

          activedTargets.push(target);
        }
      }
      window.targets = activedTargets;
    }

    function setGazePoint(gp) {
      if (gp) {
        gp = {
          x: gp.x + window.scrollX - window.screenX,
          y: gp.y + window.scrollY - window.screenY
        };
      }
      window.gazePoint = gp;
      if (gazePointElement) {
        if (gp) {
          gazePointElement.style.visibility = "visible";
          gazePointElement.style.left = gp.x + "px";
          gazePointElement.style.top = gp.y + "px";
        } else {
          gazePointElement.style.visibility = "hidden";
        }
      }
    }

    function onFrequencyIdentified(frequencyIndex) {
      if (!isSystemAvailable()) return;
      if (!window.targets || frequencyIndex < 0 || frequencyIndex >= window.targets.length) {
        resetTrial();
        return;
      }
      for (var i = 0; i < window.targets.length; i++) {
        var target = window.targets[i];
        if (target.element[0].tagName.toLocaleLowerCase() !== 'key') {
          if (i == frequencyIndex) {
            continue;
          }
          target.indicator.remove();
        } else {
          for (var f = 0; f < window.schemeCount; f++) {
            target.element.removeClass('flicker_' + f);
          }
          if (i == frequencyIndex) {
            target.element.addClass('indicator_' + i);
          }
        }
      }
      acceptTrial = false;
      var target = window.targets[frequencyIndex];
      setTimeout(function () {
        var el = target.element[0];
        switch (el.tagName.toLocaleLowerCase()) {
          case 'a':
            el.setAttribute('target', '_self');
            el.click();
            break;
          case 'input':
            var inputType = el.getAttribute('type');
            switch (inputType) {
              case 'button': // button
              case 'submit':
                el.click();
                break;
              default: // input box
                openKeyboard(el);
                break;
            }
            break;
          default:
            el.click();
            break;
        }
        var restartDelay = 3000;
        switch (el.tagName.toLocaleLowerCase()) {
          case 'key':
            restartDelay = 100;
            break;
          case 'input':
            var inputType = el.getAttribute('type');
            if (inputType !== 'button' && inputType !== 'submit') restartDelay = 100;
            break;
        }
        resetTrial();
        setTimeout(function() {
          acceptTrial = true;
        }, restartDelay);
      }, confirmationDelay);
    }

    function onWindowFocusChanged(focused) {
      windowFocused = focused;
      if (window.clientSocket) {
        window.clientSocket.send(JSON.stringify({type: "Focus", focused: focused}));
      }
      if (debug) {
        console.log('Window Focus Changed: ' + focused);
      }
    }

    function onWindowViewportChanged() {
      resetTrial();
      updateScrollerVisibility();
    }

    function handleIncomingMessage(message) {
        var messageType = message.type;
        if (!messageType) return;
        switch (messageType) {
            case 'Handshake':
                debug = message.debug;
                maxActiveDistance = message.maxActiveDistance;
                confirmationDelay = message.confirmationDelay;
                homePage = message.homePage;
                initializeStimulation(message.visualSchemes, message.stimulationSize);
                break;
            case 'StartTrial':
                setGazePoint(message.gazePoint);
                startTrial();
                break;
            case 'EndTrial':
                removeFlickers();
                break;
            case 'Frequency':
                onFrequencyIdentified(message.frequencyIndex);
                break;
            default:
                console.log("Unknown message type: " + messageType);
                break;
        }
    }

    function startSystem() {
      if (!Date.now) {
        Date.now = function() { return new Date().getTime(); }
      }
      if (!window.htmlentities) {
      	window.htmlentities = {
      		/**
      		 * Converts a string to its html characters completely.
      		 * @param {String} str String with unescaped HTML characters
      		 **/
      		encode : function(str) {
      			var buf = [];

      			for (var i=str.length-1;i>=0;i--) {
      				buf.unshift(['&#', str[i].charCodeAt(), ';'].join(''));
      			}

      			return buf.join('');
      		},
      		/**
      		 * Converts an html characterSet into its original character.
      		 * @param {String} str htmlSet entities
      		 **/
      		decode : function(str) {
      			return str.replace(/&#(\d+);/g, function(match, dec) {
      				return String.fromCharCode(dec);
      			});
      		}
      	};
      }
      var afterJQuery = function() {
        $jQuery.extend(
          $jQuery.expr[ ":" ],
          {
            reallyvisible : function (a) {
              var $a = $jQuery(a);
              if ($a.parents().not(":visible").length > 0) {
                return false;
              }
              return !($a.css('visibility') === 'hidden' || $a.css('opacity') == 0);
            }
          }
        );
        var $window = $jQuery(window);
        $window.on('resize', onWindowViewportChanged);
        $window.on('scroll', onWindowViewportChanged);
        $window.focus(function() {
          onWindowFocusChanged(true);
        });
        $window.blur(function() {
          onWindowFocusChanged(false);
          resetTrial();
        });
        observeChanges();
        onWindowViewportChanged();
      }

      function observeChanges() {
        if (!MutationObserver) return;
        var observer = new MutationObserver(function( mutations ) {
          updateScrollerVisibility();
        });
        var config = {
            attributes: true,
            childList: true,
            characterData: true
        };
        observer.observe(document.body, config);
        observer.disconnect();
      }

      function connectServer(onMessageReceived, reconnectDelay) {
        var websocket = window.clientSocket = new WebSocket("ws://localhost:" + serverPort + "/?priority=0");
        websocket.onopen = function(evt) {
          console.log("Web browser assistant server connected!");
          websocket.send('{"Type":"Handshake"}');
        };
        websocket.onclose = function(evt) {
          console.log("Web browser assistant server disconnected!");
          window.clientSocket = undefined;
          setTimeout(function () {
            connectServer(onMessageReceived, reconnectDelay);
          }, reconnectDelay);
        };
        websocket.onerror = function(evt) {
          console.log(evt);
        };
        websocket.onmessage = function(evt) {
          if (debug) console.log("Message Received: " + evt.data);
          var message = JSON.parse(evt.data);
          onMessageReceived(message);
        };
      }

      if (!window.jQuery) {
        var jQueryScript = document.createElement("script");
        jQueryScript.src = "http://localhost:" + serverPort + "/static/jquery-3.3.1.min.js?t=" + Date.now();
        jQueryScript.type = 'text/javascript';
        jQueryScript.onload = function () {
          $jQuery = window.jQuery;
          afterJQuery();
        };
        document.head.appendChild(jQueryScript);
      } else {
        $jQuery = window.jQuery;
        afterJQuery();
      }
      connectServer(handleIncomingMessage, 5000);
    }

    if (window.top == window.self)
    {
      startSystem();
    }

})();
