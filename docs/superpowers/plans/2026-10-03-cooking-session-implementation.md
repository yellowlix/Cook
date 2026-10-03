# Cooking Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable, data-driven cooking session with six ordered operations, four round grades, early recipe completion, immediate input handoff after one-shot actions, placeholder HUD, distinct placeholder animations, and a semantic Unity scene hierarchy.

**Architecture:** `CookingSession` is a pure C# state machine and owns all rules, timers, ordered progress, grading, and recipe completion. ScriptableObjects author content and convert it to immutable runtime data. `CookingInputController`, `CookingAnimationPresenter`, and `CookingHud` are Unity adapters that consume session events; Animator, UI, and scene objects never decide gameplay state.

**Tech Stack:** Unity 2022.3.17f1c1, C#/.NET Standard, Input System 1.7.0 generated wrapper, Mecanim Animator, uGUI, Unity Test Framework 1.1.33, Unity MCP.

## Global Constraints

- Fixed station order is SoupPot (left), CuttingBoard (center), FryingPan (right).
- The J key is interpreted by the active authored operation as short press, repeated press, or hold.
- The six first-stage operations are ChopOnce, ChopRepeated, StirOnce, StirHold, PanFlipOnce, and PanFlipHold.
- Repeated and hold progress is preserved when input stops or the player changes station.
- A short-press operation completes immediately and the next operation accepts input in the same frame; animation playback never gates core state.
- Each authored round has exactly 2–3 ordered operations and receives one of Miss, Good, Great, or Excellent.
- Default thresholds are Excellent ≤ standard × 0.85, Great ≤ standard × 1.00, Good ≤ standard × 1.40, then Miss.
- Miss/Good/Great/Excellent award 0/1.00/1.25/1.50 times the base round progress.
- Recipe success target is exactly 100%; reaching it skips remaining rounds.
- First stage uses global operation defaults only; no per-recipe operation overrides.
- Core code must not reference MonoBehaviour, Input System, Animator, UI, scene objects, VFX, Timeline, or Animation Events.
- Generated file `Assets/Scripts/Input/CookInputActions.cs` must not be edited manually.
- Before every `.unity`, `.asset`, `.anim`, `.controller`, `.meta`, or prefab mutation, verify the worktree is clean and commit preceding code changes.
- Unity YAML must be modified through Unity Editor/MCP or Unity editor APIs, never by raw text replacement.
- Preserve UTF-8 encoding and existing Chinese code comments.
- Add concise Chinese comments at core state transitions and Unity-adapter boundaries; do not add line-by-line C# syntax explanations.

## File Map

Create:

- `Assets/Scripts/Cooking/Core/CookingTypes.cs` — enums and immutable runtime data.
- `Assets/Scripts/Cooking/Core/CookingSession.cs` — pure gameplay state machine.
- `Assets/Scripts/Cooking/Configuration/OperationDefinition.cs` — authored operation asset and conversion.
- `Assets/Scripts/Cooking/Configuration/RecipeDefinition.cs` — authored rounds, thresholds, validation, and conversion.
- `Assets/Scripts/Cooking/Presentation/CookingAnimationPresenter.cs` — session events to Animator triggers.
- `Assets/Scripts/Cooking/Presentation/CookingHud.cs` — placeholder ordered-operation, progress, grade, and result UI.
- `Assets/Tests/EditMode/CookingSessionTests.cs` — core rule tests.
- `Assets/Tests/EditMode/CookingDefinitionTests.cs` — authored-data validation tests.
- `Assets/Tests/PlayMode/Cook.PlayModeTests.asmdef` — PlayMode test assembly.
- `Assets/Tests/PlayMode/CookingPresentationTests.cs` — immediate handoff and station visibility integration tests.
- `Assets/Cooking/Operations/*.asset` — six global operation definitions.
- `Assets/Cooking/Recipes/DemoRecipe.asset` — fixed demo sequence.
- `Assets/Animations/Clips/*.anim` — three one-shot and nine sustained placeholder clips plus Idle.
- `Assets/Animations/Controllers/CookingCharacter.controller` — minimal cooking Animator graph.

Modify:

