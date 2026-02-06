# Automated Visual Testing & Headless Verification

This document describes the automated verification pipeline for running tests and capturing screenshots without a real display.

## Overview

The pipeline supports two modes:

1. **Headless Tests** - Logic/simulation tests that run without rendering
2. **Screenshot Tests** - Visual tests that capture screenshots for UI regression

Both modes can run in WSL Ubuntu via virtual display (Xvfb).

## Prerequisites

### 1. Godot Binary

Install Godot with .NET support. The scripts look for `godot` in PATH, or use the `GODOT_BIN` environment variable.

```bash
# Option A: Add Godot to PATH
export PATH="$PATH:/path/to/godot"

# Option B: Set GODOT_BIN
export GODOT_BIN="/home/user/godot/Godot_v4.5-stable_mono_linux_x86_64/godot"
```

### 2. Xvfb (Virtual Framebuffer)

Required for screenshot tests in headless environments (WSL, CI):

```bash
sudo apt-get update
sudo apt-get install xvfb
```

### 3. ImageMagick (for screenshot comparison)

Required for visual regression testing:

```bash
sudo apt-get install imagemagick bc
```

## Headless Tests

Runs deterministic validation tests in Godot headless mode. These tests verify:
- Core services initialize correctly
- Data registry loads and validates
- Combat scenarios load without errors
- Deterministic simulation produces expected results

### Running

```bash
# Basic usage
./scripts/run_headless_tests.sh

# With custom Godot path
GODOT_BIN=/path/to/godot ./scripts/run_headless_tests.sh
```

### Exit Codes

- `0` - All tests passed
- `1` - One or more tests failed
- `2` - Setup/configuration error

### Direct Godot Command

```bash
godot --headless --path . res://Tools/CLIRunner.tscn -- --run-tests
```

## Screenshot Tests

Captures screenshots of Godot scenes for HUD/UI visual regression testing. Runs under Xvfb virtual display to work without a monitor.

### Running

```bash
# Default: captures CombatArena at 1920x1080
./scripts/run_screenshots.sh

# Custom scene
./scripts/run_screenshots.sh --scene res://Combat/Arena/CombatArena.tscn

# Custom resolution
./scripts/run_screenshots.sh --width 2560 --height 1440

# Without Xvfb (requires display)
./scripts/run_screenshots.sh --no-xvfb
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--scene <path>` | Scene to capture | `res://Combat/Arena/CombatArena.tscn` |
| `--width <int>` | Screenshot width | 1920 |
| `--height <int>` | Screenshot height | 1080 |
| `--no-xvfb` | Run without virtual display | false |

### Exit Codes

- `0` - Screenshot captured successfully
- `1` - Screenshot capture failed
- `2` - Setup/configuration error

### Direct Godot Command

```bash
xvfb-run -a -s "-screen 0 1920x1080x24" godot --path . --rendering-driver opengl3 \
  res://Tools/ScreenshotRunner.tscn -- \
  --scene res://Combat/Arena/CombatArena.tscn \
  --screenshot-out artifacts/screens \
  --w 1920 --h 1080
```

## Screenshot Comparison

Compares new screenshots against baseline images to detect visual regressions.

### Setup Baseline

1. Run screenshot tests to generate images:
   ```bash
   ./scripts/run_screenshots.sh
   ```

2. Copy approved screenshots to baseline:
   ```bash
   cp artifacts/screens/*_latest.png artifacts/baseline/
   ```

3. Track baseline in git (optional but recommended):
   ```bash
   git add artifacts/baseline/
   git commit -m "Add visual regression baseline"
   ```

### Running Comparison

```bash
# Compare all screenshots
./scripts/compare_screenshots.sh

# Compare with custom threshold (0.0-1.0)
./scripts/compare_screenshots.sh --threshold 0.01

# Compare specific scene
./scripts/compare_screenshots.sh --scene CombatArena
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--threshold <float>` | Allowed difference (0.0-1.0) | 0.001 |
| `--scene <name>` | Filter by scene name | all |
| `--baseline <dir>` | Baseline directory | `artifacts/baseline` |
| `--screens <dir>` | Screenshots directory | `artifacts/screens` |
| `--diff <dir>` | Diff output directory | `artifacts/diff` |

