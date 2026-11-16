# Gameplay Features Overview

This document details the core gameplay systems implemented for **Gipsy Danger (Player)**, **Leatherback (Boss AI)**, and the **Combat HUD & Environment**.

---

## 1. Player Mechanics (`SimpleJaegerController`)

### Core Stats & Stamina / Exhaustion
- **Hull (Health):** `1000` max HP.
- **Stamina:** `125` max stamina, regenerating at `24/s` after a `0.85s` delay (`1.4x` regen while Overcharged, `0.45x` while Blocking).
- **Exhaustion:** Depleting stamina to `0` triggers **Exhausted** state, disabling attacks and blocking until stamina recovers to **35%** (`43.75`).

### Locomotion & Procedural Leg IK
- **Walk / Sprint (`W/A/S/D` + `Shift`):** `12.75 m/s` walk speed, `33.75 m/s` sprint speed (`Shift` toggles run mode by default; disabled when moving backward, blocking, emoting, or heavy attacking).
- **Omnidirectional Strafe & Leg IK:** Lateral movement (`0.85x` speed) applies pelvis redirection (up to `65°`) and 2-bone leg IK for heavy mechanical sidestepping, while Jaeger yaw smoothly aligns to the camera look angle.
- **Ocean Stomp (`Space`):** `1.2s` cooldown. Slams into the ocean to emit an enlarged water shockwave (`1.8x` radius, `2.5x` strength), heavy water splash VFX, and stomp camera shake.

### Combat & Attacks
- **Light Attacks (`Left Click`):**
  - Cycles through a randomized, non-repeating 3-hit combo (`Punch1`, `Punch2`, `Punch3`).
  - **Damage / Range:** `62.5` damage, `18m` range, `0.93s` cooldown, `16` stamina cost, `0.70x` movement speed during swing.
  - **Combo Window:** Opens right as the fist lands (`~0.46s–0.51s`), allowing fluid chaining within the `2.4s` reset window.
- **Heavy Attacks (`Right Click`):**
  - **Standing Heavy (`HeavyAttack`):** `137.5` damage, `22m` range, `1.20s` cooldown, `32` stamina cost, `0.30x` movement speed.
  - **Sprint / Rocket Lunge Heavy (`HeavyAttackRun`):** Triggered by heavy attacking while sprinting forward. Fires rocket thrusters for a decaying forward lunge (`32+ m/s` initial burst) and grants **Hyper-Armor (Uninterruptible)**—incoming Kaiju hits deal damage without staggering, cancelling the swing, or locking out attacks.
- **Combat Stance:** Automatically activates on attacking or blocking and persists for `5.0s` before relaxing to normal idle.

### Defense & Parry System
- **Guard / Block (`Hold F`):**
  - Reduces incoming damage by **80%** and knockback to **25%**, at `0.35x` movement speed.
  - Consumes `16` stamina on light hits and `28.8` stamina on heavy hits (cannot block while Exhausted).
- **Perfect Parry (`F` within `0.18s` of impact):**
  - Raising block within the `0.18s` parry window completely deflects the strike: **0 damage** and **0 knockback**.
  - Restores **+15% max stamina** and **immediately clears Exhaustion**.
  - Triggers `KaijuAI.TriggerParryStagger()`, cancelling the Kaiju's active attack, knocking it back with recoil, and staggering it for **`2.2s`** (without dealing direct parry damage).
  - Fires parry clang audio, radiant sparks, heavy camera shake, and a golden HUD parry cue.

### Reactor Overcharge / Emote (`Q`)
- Locks movement for the `7.334s` battle-roar emote, then grants a **`15s` Power-Up**:
  - **+30%** attack animation speed
  - **+25%** faster cooldowns & **+25%** attack damage
  - **-30%** stamina cost & **1.4x** stamina regeneration
  - Activates global `_GlobalPowerUp` armor/turbine glow.

### Damage Reaction
- Unblocked hits (outside of Sprint-Attack Hyper-Armor) cancel active strikes, lock attacks for `0.15s`, and play directional staggers (`HitLeft` / `HitRight`) based on the impact side.

---

## 2. Boss AI (`KaijuAI` — Category IV Leatherback)

### Stats & State Machine
- **Health & Poise:** `4200` max HP, `140` max Poise (regenerates at `22/s` after a `2.0s` delay).
- **AI States (`AIState`):** `Idle`, `ChasingAndStrafing`, `SizingUp`, `Retreating`, `EmpRoar`, `PhaseTransition`, `Attacking`, `Blocking`, `Staggered`, `Dead`.
- **Tactical Positioning:** Circle-strafes (`SizingUp`), steps back if crowded inside `15.8m`, sprints to close gaps beyond `20.5m`, and uses procedural head/torso lock-on (`24°` max yaw) with 2-bone lateral leg IK.

### 3 Escalating Combat Phases
1. **Phase I — Apex Stalker (`100%–65%` HP):**
   - Uses `LeftHook`, `RightHook`, `DoubleHookCombo`, and `HeavySlam`, plus periodic Bio-EMP Roars (`18s` buff, `20s` cooldown).
2. **Phase II — Bio-EMP Overdrive (`<= 65%` HP):**
   - Disengages with a backward sprint, unleashes an EMP discharge blast into the ocean, unlocks `TripleBrawlerCombo`, and immediately charges in with `SprintChargeSlam`.
3. **Phase III — Category IV Berserker (`<= 30%` HP):**
   - Heals **`+450` HP**, activates **permanent Bio-EMP Overdrive** (faster walk/sprint/strafe speeds), and gains **20% damage reduction** (`0.80x` incoming damage).

### Predictive Guard, Guard Break & Combo-Breaker
- **Anti-Spam Forearm Block:** `NotifyPlayerAttackStarted` tracks player attack frequency within a `2.6s` window. Base block chance scales by phase (`38%` P1, `52%` P2, `68%` P3) plus `+26%` per consecutive swing (guaranteed at 4+ attacks).
- **Armored Counter:** Light attacks hitting Leatherback's guard deal **88% reduced damage** (`blockDamageReduction = 0.88`) and queue an immediate counterattack.
- **Guard Break:** Heavy attacks (`>= 95` damage or heavy variants `4`/`5`) shatter Leatherback's block and force `StaggerUp`.
- **Directional Staggers & Combo-Breaker:** Player hooks stagger Leatherback directionally (`StaggerRight`, `StaggerLeft`, `StaggerUp`). Leatherback allows a full `1 -> 2 -> 3` (`Right -> Left -> Uppercut`) chain before triggering a **Combo-Breaker** (`65%` fast block / `35%` sprint retreat) to prevent infinite stunlocks.

---

## 3. Combat HUD & Atmosphere (`JaegerHUD` & World Systems)

- **Top-Center Boss Bar:** Displays Leatherback's current Phase Title, Tactical Status, HP, and Poise sub-bar, alongside the combat timer and notification banners.
- **Bottom-Left Telemetry:** Hull integrity bar, Stamina bar (with Exhausted state), and Overcharge timer bar.
- **Bottom-Right Combat Matrix:** Live readouts for Combat Stance, Defense Matrix (Guard / Parry), and Ocean Depth telemetry.
- **Screen & Camera Feedback:** Directional edge-flash overlays for hits/blocks/parries (`impactFlashGroup`), holographic center reticle (`UIHoloReticleGraphic`), heavy impact camera trauma, and dynamic ocean wave/splash emission on every footstep, stomp, and slam.