- `Assets/Scripts/CookingInputController.cs` — replace station-mode demo logic with session orchestration.
- `Assets/Scripts/Cook.Runtime.asmdef` — add uGUI reference.
- `Assets/Tests/EditMode/Cook.Tests.asmdef` — reference runtime assembly.
- `Assets/Scenes/SampleScene.unity` — semantic hierarchy, HUD, configuration references, Animator references.

Remove after replacement is green:

- `Assets/Scripts/CookingInputLogic.cs` and `.meta`.
- `Assets/Tests/EditMode/CookingInputLogicTests.cs` and `.meta`.

Keep but disconnect in this phase:

- Existing placeholder clips `Move_Side`, `Miss`, `Finish`, and `Celebrate`; they are not part of the new controller and are not deleted without a separate cleanup request.

---

### Task 1: Pure runtime data and cooking state machine

**Files:**

- Create: `Assets/Scripts/Cooking/Core/CookingTypes.cs`
- Create: `Assets/Scripts/Cooking/Core/CookingSession.cs`
- Create: `Assets/Tests/EditMode/CookingSessionTests.cs`
- Modify: `Assets/Tests/EditMode/Cook.Tests.asmdef`

**Interfaces:**

- Consumes: no Unity scene or presentation objects.
- Produces: `OperationRuntimeData`, `RoundRuntimeData`, `RecipeRuntimeData`, and `CookingSession` with `StartRecipe`, `MoveStation`, `PressCook`, `ReleaseCook`, `Tick`, and `ContinueAfterRoundResult`.

- [ ] **Step 1: Give the EditMode assembly a direct runtime reference**

Replace `Assets/Tests/EditMode/Cook.Tests.asmdef` with:

```json
{
  "name": "Cook.Tests",
  "references": ["Cook.Runtime"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": true
}
```

- [ ] **Step 2: Add compiling runtime type shells and focused failing tests**

Create the enums and constructor signatures below so tests compile, but leave session methods throwing `NotImplementedException`:

```csharp
namespace Cook.Core
{
    public enum CookingStation { SoupPot, CuttingBoard, FryingPan }
    public enum CookingInputMode { ShortPress, RepeatedPress, Hold }
    public enum CookingOperationType { ChopOnce, ChopRepeated, StirOnce, StirHold, PanFlipOnce, PanFlipHold }
    public enum CookingAnimationMode { OneShot, Sustained }
    public enum CookingSessionState { Idle, RoundIntro, OperationActive, RoundResult, RecipeSuccess, RecipeFailure }
    public enum CookingGrade { Miss, Good, Great, Excellent }
}
```

`CookingSessionTests.cs` must contain concrete tests for:

```csharp
[Test] public void ShortPress_CompletesAndStartsNextOperationImmediately();
[Test] public void Operations_MustBeCompletedInAuthoredOrder();
[Test] public void WrongStation_PublishesInvalidInputWithoutProgress();
[Test] public void RepeatedPress_AddsOneUnitPerPress();
[Test] public void Hold_AddsDeltaTimeOnlyWhileHeldAtCorrectStation();
[Test] public void HoldProgress_IsPreservedAfterReleaseAndStationChange();
[TestCase(0.84f, CookingGrade.Excellent)]
[TestCase(0.95f, CookingGrade.Great)]
[TestCase(1.20f, CookingGrade.Good)]
public void CompletedRound_UsesFourGradeThresholds(float ratio, CookingGrade expected);
[Test] public void GoodTimeout_ProducesMissAndNoRecipeProgress();
[Test] public void ExcellentRounds_CanReachOneHundredPercentEarly();
[Test] public void ExhaustedRoundsBelowTarget_ProducesRecipeFailure();
```

The immediate-handoff assertion must call `PressCook()` twice without calling `Tick()` between calls and assert that both consecutive one-shot operations completed.

- [ ] **Step 3: Run the focused test suite and verify red**

Use Unity MCP:

```json
{"mode":"EditMode","assembly_names":["Cook.Tests"],"include_failed_tests":true}
```

Expected: test job completes with failures caused by the unimplemented `CookingSession` methods, not compilation errors.

- [ ] **Step 4: Implement immutable runtime data**

`CookingTypes.cs` must expose these exact constructors and read-only properties:

