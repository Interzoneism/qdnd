## Phase 1 Complete: Combat State Snapshot Model

Created serializable DTOs that capture complete combat state for save/load persistence. All snapshot classes support JSON serialization with round-trip verification and include version fields for future migration support.

**Files created/changed:**
- Combat/Persistence/CombatSnapshot.cs
- Combat/Persistence/CombatantSnapshot.cs
- Combat/Persistence/SurfaceSnapshot.cs
- Combat/Persistence/StatusSnapshot.cs
- Combat/Persistence/StackItemSnapshot.cs
- Combat/Persistence/CooldownSnapshot.cs
- Combat/Persistence/ReactionPromptSnapshot.cs
- Combat/Persistence/PropSnapshot.cs
- Tests/Unit/CombatSnapshotTests.cs

**Functions created/changed:**
- CombatSnapshot class with Version, Timestamp, CombatState, RNG state, entity lists
- CombatantSnapshot with full identity, position, resources, stats, action budget
- SurfaceSnapshot with position, type, duration, ownership
- StatusSnapshot with stacking, duration, custom data
- StackItemSnapshot with resolution stack fields (Id, ActionType, Source/Target, Cancelled, Depth)
- CooldownSnapshot with charges, remaining cooldown, decrement type
- ReactionPromptSnapshot for pending player prompts
- PropSnapshot for spawned battlefield objects

**Tests created/changed:**
- CombatSnapshot_Serializes_ToValidJson
- CombatSnapshot_RoundTrip_PreservesAllFields
- CombatSnapshot_EmptyCollections_SerializeCorrectly
- CombatSnapshot_VersionField_IsPresent
- CombatantSnapshot_Serializes_ToValidJson
- CombatantSnapshot_RoundTrip_PreservesAllFields
- SurfaceSnapshot_Serializes_ToValidJson
- SurfaceSnapshot_RoundTrip_PreservesAllFields
- StatusSnapshot_Serializes_ToValidJson
- StatusSnapshot_RoundTrip_PreservesAllFields
- StatusSnapshot_CustomData_SerializesCorrectly
- StackItemSnapshot_Serializes_ToValidJson
- StackItemSnapshot_RoundTrip_PreservesAllFields
- StackItemSnapshot_PayloadData_HandlesComplexJson
- CooldownSnapshot_Serializes_ToValidJson
- CooldownSnapshot_RoundTrip_PreservesAllFields
- ReactionPromptSnapshot_Serializes_ToValidJson
- ReactionPromptSnapshot_RoundTrip_PreservesAllFields
- PropSnapshot_Serializes_ToValidJson
- PropSnapshot_RoundTrip_PreservesAllFields
- CompleteSnapshot_WithAllNestedData_SerializesCorrectly
- EmptySnapshot_SerializesWithDefaults

**Review Status:** APPROVED

**Git Commit Message:**
```
feat: Add combat state snapshot DTOs for persistence

- Add CombatSnapshot as main container with Version field
- Add CombatantSnapshot with identity, position, stats, action budget
- Add SurfaceSnapshot, StatusSnapshot for battlefield state
- Add StackItemSnapshot for mid-reaction save support
- Add CooldownSnapshot with charges and decrement tracking
- Add ReactionPromptSnapshot and PropSnapshot for complete state
- Add 22 unit tests verifying JSON serialization round-trips
```
