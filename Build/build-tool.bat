@echo off
setlocal enableextensions enabledelayedexpansion
call :getargc argc %*
if %argc% equ 0 (
  echo no args
  goto :eof
)
endlocal
set projectDir=%1
set projectDir=%projectDir:~1,-1%
set outDir=%2
set outDir=%outDir:~1,-1%
set configurationName=%3
set sourceDir=%projectDir%%outDir%
set targetDir=%~dp0%outDir%
echo %sourceDir%
if not exist "%targetDir%" (
  md "%targetDir%"
)
xcopy "%sourceDir%*" "%targetDir%" /C /Y
if %configurationName%==Release (
	del "%targetDir%*.pdb" /Q
)
goto :eof

:getargc
    set getargc_v0=%1
    set /a "%getargc_v0% = 0"
:getargc_l0
    if not x%2x==xx (
        shift
        set /a "%getargc_v0% = %getargc_v0% + 1"
        goto :getargc_l0
    )
    set getargc_v0=
    goto :eof