```csharp
public sealed class OperationRuntimeData
{
    public OperationRuntimeData(
        string id,
        CookingOperationType type,
        CookingStation station,
        CookingInputMode inputMode,
        float requiredAmount,
        float standardDuration,
        CookingAnimationMode animationMode,
        string animatorStartTrigger,
        string animatorStopTrigger);

    public string Id { get; }
    public CookingOperationType Type { get; }
    public CookingStation Station { get; }
    public CookingInputMode InputMode { get; }
    public float RequiredAmount { get; }
    public float StandardDuration { get; }
    public CookingAnimationMode AnimationMode { get; }
    public string AnimatorStartTrigger { get; }
    public string AnimatorStopTrigger { get; }
}

public sealed class RoundRuntimeData
{
    public RoundRuntimeData(
        IReadOnlyList<OperationRuntimeData> operations,
        float excellentTime,
        float greatTime,
        float goodTime);

    public IReadOnlyList<OperationRuntimeData> Operations { get; }
    public float ExcellentTime { get; }
    public float GreatTime { get; }
    public float GoodTime { get; }
}

public sealed class RecipeRuntimeData
{
    public RecipeRuntimeData(string id, string displayName, IReadOnlyList<RoundRuntimeData> rounds);
    public string Id { get; }
    public string DisplayName { get; }
    public IReadOnlyList<RoundRuntimeData> Rounds { get; }
}
```

Constructors must reject blank IDs, null operation/round collections, round counts outside 2–3 operations, non-positive required amounts/durations, and thresholds that do not satisfy `0 < ExcellentTime <= GreatTime <= GoodTime`.

- [ ] **Step 5: Implement the session state machine**

`CookingSession` must expose exactly:

```csharp
public CookingSessionState State { get; }
public CookingStation CurrentStation { get; }
public int CurrentRoundIndex { get; }
public int CurrentOperationIndex { get; }
public float CurrentOperationProgress01 { get; }
public float RecipeProgress01 { get; }
public float RoundElapsedTime { get; }
public OperationRuntimeData CurrentOperation { get; }
public RoundRuntimeData CurrentRound { get; }

public event Action<CookingSessionState> StateChanged;
public event Action<CookingStation> StationChanged;
public event Action<OperationRuntimeData, int, int> OperationStarted;
public event Action<float> OperationProgressChanged;
public event Action<OperationRuntimeData> OperationCompleted;
public event Action InvalidInput;
public event Action<CookingGrade> RoundEvaluated;
public event Action<float> RecipeProgressChanged;
public event Action RecipeSucceeded;
public event Action RecipeFailed;

public void StartRecipe(RecipeRuntimeData recipe, CookingStation startingStation);
public void MoveStation(int direction);
public void PressCook();
public void ReleaseCook();
public void Tick(float deltaTime);
public void ContinueAfterRoundResult();
```

Use these implementation rules:

```csharp
private void CompleteCurrentOperation()
{
    OperationRuntimeData completed = CurrentOperation;
    currentAmount = completed.RequiredAmount;
    OperationProgressChanged?.Invoke(1f);
    OperationCompleted?.Invoke(completed);
    isHolding = false;
    currentOperationIndex++;

    if (currentOperationIndex < CurrentRound.Operations.Count)
    {
        StartCurrentOperation(); // same call stack; no animation wait
        return;
    }

    EvaluateRound(GradeForElapsedTime());
}
```

`PressCook()` must ignore non-`OperationActive` states; publish `InvalidInput` at the wrong station; add all required progress for `ShortPress`; add `1f` for `RepeatedPress`; and set `isHolding = true` for `Hold`. `MoveStation()` clamps to the three enum positions and clears `isHolding` without clearing `currentAmount`. `Tick()` adds held time first, then produces Miss if state is still active and elapsed time exceeds `GoodTime`.

`EvaluateRound()` stores the grade, enters `RoundResult`, and awards:

```csharp
float multiplier = grade switch
{
    CookingGrade.Excellent => 1.50f,
    CookingGrade.Great => 1.25f,
    CookingGrade.Good => 1.00f,
    _ => 0f
};

float baseProgress = 1f / recipe.Rounds.Count;
recipeProgress = Math.Min(1f, recipeProgress + baseProgress * multiplier);
```

`ContinueAfterRoundResult()` must enter success when progress is 1, start the next fixed round when available, or enter failure after the last round.

