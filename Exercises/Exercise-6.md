# Exercise 6: Media Player — Play/Pause/Seek with the State Pattern

## Objective

Implement a media player whose behavior (play, pause, stop, seek, fast-forward, rewind) changes based on its **current state**, using the **State pattern**.  
You will:

- Define a **state interface** and multiple **singleton state classes**.
    
- Use a **context class** (`Player`) that delegates all behavior to the active state.
    
- Manage **state transitions** through a unified helper method.
    
- Implement **side effects** (speed changes, position resets) on `Enter`/`Exit` methods.
    
- Avoid re-creating state objects — use a static `Instance` property per state.
    


---

## Scenario

You’re building a simple audio/video player that supports:

- States: **Stopped**, **Playing**, **Paused**, **FastForwarding**, **Rewinding**.
    
- Commands: `Play`, `Pause`, `Stop`, `Seek(toPosition)`, `FastForward()`, `Rewind()`.
    
- Player model data: `CurrentPosition`, `Duration`, `PlaybackSpeed`.
    

Behavior examples:

- From **Stopped**, `Play()` starts playback at `CurrentPosition` (0 if never started).
    
- From **Playing**, `Pause()` freezes time but leaves position; `Stop()` resets `CurrentPosition` to 0.
    
- `Seek()` while **Playing/Paused** jumps to a valid position; while **Stopped** either resumes or stays stopped (your design—document it).
    
- **FastForwarding** or **Rewinding** modifies `PlaybackSpeed` and changes how `Seek(delta)` applies (e.g., ticks forward/back).
    

---

## Step-by-Step Instructions

### Step 1 — Define the domain model

Decide on a minimal player model:

- `Duration` (TimeSpan): total media length.
    
- `CurrentPosition` (TimeSpan): playback position.
    
- `PlaybackSpeed` (double): e.g., `1.0` for normal, `2.0` for 2x, `0.0` when paused.

Document (`comment`) any assumptions (e.g., `0 ≤ CurrentPosition ≤ Duration`).


**Rules**:

- Position must always be between `0` and `Duration`.
    
- Setting beyond limits should **clamp** to valid range.
    

---

### Step 2 — Design the State contract

Create an interface (e.g., `IPlayerState`) declaring **only the operations that change with state**, for example:

- `void Play(Player ctx)`
    
- `void Pause(Player ctx)`
    
- `void Stop(Player ctx)`
    
- `void Seek(Player ctx, TimeSpan position)`
    
- `void FastForward(Player ctx)` / `void Rewind(Player ctx)`
    
- `void Enter(Player ctx)` and `void Exit(Player ctx)` for transition side effects (set speed, toggle flags).
    

**Guidelines**

- All methods take a `Player` context parameter.
    
- Methods **do not return values**; they **manipulate the context** or transition state.
    
- The `Enter` and `Exit` methods handle side effects like changing playback speed.
    
- Invalid actions should be clearly defined (e.g., throw `InvalidOperationException`).
    

---

### Step 3 — Implement the Context (Player)

Create a `Player` class that:

- Holds a reference to the current `IPlayerState` (`_state`).
    
- Exposes the public API (`Play`, `Pause`, `Stop`, `Seek`, `FastForward`, `Rewind`) that **delegate** to `_state`.
    
- Provides **transition helpers**, e.g., `TransitionTo(IPlayerState next)`that:
    
    - Calls `Exit()` on the old state.
        
    - Switches to the new state.
        
    - Calls `Enter()` on the new state.
    
- Owns core data (`CurrentPosition`, `Duration`, `PlaybackSpeed`).
    
- Does **not** branch on concrete state types (no `if/stateName` or `switch` on type).
    
- Add a `Tick(TimeSpan delta)` method to simulate playback progression:
    
    - Advance or rewind position by `PlaybackSpeed * delta`.
        
    - Clamp within `[0, Duration]`.
        
    - Auto-stop at start or end (optional but recommended).
        

