# Parity Validation Gate

## Purpose

`parity-validate` is a deterministic CI gate that verifies core data/runtime parity for combat content.

It checks:

- Duplicate IDs across loaded data categories (abilities, statuses, races, classes, feats, beast forms, scenarios, weapons, armors)
- Missing granted ability links from race/class/feat/beast-form features
- Ability/status link integrity (`apply_status`, `remove_status`)
- Effect handler coverage (every ability effect type is registered in `EffectPipeline`)
- JSON schema compatibility via typed deserialization of all combat data packs

## Commands

Run the gate directly:

```bash
./scripts/ci-parity-validate.sh
```

Run full test gate (includes parity gate):

```bash
./scripts/ci-test.sh
```

## Allowlist policy

Known legacy gaps are tracked in:

- `Data/Validation/parity_allowlist.json`

Rules:

- New parity gaps must fail CI.
- If a previously allowlisted gap is resolved, remove it from the allowlist.
- Keep allowlist entries minimal and explicitly justified by current implementation scope.

## Implementation location

- Validator: `Tests/Helpers/ParityDataValidator.cs`
- Gate test: `Tests/Unit/ParityValidationTests.cs`
