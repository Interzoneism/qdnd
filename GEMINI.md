# QDND Gemini Assistant Configuration

This document provides context for the Gemini AI assistant to understand and effectively assist with the QDND project.

## Project Overview

QDND is a turn-based combat game built with the Godot engine and C#. The project focuses on a robust and testable combat system, with a clear separation between the game logic and the Godot runtime.

The core of the project is a deterministic, turn-based combat system. Combatants are defined in JSON scenarios, and the game logic is driven by a state machine. The project includes a comprehensive testing framework, with unit, integration, performance, and simulation tests.

**Key Technologies:**

*   **Engine:** Godot 4.6
*   **Language:** C#
*   **Testing:** xUnit
*   **Editor:** Visual Studio Code is supported with an MCP configuration.

**Architecture:**

*   **Game Logic:** The core combat logic is implemented in C# and is independent of the Godot runtime. This allows for fast, deterministic testing.
*   **Godot Integration:** The C# logic is integrated with Godot for presentation and user input.
*   **Data-Driven Design:** Combat scenarios, abilities, and statuses are defined in JSON files, allowing for easy modification and extension.
*   **Test-Driven Development:** The project has a strong emphasis on automated testing, with a comprehensive suite of unit, integration, and simulation tests.

## Building and Running

### Running the Game

The main scene is `res://Combat/Arena/CombatArena.tscn`. To run the game, open the project in the Godot editor and press the "Play" button.

### Running Tests

The project uses xUnit for testing. Tests can be run from the command line:

```bash
# Run all tests
dotnet test Tests/QDND.Tests.csproj

# Run with verbose output
dotnet test Tests/QDND.Tests.csproj --logger "console;verbosity=detailed"
```

### Running Simulations

The project includes a simulation harness for running automated gameplay tests. Simulations can be run from the command line by executing a Godot scene in headless mode.

## Development Conventions

*   **Testing:** Follow the principles of deterministic, non-visual, isolated, fast, and documented tests.
*   **Scenarios:** Create new combat scenarios by adding JSON files to the `Data/Scenarios` directory.
*   **Simulation Tests:** Extend the simulation test suite by adding new `SimulationTestCase` definitions.

## Key Files and Directories

*   **`project.godot`:** The main Godot project file.
*   **`QDND.sln`:** The C# solution file.
*   **`Combat/`:** Contains the core combat logic and scenes.
    *   **`Arena/CombatArena.tscn`:** The main game scene.
*   **`Data/`:** Contains game data, including scenarios, abilities, and statuses.
    *   **`Scenarios/`:** Contains JSON files defining combat scenarios.
*   **`Tests/`:** Contains the automated tests for the project.
    *   **`QDND.Tests.csproj`:** The xUnit test project.
*   **`Tools/`:** Contains tools for debugging, profiling, and simulation.
    *   **`Simulation/`:** Contains the simulation testing framework.
*   **`scripts/`:** Contains shell scripts for CI/CD and other automation tasks.

This information should provide a solid foundation for the Gemini assistant to understand the QDND project and provide effective assistance.

## Agent Rules

- Operate strictly within the repository root.
- Prefer minimal diffs and reversible changes.
- Before declaring done, always run `scripts/ci-build.sh` and `scripts/ci-test.sh`.
- If you introduce new systems, update documentation in `/docs`.

## Game Testing & Debugging

The project has two primary methods for testing the game:

### Full-Fidelity Testing

- **Purpose**: Run the game exactly as a player would experience it — full HUD, animations, visuals, camera — with a UI-aware AI playing like a human.
- **When to use**: To verify that the game works end-to-end after any change.
- **Quick start**: `./scripts/run_autobattle.sh --full-fidelity --seed 42`
- **Iron rule**: NEVER disable systems or bypass components to make the test pass. Fix the game code.

### Fast Auto-Battle

- **Purpose**: Run the real `CombatArena.tscn` scene headless with AI-controlled units to expose state machine bugs, action budget issues, turn queue problems, and victory condition failures.
- **When to use**: Quick iteration on combat logic bugs, or stress-testing with many seeds.
- **Quick start**: `./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20`

## Full-Fidelity Testing Guide

Full-fidelity testing runs the **real game** with every system active and uses a UI-aware AI to play it like a human would. Its purpose is to find **game-breaking bugs** that only surface when all systems interact together.

### The Iron Rule

> **Fix the game, never bypass the test.**

When a full-fidelity run fails, the failure is revealing a real bug that a player would hit. The correct response is **always** to fix the underlying game code.

### Quick Start

- **Build first**: `./scripts/ci-build.sh`
- **Basic run**: `./scripts/run_autobattle.sh --full-fidelity --seed 42`
- **With a specific scenario**: `./scripts/run_autobattle.sh --full-fidelity --seed 42 --scenario res://Data/Scenarios/ff_short_ability_mix.json`

### How the UIAwareAIController Works

The `UIAwareAIController` follows a human-like loop, checking for HUD readiness, animations, and decision states before acting. It interacts with the game via the same public APIs as a human player.

### Interpreting Failures

Failures in full-fidelity testing point to real bugs. Common failures include:

- **HUD never becomes ready**: The `ActionBarModel` is not initializing correctly.
- **Animation blocks decision state**: An `ActionTimeline` is not completing.
- **State machine never reaches decision state**: A state transition is missing after an action.
- **Reaction prompt hangs forever**: The UI is not correctly notifying the game of the player's decision.
- **Ability not found in action bar**: The AI's decision is out of sync with the available actions in the UI.

### Debugging Workflow

1.  **Build**: `./scripts/ci-build.sh`
2.  **Run**: `./scripts/run_autobattle.sh --full-fidelity --seed <failing_seed>`
3.  **Observe**: Check the exit code and read the logs.
4.  **Diagnose**: Identify the root cause of the failure.
5.  **Fix**: Fix the game code, not the test.
6.  **Verify**: Re-run the tests to confirm the fix.
7.  **Stress Test**: Run with multiple scenarios and seeds.