---

### Step 4 — Implement baseline states

You will create classes for each state that all implement `IPlayerState`.

Each state must:

- Have a **private constructor**.
    
- Expose a static singleton `Instance` property.
    
- Have no internal mutable data (so it’s safe to reuse).
    

1. **StoppedState**
    
	- `Enter`: set `PlaybackSpeed = 0`, ensure `CurrentPosition` is clamped (usually 0).
	    
	- `Play`: transition to **Playing** (optionally start from 0).
	    
	- `Seek`: allowed? Choose and document: either set position and stay **Stopped**, or transition to **Paused**.
	    
	- `Pause`: often illegal; define your rule.
	    

2. **PlayingState**
    
	- `Enter`: `PlaybackSpeed = 1.0`.
	    
	- `Pause`: transition to **Paused** (keep position).
	    
	- `Stop`: set `CurrentPosition = 0` and transition to **Stopped**.
	    
	- `Seek`: clamp to `[0, Duration]`; remain **Playing**.
	    
	- `FastForward`/`Rewind`: transition to speed states (see Step 5).
	    

3. **PausedState**
    
	- `Enter`: `PlaybackSpeed = 0`.
	    
	- `Play`: resume to **Playing**.
	    
	- `Stop`: reset to 0 → **Stopped**.
	    
	- `Seek`: allowed, remains **Paused**.
	    

**Acceptance (baseline)**

- `Play→Pause→Play` resumes from the same position.
    
- `Stop` resets `CurrentPosition` to 0.
    
- Illegal actions behave as documented.
    

---

### Step 5 — Advanced states 

- **FastForwardingState**
    
    - `Enter`: `PlaybackSpeed = 2.0` (or configurable).
        
    - `Play`: drop back to **Playing** (1.0).
        
    - `Pause` or `Stop`: behave as expected.
        
    - `Seek(ctx, pos)`: clamp; remain FF.
        
    - `Rewind()`: switch to **Rewinding**.
        
- **RewindingState**
    
    - `Enter`: `PlaybackSpeed = -2.0`.
        
    - `Play`: → **Playing** (speed 1.0).
        
    - `Seek` clamps; remain RW.
        

---

### Step 6 — Time progression (simple simulation)

You don’t need a real timer. Provide a method like:

- `Tick(TimeSpan delta)`
    
    - If `PlaybackSpeed > 0`, advance `CurrentPosition += PlaybackSpeed * delta`.
        
    - If `< 0`, rewind.
        
    - Clamp to `[0, Duration]`; reaching 0 or end may auto-transition:
        
        - At `Duration`: auto-`Stop()` or stay **Paused** (choose and document).
            
- Keep `Tick` in the **context**; states set `PlaybackSpeed` but don’t move time directly.
    

- Optionally you can try to implement a real simulated timer

---

### Step 7 — Demonstration

Write a small demo sequence that:

1. Creates a player with a `Duration`.
    
2. Shows transitions: `Play → Tick → Pause → Seek → Play → FastForward → Play → Stop`.
    
3. Prints the current state name, `CurrentPosition`, `PlaybackSpeed` after each command.
    

---
## Acceptance Criteria (overall)

- Adding a new state **does not** require modifying existing state methods (beyond wiring transitions where needed).
    
- The `Player` context has **no concrete state logic**; it delegates calls to `_state`.
    
- Position/time logic is clamped and consistent; `Tick` respects `PlaybackSpeed`.
    
- Illegal actions are handled per your published policy.
    
- Tests cover happy paths, edges, and transitions.
    

---

## Pitfalls to Avoid

- **God-context**: Don’t put state-specific rules in `Player`; push them into state classes.
    
- **Leaky transitions**: Always use a single `TransitionTo` method that calls `Exit/Enter`.
    
- **Unclamped seeks/time**: Always clamp to valid ranges.
    
- **Hidden side effects**: Keep state methods deterministic and explicit about transitions and speed changes.
    
