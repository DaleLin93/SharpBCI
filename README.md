# SharpBCI

### Introduction

This is a easy-to-use platform to run BCI experiments. 
This platform provides basic functions such as experimental configuration, data streaming, and offline analysis.
Different experiment paradigms and external device drivers can be implemented through plug-in development.

### Repository Organisation

\ 
 + **Library** *Shared libraries*
 + **SharpBCI** *SharpBCI executable*
 + **SharpBCI.Core** *Core of SharpBCI*
 + **SharpBCI.Extensions** *Basic extensions*
 + **SharpBCI.Plugins** *SharpBCI plugins*
    + **SharpBCI.CPT.Plugin** *Conners' Continuous Performance Test pradigm*
    + **SharpBCI.MI.Plugin** *Motor imagery paradigm*
    + **SharpBCI.MRCP.Plugin** *Movement related cortical potential paradigm*
    + **SharpBCI.P300.Plugin** *P300 paradigm*
    + **SharpBCI.Speller.Plugin** *BCI speller paradigm, using DirectX to provide stable flicker rendering*
        + **CCA** *C++ cannonical correlation analysis library*
    + **SharpBCI.VEP.Plugin**  *Multiple visual evoked potential paradigms included, using DirectX to provide stable flicker rendering*
    + **SharpBCI.WebBrowser.Plugin** *BCI web browser paradigm*
    + **SharpBCI.BiosignalSamplers.Plugin** *Bio-signal sampler drivers, e.g. Neuroscan, NeuroElectrics, OpenBCI, ...*
    + **SharpBCI.EyeTrackers.Plugin** *Eye-tracker drivers, e.g. Tobii's eye-tracker*
    + **SharpBCI.VideoSources.Plugin** *Video source drivers, e.g. Web-Cam*
 + **SharpBCI.Tests** *Unit tests of SharpBCI*

### Currently Supported Devices
 + ***Biosignal Sampler***
     + *Generic Oscillator* (Sine wave simulated)
     + *Data File Reader* (Replay stored file)
     + *Neuroscan* (via TCP/IP from SCAN 4.5)
     + *Neuro Electrics* (via TCP/IP from NIC 2)
     + *OpenBCI* (via serial port)
     + *Unnamed Device* (via serial port, made by ZJU)
 + ***Eye tracker***
     + *Cursor Tracker* (PC mouse simulated)
     + *Tobii's Eye-Tracker* (via Tobii gaming SDK)
 + ***Video Source***
     + *Screen Capturer* (PC screenshot)
     + *Web Camera* (via DirectShow)

### Get Started

0. Install Visual Studio 2018 or later, clone [MarukoLib](https://github.com/DaleLin93/MarukoLib) and [SharpBCI](https://github.com/DaleLin93/SharpBCI) into same folder.
1. Open Visual Studio **as administrator** and create a new empty Visual Studio solution.
2. Add all projects in repo [MarukoLib](https://github.com/DaleLin93/MarukoLib) except '*MarukoLib.Dragonfly*' to the created solution.
3. Add '*SharpBCI.Core*', '*SharpBCI.Extensions*', '*SharpBCI*' projects into the solution.
4. (*Optional*) Add some plugin projects into the solution as your wish.
5. Set **SharpBCI** as the startup project.
6. Build and run the solution.

### Data Files

After the completion of a session, you can see some files that have the same filename but different suffixes in the data folder: 
 + **.mrk** *Experiment marker with timestamp with timestamp.* (ASCII; Comma-separated)
 + **.dat** *Biosignal data with timestamp.* (ASCII; Comma-separated)
 + **.gaz** *Eye gaze point data with timestamp.* (ASCII; Comma-separated)
 + **.vfs** *Video frames with timestamp.* (Binary)
 + **.scfg** *Session configuration, can be used to restart the experiment.* (ASCII; Json)
 + **.session** *Session information.* (ASCII; Json)
 + **.result** *Result of the experiment.* (ASCII; Json)
 
### Command-line
 + **.\SharpBCI.exe "xxx.scfg"** *Directly run the predefined experiment.*

### How to Create Your Own Paradigm

You can simply defined your own [Experiment](https://github.com/DaleLin93/SharpBCI/blob/master/SharpBCI.Core/Experiment/Experiment.cs) with required paramters and corresponding [ExperimentFactory](https://github.com/DaleLin93/SharpBCI/blob/master/SharpBCI.Extensions/Experiments/ExperimentFactory.cs).

![Demo Experiment](https://github.com/DaleLin93/SharpBCI/blob/master/SharpBCI.Plugins/SharpBCI.Demo.Plugin/Configuration%20Preview.jpg)

See [Demo Plugin](https://github.com/DaleLin93/SharpBCI/tree/master/SharpBCI.Plugins/SharpBCI.Demo.Plugin) for more detail.



