# Introduction

<img src=http://www.gavpugh.com/boxthumbs/box_xnacpc.jpg align="right">

Development on XNACPC was borne out of an interest to test the constraints of C# and XNA. I was curious as to how much CPU power you could get out of the Xbox 360 platform with C#, since in some other experiments I was worried it was a little limited. I also wanted to see how well you could structure an emulator with the language features of C#. To make the source code easy to understand and navigate, which is something you can't say about most C++ based emulators.

An Amstrad emulator is something I've toyed with since 1998, when I worked on a similarly experimental project. I managed to get it to a working state back then, and included it in my portfolio when I was attempting to get into the games industry. It's still available for download here: http://www.angelfire.com/retro2/cpc3d/

When working on XNACPC I started from scratch again. I used my old source code as a reference, as well as various Amstrad technical documentation. I made a point of not looking at any existing 'fully-featured' CPC emulators source code. I didn't want to take the fun and experimentation out of the project.

XNACPC as it stands now, is comparable to some of the late 90's CPC emulators. It's got a very good rate of compatibility with games I've tried on it; probably around 9 out of 10 worked perfectly for me. I've played through many games with it now, it makes a really fun platform to play CPC games on an Xbox 360.

It's by no means striving to be a competitor to the raft of full-featured PC- based emulators around now. It's just a humble toy project that I wanted to share. Unfortunately due to the obvious legal issues around distributing games I don't have the rights for, I can't upload it to the Xbox Live Indie Games service. So you'll need the XNA Creators Club subscription to be able to use the emulator on your Xbox 360.


# Blog Posts

A couple of blog posts I've made about development of the emulator:
* http://www.gavpugh.com/2010/05/21/xnacpc-an-amstrad-cpc-emulator-for-the-xbox-360/
* http://www.gavpugh.com/2011/11/11/xnacpc-xbox-360-amstrad-cpc-emulator-released/


# XNACPC In Action

A demonstration video showing a number of games running on Xbox 360:
[![XNACPC In Action](http://img.youtube.com/vi/TfB2V1DmFs0/0.jpg)](http://www.youtube.com/watch?v=TfB2V1DmFs0)


# Installation

There are two options to get started and play with XNACPC:

1) Download the PC binary package from here:
http://www.gavpugh.com/xnacpc/xnacpc_1_0_pc_binaries.zip

2) Download the ready-to-play source package from here:
http://www.gavpugh.com/xnacpc/xnacpc_1_0_source.zip

Both include the Amstrad ROMs (which are free to distribute), and a small selection of playable games.

You will need to compile and deploy the source to your Xbox 360 yourself, using Microsoft Visual Studio. You will also need the XNA 'Creators Club' ('App Hub'?) subscription to be able to deploy the emulator to your Xbox.


# Prereqs

If just running the PC binaries, you will need these two redistributables:

Microsoft XNA Framework Redistributable 4.0
* http://www.microsoft.com/download/en/details.aspx?id=20914

Microsoft .NET Framework 4
* http://www.microsoft.com/download/en/details.aspx?id=17718
 
If you wish to compile the source yourself, or deploy to Xbox 360, you will need this:

Microsoft XNA Game Studio 4.0
* http://www.microsoft.com/download/en/details.aspx?id=23714


# Usage

XNACPC works with any Microsoft Xbox 360 game pad, on both PC and Xbox. The 'Start'
button will bring up the pause menu. Which allows you go change settings of the
emulator, and start up games.

The keyboard also functions on both platforms, as the CPC keyboard.

On an attached keyboard you can press the 'F1' key to get to the pause menu. It's
very useful if you don't have a control pad attached.

The keyboard also offers joystick controls on the NumPad. The center button '5' 
is the fire button. Num Lock must be turned 'ON' for the keyboard-joystick to work.


# Adding New Games

Games can only be loaded currently via .SNA snapshot files.

I recommend using the WinAPE emulator to produce these: http://www.winape.net/

Save in the v3 format, uncompressed. XNACPC supports a 128kb CPC. You will have
problems with 'multi-load' games though. Ones which access the floppy drive or
tape to load up additional levels of the game.

The .SNA files should simply be dragged into the 'Content' portion of the XNACPC
solution. Just put them in the same directory as the existing sample SNA files.
Their presence in the project will automatically add them to the pause menu list.


# Bugs / Missing Features

 * The floppy disc drive isn't emulated
 * The tape deck isn't emulated
 * The only way to get games into the emulator is via the .SNA snapshot format.
 * XNACPC supports v3 snapshots, but not the compressed variant.
 * Amstrad Plus features aren't emulated.
 * The CRTC emulation may have trouble with some 'exotic' software.


# Version History

v1.0 - November 2011 - Initial Release