- [ ] **Step 6: Run EditMode tests and verify green**

Run the same `Cook.Tests` EditMode job. Expected: all new `CookingSessionTests` pass and no compilation error appears in Console.

- [ ] **Step 7: Commit the pure core**

```powershell
git add Assets/Scripts/Cooking/Core Assets/Tests/EditMode/CookingSessionTests.cs Assets/Tests/EditMode/Cook.Tests.asmdef
git commit -m "feat: add cooking session state machine"
```

---

### Task 2: ScriptableObject authoring and validation

**Files:**

- Create: `Assets/Scripts/Cooking/Configuration/OperationDefinition.cs`
- Create: `Assets/Scripts/Cooking/Configuration/RecipeDefinition.cs`
- Create: `Assets/Tests/EditMode/CookingDefinitionTests.cs`

**Interfaces:**

- Consumes: runtime constructors from Task 1.
- Produces: `OperationDefinition.ToRuntime()` and `RecipeDefinition.TryBuildRuntime(out RecipeRuntimeData, out string)`.

- [ ] **Step 1: Write failing authoring tests**

Cover these exact cases with `ScriptableObject.CreateInstance` and `UnityEditor.SerializedObject`:

```csharp
[Test] public void OperationDefinition_ConvertsAllGameplayAndAnimationFields();
[Test] public void RecipeDefinition_RejectsEmptyRecipe();
[Test] public void RecipeDefinition_RejectsRoundOutsideTwoToThreeOperations();
[Test] public void RecipeDefinition_RejectsNullOperationReference();
[Test] public void RecipeDefinition_RejectsUnorderedThresholds();
[Test] public void RecipeDefinition_ComputesDefaultThresholdsFromOperationDurationsAndStationChanges();
```

For the default-threshold test, use operation durations `1 + 2 + 1`, two station changes at `0.25` seconds each, and assert `Excellent=3.825`, `Great=4.5`, `Good=6.3` within `0.001`.

- [ ] **Step 2: Run tests and verify red**

Expected: failures because the two definition types do not exist.

- [ ] **Step 3: Implement `OperationDefinition`**

Use `[CreateAssetMenu(menuName = "Cook/Operation Definition")]` and these serialized fields:

```csharp
[SerializeField] private string operationId;
[SerializeField] private CookingOperationType operationType;
[SerializeField] private CookingStation station;
[SerializeField] private CookingInputMode inputMode;
[SerializeField, Min(0.01f)] private float requiredAmount = 1f;
[SerializeField, Min(0.01f)] private float standardDuration = 0.5f;
[SerializeField] private Sprite icon;
[SerializeField] private CookingAnimationMode animationMode;
[SerializeField] private string animatorStartTrigger;
[SerializeField] private string animatorStopTrigger;
```

`ToRuntime()` copies all non-visual fields into `OperationRuntimeData`. `OnValidate()` clamps numeric fields; clears `animatorStopTrigger` for one-shot operations; and logs an Inspector validation error if operation type, station, and input mode are inconsistent with the fixed mapping.

- [ ] **Step 4: Implement `RecipeDefinition`**

Use `[CreateAssetMenu(menuName = "Cook/Recipe Definition")]`, a serializable nested `RoundDefinition`, and these exact authored fields:

```csharp
[Serializable]
public sealed class RoundDefinition
{
    [SerializeField] private List<OperationDefinition> operations;
    [SerializeField] private bool useCustomTimeThresholds;
    [SerializeField] private float excellentTime;
    [SerializeField] private float greatTime;
    [SerializeField] private float goodTime;
}

[SerializeField] private string recipeId;
[SerializeField] private string displayName;
[SerializeField] private Sprite icon;
[SerializeField, Min(0f)] private float stationChangeAllowance = 0.25f;
[SerializeField] private List<RoundDefinition> rounds;
```

`TryBuildRuntime` must return `false` and a path-specific error such as `Round 2 operation 1 is null`. For default thresholds, sum `StandardDuration`, add `stationChangeAllowance` whenever adjacent stations differ, then multiply by `0.85`, `1.00`, and `1.40`.

- [ ] **Step 5: Run all EditMode tests and verify green**

Expected: all `CookingSessionTests`, `CookingDefinitionTests`, and still-existing legacy input tests pass.

