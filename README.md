# ColorBlockClicker

ColorBlockClicker is a lightweight Windows utility that detects and automatically clicks the one differently colored tile in a grid. It continuously adapts as the grid grows, without requiring a fixed tile count.

## Features

- Detects the grid size automatically, starting with a 2 x 2 grid.
- Identifies the differently colored tile and performs rapid mouse clicks.
- Captures only the area you select on screen.
- Start and stop controls, plus an `F8` global toggle hotkey.
- Adjustable maximum grid size (up to 60 x 60).
- Optional high-speed scanning mode.
- Always-on-top dark floating window with collapse and expand controls.
- Resizable interface with responsive controls and high-DPI scaling.
- Displays the detected grid size, click count, color confidence, and grid confidence.
- Saves the selected screen region and scanning preferences locally.

## Usage

1. Launch `ColorBlockClicker.exe`.
2. Click the **leftmost region-selection button** and drag a rectangle around the full tile grid. Do not include scores, timers, or other surrounding content.
3. Click the **middle start button** or press `F8` to begin detection and clicking.
4. Click the **rightmost stop button** or press `F8` again to stop.
5. Select the region again if the game's grid position or size changes substantially.

You can resize the window from its edges or corners, or drag the lower-right resize grip. The arrow in the title bar collapses or expands the floating window.

## Build

**Requirements:** Windows 10 or 11 and the .NET 8 SDK.

Run `build_release.bat` to create a Windows x64 release executable in `publish/`. The release script attempts a self-contained, single-file build first.

Run `run_dev.bat` to launch the project from source with the .NET SDK.

## Implementation

Built with C#, .NET 8, and Windows Forms. Screen capture uses Windows GDI `BitBlt`, followed by direct pixel sampling and color outlier detection. Mouse clicks and the global hotkey use Win32 APIs. No OCR or online service is required.
