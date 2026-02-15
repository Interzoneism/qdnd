# Combat Tests

This folder contains automated tests for the combat system.

## Current Status

The canonical way to run the suite is through repo scripts:

```bash
./scripts/ci-build.sh
./scripts/ci-test.sh
```

`ci-test.sh` also runs the parity validation gate (`scripts/ci-parity-validate.sh`).

## Structure (high level)

```
Tests/
├── QDND.Tests.csproj    # xUnit test project
├── Unit/                 # Unit tests for core systems
├── Integration/          # Integration tests across subsystems
├── Performance/          # Benchmark/perf-focused tests
└── Simulation/           # Simulation and scenario regression tests
```

## Running Tests

```bash
# Run all tests
dotnet test Tests/QDND.Tests.csproj

# Run with verbose output
dotnet test Tests/QDND.Tests.csproj --logger "console;verbosity=detailed"
```

## Coverage Notes

Coverage evolves quickly; avoid hard-coding test counts in this file. For current status, run:

```bash
dotnet test Tests/QDND.Tests.csproj -c Release --no-build
```

## Testing Framework

- **xUnit** for unit tests
- Tests are independent of Godot runtime
- Use isolated test versions of classes to avoid Godot dependencies

## Writing Tests

Follow these principles:
- **Deterministic**: Use fixed RNG seeds
- **Non-visual**: Assert on state/events, not visual output
- **Isolated**: Each test should be independent
- **Fast**: Unit tests should complete in milliseconds
- **Documented**: Include clear failure messages

## Future Tests (Phase B+)

### Planned Unit Tests
- Rules engine calculations
- Damage mitigation ordering
- Status duration logic
- Action economy validation

### Planned Simulation Tests
- Deterministic combat runs
- Multi-step combat sequences
- Invariant checking (HP bounds, resource limits)
- Save/load equivalence tests