- [ ] **Step 6: Commit the configuration layer**

```powershell
git add Assets/Scripts/Cooking/Configuration Assets/Tests/EditMode/CookingDefinitionTests.cs
git commit -m "feat: add cooking recipe authoring"
```

---

### Task 3: Input orchestration, animation adapter, and placeholder HUD

**Files:**

- Create: `Assets/Scripts/Cooking/Presentation/CookingAnimationPresenter.cs`
- Create: `Assets/Scripts/Cooking/Presentation/CookingHud.cs`
- Modify: `Assets/Scripts/CookingInputController.cs`
- Modify: `Assets/Scripts/Cook.Runtime.asmdef`
- Create: `Assets/Tests/PlayMode/Cook.PlayModeTests.asmdef`
- Create: `Assets/Tests/PlayMode/CookingPresentationTests.cs`
- Remove: `Assets/Scripts/CookingInputLogic.cs` and `.meta`
- Remove: `Assets/Tests/EditMode/CookingInputLogicTests.cs` and `.meta`

**Interfaces:**

- Consumes: `RecipeDefinition.TryBuildRuntime` and all `CookingSession` commands/events.
- Produces: Input System adapter, station visibility, Animator trigger adapter, and HUD binding.

- [ ] **Step 1: Add UI assembly reference and PlayMode assembly**

`Cook.Runtime.asmdef` references must become:

```json
"references": ["Unity.InputSystem", "UnityEngine.UI"]
```

Create the PlayMode asmdef:

```json
{
  "name": "Cook.PlayModeTests",
  "references": ["Cook.Runtime"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 2: Write failing presentation tests**

Create tests that instantiate three station presenter objects and assert:

```csharp
[UnityTest] public IEnumerator StationChanged_ActivatesExactlyOnePresenter();
[UnityTest] public IEnumerator OneShotCompletion_AllowsNextInputBeforeAnimatorFinishes();
[UnityTest] public IEnumerator SustainedOperation_SendsStartThenStopTrigger();
```

Use two consecutive short operations in the second test; call the controller/session input seam twice in one frame and assert current operation index advanced twice. Do not wait for `AnimatorStateInfo.normalizedTime`.

- [ ] **Step 3: Run PlayMode tests and verify red**

Use `run_tests` with `mode=PlayMode`, `assembly_names=["Cook.PlayModeTests"]`, and `init_timeout=120000`. Expected: failures because the new adapters are missing.

- [ ] **Step 4: Implement `CookingAnimationPresenter`**

Define one serializable binding per station:

```csharp
[Serializable]
public sealed class StationPresentation
{
    public CookingStation station;
    public GameObject root;
    public Animator animator;
}
```

Expose `Bind(CookingSession session)` and `Unbind()`. On `StationChanged`, activate only the matching root. On `OperationStarted`, call `SetTrigger(operation.AnimatorStartTrigger)` on that operation's station Animator. On `OperationCompleted`, send `AnimatorStopTrigger` only when `AnimationMode == Sustained`. Never query Animator state and never delay a session command.

- [ ] **Step 5: Replace `CookingInputController` with a session orchestrator**

Serialized fields:

```csharp
[SerializeField] private RecipeDefinition recipe;
[SerializeField] private CookingStation startingStation = CookingStation.SoupPot;
[SerializeField] private CookingAnimationPresenter animationPresenter;
[SerializeField] private CookingHud hud;
[SerializeField, Min(0f)] private float roundResultDisplaySeconds = 0.75f;
```

Lifecycle and callbacks:

```csharp
private void Start()
{
    if (!recipe.TryBuildRuntime(out RecipeRuntimeData runtime, out string error))
    {
        Debug.LogError($"Cannot start cooking: {error}", this);
        enabled = false;
        return;
    }

    session = new CookingSession();
    animationPresenter.Bind(session);
    hud.Bind(session);
    session.StartRecipe(runtime, startingStation);
}

private void Update()
{
    session?.Tick(Time.deltaTime);
    if (session?.State == CookingSessionState.RoundResult)
    {
        resultElapsed += Time.deltaTime;
        if (resultElapsed >= roundResultDisplaySeconds)
        {
            resultElapsed = 0f;
            session.ContinueAfterRoundResult();
        }
    }
}

