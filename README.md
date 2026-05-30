# EarTrumpet Taskbar Middle-Click Mute Fork

This repository is a fork of [EarTrumpet](https://github.com/File-New-Project/EarTrumpet) with an additional taskbar mute workflow.

The fork adds a configurable action that lets you middle-click a running app icon on the Windows taskbar to toggle that app's mute state without opening the EarTrumpet flyout.

![EarTrumpet Screenshot](./Graphics/1.gif)

## Download
You can download at [Release page](https://github.com/tatsuya087/EarTrumpet/releases)

## Configuration

After launching the app:

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

## Behavior

When the setting is enabled, the app installs a low-level mouse hook and listens for middle-button clicks on the taskbar.

If the click lands on a running app button, the fork resolves the target app from the taskbar's UI Automation tree and toggles the mute state for the matching EarTrumpet session.

If no app can be resolved, the click is ignored and Windows handles it normally.

## Why This Fork Exists

Upstream EarTrumpet already provides per-app volume and device control from the tray and full mixer UI. This fork adds a faster taskbar-first workflow for muting noisy apps with a single middle click.

## Feature Summary

In addition to the upstream EarTrumpet mixer and device-routing capabilities, this fork includes:

* Visualize audio with multi-channel aware peaking
* Standalone volume mixer
* Move apps between playback devices
* Default playback device management
* Configurable hotkeys
* Modern context menus
* Light/dark mode and accent color support
* Taskbar middle-click mute toggle for running apps

## Supported Operating Systems

* Windows 10 1803 or later
* Windows 11

Windows 11 required additional taskbar detection handling in this fork because the taskbar button UI differs from Windows 10.

## Building

[Compiling EarTrumpet](./COMPILING.md)

The generated binaries are placed under `Build\Debug` or `Build\Release` depending on the selected configuration.

## Documentation

* [Technical Information](./EarTrumpet/README.md)
* [Compiling EarTrumpet](./COMPILING.md)
* [Contributing to EarTrumpet](./CONTRIBUTING.md)
* [Information Collected And Transmitted By EarTrumpet](./PRIVACY.md)
* [Project License](./LICENSE)
* [Change Log](./CHANGELOG.md)

## Upstream Credits

This fork is based on EarTrumpet by:

* David Golden ([@GoldenTao](https://www.twitter.com/GoldenTao))
* Rafael Rivera ([@WithinRafael](https://www.twitter.com/WithinRafael))
* Dave Amenta ([@davux](https://www.twitter.com/davux))
* [Contributors](https://github.com/File-New-Project/EarTrumpet/graphs/contributors)

## Special Thanks

`[Horn](https://thenounproject.com/icon/horn-125731/)` icon by Artjom Korman from [the Noun Project](https://thenounproject.com/)
