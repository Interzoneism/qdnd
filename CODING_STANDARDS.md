# Coding Standards

## Naming
- Use PascalCase for classes/types.
- Use camelCase for variables and methods.
- Folder and file names should be PascalCase.

## Logging
- Use structured logs for all combat events and state transitions.
- Prefer event-driven logging over ad-hoc print statements.

## Determinism
- All rules and combat logic must be deterministic and reproducible given the same inputs.
- Use dependency injection for services and context.

## Data-Driven Design
- Prefer configuration/data files (JSON/YAML/Resources) over hardcoded values.
- Avoid scene-tight coupling; logic should be testable headless.

## Tests
- Every new system must include at least one automated test or scenario in Testbed.