private void OnChangeStation(InputAction.CallbackContext context)
{
    float axis = context.ReadValue<float>();
    if (Mathf.Abs(axis) >= 0.5f) session?.MoveStation(axis < 0f ? -1 : 1);
}

private void OnCookPressed(InputAction.CallbackContext context) => session?.PressCook();
private void OnCookReleased(InputAction.CallbackContext context) => session?.ReleaseCook();
```

Keep the existing generated-wrapper creation, paired subscription/unsubscription, and disposal pattern. Expose a read-only `CookingSession Session` property for PlayMode tests and debugging.

- [ ] **Step 6: Implement `CookingHud`**

Serialize `Text operationSequenceText`, `Slider operationProgressSlider`, `Slider recipeProgressSlider`, `Text gradeText`, and `Text resultText`. `Bind` subscribes to session events, `Unbind` removes all subscriptions. Render operations in authored order as:

```text
✓ 搅一下  >  [持续搅动]  >  连续切菜
```

Show the operation slider only for `RepeatedPress` and `Hold`; update recipe slider from `RecipeProgressChanged`; show the four grade names; show `完成` or `失败` on terminal events. UI must never invoke state transitions.

- [ ] **Step 7: Remove the superseded demo input logic**

Delete `CookingInputLogic.cs` and `CookingInputLogicTests.cs` only after all references are removed and compilation is green. Keep `CookInputActions.cs` untouched.

- [ ] **Step 8: Run EditMode and PlayMode suites**

Expected: all tests pass; `read_console` returns no compile errors.

- [ ] **Step 9: Commit adapters and tests**

```powershell
git add Assets/Scripts Assets/Tests
git commit -m "feat: connect cooking input presentation and hud"
```

---

### Task 4: Six operation assets, demo recipe, and corrected Animator graph

**Files:**

- Create: `Assets/Cooking/Operations/*.asset`
- Create: `Assets/Cooking/Recipes/DemoRecipe.asset`
- Create: `Assets/Animations/Clips/*.anim`
- Create: `Assets/Animations/Controllers/CookingCharacter.controller`
- Modify: relevant `.meta` files through Unity AssetDatabase.

**Interfaces:**

- Consumes: ScriptableObject types and trigger names from Tasks 2–3.
- Produces: concrete content assets referenced by the scene.

- [ ] **Step 1: Verify the code checkpoint before Unity asset mutation**

Run `git status --short`. Expected: empty. If not empty, stop and commit only the preceding task's intended files before touching Unity assets.

- [ ] **Step 2: Create the six operation assets with exact values**

Use Unity ScriptableObject APIs/MCP, not hand-written YAML:

| Asset | Type | Station | Input | Required | Standard | Animation | Start trigger | Stop trigger |
|---|---|---|---|---:|---:|---|---|---|
| `ChopOnce.asset` | ChopOnce | CuttingBoard | ShortPress | 1 | 0.50 | OneShot | `ChopOnce` | empty |
| `ChopRepeated.asset` | ChopRepeated | CuttingBoard | RepeatedPress | 6 | 2.50 | Sustained | `ChopRepeatedStart` | `ChopRepeatedStop` |
| `StirOnce.asset` | StirOnce | SoupPot | ShortPress | 1 | 0.55 | OneShot | `StirOnce` | empty |
| `StirHold.asset` | StirHold | SoupPot | Hold | 2.0 | 2.25 | Sustained | `StirHoldStart` | `StirHoldStop` |
| `PanFlipOnce.asset` | PanFlipOnce | FryingPan | ShortPress | 1 | 0.60 | OneShot | `PanFlipOnce` | empty |
| `PanFlipHold.asset` | PanFlipHold | FryingPan | Hold | 2.0 | 2.25 | Sustained | `PanFlipHoldStart` | `PanFlipHoldStop` |

- [ ] **Step 3: Create a deterministic three-round demo recipe**

Author `DemoRecipe.asset`:

```text
Round 1: StirOnce -> ChopOnce
Round 2: ChopRepeated -> PanFlipOnce -> StirHold
Round 3: PanFlipHold -> ChopOnce -> StirOnce
stationChangeAllowance: 0.25 seconds
custom thresholds: disabled for all rounds
```

This sequence exercises all six operation definitions and both same-frame one-shot handoff and cross-station switching.

- [ ] **Step 4: Create visibly distinct clips on the visual child, never the station/character root**

All curves target relative path `CharacterVisual`. Required placeholder motion:

| Clip | Duration | Loop | Distinguishing motion |
|---|---:|---|---|
| `Idle` | 1.0 | yes | subtle Y breathing |
| `Cut_Once` | 0.35 | no | sharp Y down/up plus Z tilt |
| `Stir_Once` | 0.45 | no | left-right Z rotation arc |
| `PanFlip_Once` | 0.50 | no | strong Y hop plus backward Z tilt |
| each `*_Enter` | 0.15 | no | move from Idle into working pose |
| each `*_Loop` | 0.35–0.60 | yes | repeated cut, circular stir, or vertical pan-toss motion |
| each `*_Exit` | 0.15 | no | return from working pose to Idle |

Do not animate `SoupPotCharacter`, `CuttingBoardCharacter`, `FryingPanCharacter`, station roots, or device props. This prevents animations from breaking discrete station placement.

- [ ] **Step 5: Build the minimal Animator graph**

Parameters are triggers only:

```text
ChopOnce
StirOnce
PanFlipOnce
ChopRepeatedStart / ChopRepeatedStop
StirHoldStart / StirHoldStop
PanFlipHoldStart / PanFlipHoldStop
```

Graph:

```text
Entry -> Idle
Any State --ChopOnce--------> Cut_Once --------Has Exit Time------> Idle
Any State --StirOnce--------> Stir_Once -------Has Exit Time------> Idle
Any State --PanFlipOnce-----> PanFlip_Once ----Has Exit Time------> Idle
Any State --*Start----------> *_Enter --Exit Time--> *_Loop
*_Loop -----*Stop-----------> *_Exit  --Exit Time--> Idle
```

One-shot transitions must permit interruption by a later trigger, which is required when the next operation is entered immediately. Only `Idle` and the three `*_Loop` clips have Loop Time enabled. Do not add `Move_Side`, `Miss`, `Finish`, or `Celebrate` states.

- [ ] **Step 6: Validate the content assets**

Inspect the controller parameters/states via `manage_animation`, verify clip loop flags, and build runtime data from `DemoRecipe.asset`. Expected: six valid operations, three rounds of 2–3 operations, and no validation errors.

- [ ] **Step 7: Commit generated Unity assets**

```powershell
git add Assets/Cooking Assets/Animations
git commit -m "feat: add cooking content and placeholder animations"
```

---

### Task 5: Semantic scene hierarchy, wiring, and placeholder HUD

**Files:**

- Modify: `Assets/Scenes/SampleScene.unity`

**Interfaces:**

- Consumes: all scripts and assets from Tasks 1–4.
- Produces: one wired playable demo scene.

- [ ] **Step 1: Verify a clean checkpoint and inspect the live hierarchy**

Confirm `git status --short` is empty, Editor is not compiling or playing, and the active scene is `Assets/Scenes/SampleScene.unity`.

- [ ] **Step 2: Apply the exact hierarchy rename map through Unity MCP**

Preserve world transforms and existing renderer/collider/material settings:

```text
World                                      -> CookSceneRoot
CookSceneRoot/Main Camera                  -> MainCamera
CookSceneRoot/Directional Light            -> KeyLight
CookSceneRoot/Global Volume                -> GlobalPostProcess
CookSceneRoot/Kitchen                      -> Kitchen
Kitchen/CookingArea                        -> CookingGameplay
CookingGameplay/LeftStation                -> SoupPotStation
CookingGameplay/CenterStation              -> CuttingBoardStation
CookingGameplay/RightStation               -> FryingPanStation
SoupPotStation/Character                   -> SoupPotCharacter
CuttingBoardStation/Character              -> CuttingBoardCharacter
FryingPanStation/Character                 -> FryingPanCharacter
each Character/Capsule                     -> CharacterVisual
Kitchen/CookingStove                       -> KitchenProps
KitchenProps/Cube                          -> Counter
KitchenProps/Cook1                         -> SoupPot
KitchenProps/Cook2                         -> CuttingBoard
KitchenProps/Cook3                         -> FryingPan
```

The resulting gameplay branch must be:

```text
CookSceneRoot
├── MainCamera
├── KeyLight
├── GlobalPostProcess
└── Kitchen
    ├── CookingGameplay                 [CookingInputController]
    │   ├── SoupPotStation
    │   │   └── SoupPotCharacter        [Animator]
    │   │       └── CharacterVisual
    │   ├── CuttingBoardStation
    │   │   └── CuttingBoardCharacter   [Animator]
    │   │       └── CharacterVisual
    │   └── FryingPanStation
    │       └── FryingPanCharacter      [Animator]
    │           └── CharacterVisual
    └── KitchenProps
        ├── Counter
        ├── SoupPot
        ├── CuttingBoard
        └── FryingPan
```

- [ ] **Step 3: Add and wire presentation components**

Add `CookingAnimationPresenter` to `CookingGameplay`; assign each station root and character Animator. Assign `CookingCharacter.controller` to all three Animators. Assign `DemoRecipe.asset`, presenter, HUD, SoupPot starting station, and `0.75` result display seconds to `CookingInputController`.

- [ ] **Step 4: Create the placeholder uGUI hierarchy**

Create a screen-space overlay Canvas named `CookingHUD`, an EventSystem if missing, and:

```text
CookingHUD
├── OperationSequenceText
├── OperationProgressSlider
├── RecipeProgressSlider
├── GradeText
└── ResultText
```

Use built-in Arial, clear high-contrast colors, top-center operation order, operation progress below it, recipe progress at screen bottom, and centered grade/result text. Assign these components to `CookingHud`. This is functional placeholder UI, not final art.

- [ ] **Step 5: Save the scene and inspect all serialized references**

Save through `manage_scene(action="save")`. Re-read the hierarchy and relevant components. Expected: no missing script, all three station/Animator bindings assigned, recipe assigned, and only SoupPot station active before play.

- [ ] **Step 6: Commit the scene checkpoint**

```powershell
git add Assets/Scenes/SampleScene.unity Assets/Scenes/SampleScene.unity.meta
git commit -m "feat: wire cooking demo scene"
```

---

### Task 6: Full verification and handoff

**Files:**

- Modify only files required by defects found during verification.

**Interfaces:**

- Consumes: completed playable slice.
- Produces: evidence that code, assets, scene hierarchy, input, animation, and HUD work together.

- [ ] **Step 1: Run all EditMode tests**

Expected: every `Cook.Tests` test passes, including fixed order, progress preservation, four grade boundaries, Miss timeout, early success, and final failure.

- [ ] **Step 2: Run all PlayMode tests**

Expected: every `Cook.PlayModeTests` test passes, including exactly-one-station visibility, same-frame one-shot handoff, and sustained start/stop triggers.

- [ ] **Step 3: Check Console after domain reload**

Read errors and warnings. Expected: zero errors; fix only warnings introduced by this implementation.

- [ ] **Step 4: Perform the manual gameplay matrix**

In Play Mode verify:

```text
Left/Right: switch display instantly without Transform travel.
Wrong station + J: no operation progress; timer continues.
Short press: next operation becomes active immediately while old animation may still finish.
Repeated press: each J adds progress; stopping preserves it.
Hold: progress grows only while held at the correct station; release/change station preserves it.
Pan short press: one visible flip and no loop.
Pan hold: Enter -> looping continuous toss -> Exit.
Round timeout: Miss, zero awarded progress, then next authored round.
Good/Great/Excellent: correct progress multipliers.
100%: immediate success and remaining rounds skipped.
Rounds exhausted below 100%: failure.
```

- [ ] **Step 5: Capture visual verification**

Capture one Scene View screenshot showing the semantic layout and one Game View screenshot showing the operation sequence and both progress bars. Confirm the visual child moves while station/character root transforms remain fixed.

- [ ] **Step 6: Review the final diff and commit fixes**

Run `git diff --check`, `git status --short`, and inspect the final commit list. If verification required fixes, commit them as:

```powershell
git add <only verified fix files>
git commit -m "fix: complete cooking demo verification"
```

- [ ] **Step 7: Push and report**

Push `main`, then report test totals, manual matrix result, final hierarchy, screenshots, commits, and any intentionally retained legacy animation assets.

