# Combat Tests

This folder contains automated tests for the combat system.

## Structure

- **Unit/**: Unit tests for individual components and rules
  - Rules engine calculations
  - Status duration logic
  - Damage mitigation ordering
  - Action economy validation
  - etc.

- **Simulation/**: Integration and simulation tests
  - Deterministic combat runs
  - Multi-step combat sequences
  - Invariant checking (HP bounds, resource limits)
  - State machine transition validation
  - Save/load equivalence tests

## Testing Framework

Tests use standard C# testing frameworks compatible with Godot:
- NUnit or xUnit for unit tests
- Custom simulation harness for deterministic combat runs

## Testbed Integration

All tests should be runnable:
1. Via standard test runners (CI/CD compatible)
2. Through the Testbed scene (headless mode)
3. With deterministic RNG seeds for reproducibility

## Writing Tests

Follow these principles:
- **Deterministic**: Use fixed RNG seeds
- **Non-visual**: Assert on state/events, not visual output
- **Isolated**: Each test should be independent
- **Fast**: Unit tests should complete in milliseconds
- **Documented**: Include clear failure messages

## Current State

Phase A implementation pending. Test infrastructure will be built alongside the core combat systems.
