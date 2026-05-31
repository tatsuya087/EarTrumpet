# EarTrumpet Taskbar Middle-Click Mute Fork

This repository is a fork of [EarTrumpet](https://github.com/File-New-Project/EarTrumpet) with an additional taskbar mute workflow.

The fork adds a configurable action that lets you middle-click a running app icon on the Windows taskbar to toggle that app's mute state without opening the EarTrumpet flyout.

![EarTrumpet Screenshot](./Graphics/1.gif)

## Download
You can download at [Release page](https://github.com/tatsuya087/EarTrumpet/releases)

## Configuration

Launch `EarTrumpet.exe`

1. Open `Settings`
2. Open `Mouse`
3. Enable `Middle-click on a taskbar app icon to toggle mute`

The option is off by default to avoid changing standard taskbar behavior unless explicitly enabled.

## What This Fork Adds

* Middle-click any running app icon on the taskbar to mute or unmute that app
* New setting: `Middle-click on a taskbar app icon to toggle mute`
* Setting is disabled by default
* Default taskbar "open new instance" behavior is suppressed when the mute action is handled
* Windows 11 support for taskbar app detection via UI Automation fallback search

## Supported Operating Systems

* Windows 10 1803 or later
* Windows 11

Windows 11 required additional taskbar detection handling in this fork because the taskbar button UI differs from Windows 10.

## Building

[Compiling EarTrumpet](./COMPILING.md)

The generated binaries are placed under `Build\Debug` or `Build\Release` depending on the selected configuration.