### Exit Codes

- `0` - All comparisons passed (or no baselines exist)
- `1` - Visual regression detected
- `2` - Setup/configuration error

### Diff Images

When a mismatch is detected, a diff image is saved to `artifacts/diff/` showing the differences highlighted.

## Visual AI-vs-AI Playback

`CombatArena.tscn` can run full AI-vs-AI directly in the normal game window using the same `RealtimeAIController` that powers auto-battle.

### Current Default

- `CombatArena` has `UseRealtimeAIForAllFactions = true`.
- Running the project (`godot --path .` or Play in editor) starts AI control for both teams automatically.
- The arena-side legacy AI driver (`UseBuiltInAI`) is disabled automatically in this mode to avoid double-driving turns.

### Toggle Behavior

If you want manual player control again, set `UseRealtimeAIForAllFactions = false` on `res://Combat/Arena/CombatArena.tscn` (or in the Inspector on the `CombatArena` node).

## Artifacts

| Directory | Description | Git Status |
|-----------|-------------|------------|
| `artifacts/screens/` | Generated screenshots | Ignored |
| `artifacts/baseline/` | Baseline images for comparison | Tracked (optional) |
| `artifacts/diff/` | Diff images when mismatch detected | Ignored |

## CI Integration

### Example GitHub Actions Workflow

```yaml
name: Visual Tests

on: [push, pull_request]

jobs:
  visual-tests:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Godot
        run: |
          wget https://github.com/godotengine/godot/releases/download/4.5-stable/Godot_v4.5-stable_mono_linux_x86_64.zip
          unzip Godot_v4.5-stable_mono_linux_x86_64.zip
          echo "GODOT_BIN=$(pwd)/Godot_v4.5-stable_mono_linux_x86_64/godot" >> $GITHUB_ENV
      
      - name: Install dependencies
        run: sudo apt-get install -y xvfb imagemagick bc
      
      - name: Run headless tests
        run: ./scripts/run_headless_tests.sh
      
      - name: Run screenshot tests
        run: ./scripts/run_screenshots.sh
      
      - name: Compare screenshots
        run: ./scripts/compare_screenshots.sh
      
      - name: Upload artifacts
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: visual-diff
          path: artifacts/diff/
```

## Troubleshooting

### "Godot not found"

Set `GODOT_BIN` to your Godot installation:
```bash
export GODOT_BIN="/path/to/godot"
```

### "xvfb-run not found"

Install Xvfb:
```bash
sudo apt-get install xvfb
```

### "compare not found"

Install ImageMagick:
```bash
sudo apt-get install imagemagick
```

### Screenshot is black/empty

Try using `--rendering-driver opengl3` which is more compatible with virtual displays:
```bash
xvfb-run -a godot --path . --rendering-driver opengl3 res://Tools/ScreenshotRunner.tscn
```

### WSL-specific issues

Ensure you're running in WSL with a proper Linux environment. The scripts require bash and standard Unix utilities.

## Architecture

### Headless Test Runner (`Tools/HeadlessTestRunner.cs`)

A pure C# test runner that validates:
- Data registry loading and validation
- Combat context initialization
- Turn queue state machine
- Deterministic RNG behavior
- Scenario loading

### Screenshot Runner (`Tools/ScreenshotRunner.cs`)

A Godot scene that:
1. Parses CLI arguments
2. Sets window resolution
3. Loads the target scene
4. Waits for UI to settle (configurable frames)
5. Captures viewport to PNG
6. Exits with appropriate code

### CLI Entry Point (`Tools/CLIEntryPoint.cs`)

Routes CLI arguments to appropriate runners. Parses arguments after `--` delimiter.
