# **The "Dungeon Master" Architect: A Guide to BG3-Style Combat AI**

In *Baldur's Gate 3*, the AI does not just "attack the player." It plays a system-heavy simulation. To replicate this, your AI cannot simply be a Finite State Machine (e.g., `Idle` -> `Chase` -> `Attack`). It needs to be a **Utility-Based Planner** that evaluates the entire board state, including environmental hazards, verticality, and resource economy.

## **1. Core Architecture: The "Think-Act" Loop**

For a prototype, avoid complex Hierarchical Task Networks (HTN) initially. Instead, use a **Utility AI (Scoring System)** combined with **Influence Maps**.

### **The Decision Pipeline**

1. **Query State:** Gather all valid targets, interactable objects (barrels, levers), and positioning data.
2. **Generate Candidate Actions:** List every possible move (Move+Attack A, Shove B, Throw Potion at C).
3. **Score Actions:** Apply a "Desirability Score" to each candidate based on curves/weights.
4. **Execute Best:** Pick the highest-scoring action (with slight RNG fuzzing to prevent predictability).

---

## **2. The Brain: Scoring Criteria (Utility Functions)**

This is the secret sauce of BG3. The AI feels smart because it weighs multiple factors before acting.

### **A. Target Selection (The "Squishy" Bias)**

BG3 AI is notorious for tunneling on weak characters.

* **Formula:** `Score = (Base Aggro) + (Hit Chance * Damage Potential) - (Defensive Cost)`
* **Key Factors to Weight:**
* **Armor Class (AC):** Inverse relationship. Lower AC targets get significantly higher scores.
* **Concentration:** If a target is concentrating on a spell, apply a huge targeted multiplier (e.g., `1.5x score`).
* **Status Effect Synergy:** If target is *Wet*, increase score for *Lightning/Cold* attacks.
* **"Downed" State:** In Hard modes (Tactician), give high scores to attacking 0 HP enemies to force failed Death Saves. In Normal modes, lower this score to be "merciful."



### **B. Positioning (Influence Maps)**

Don't just move to range. Move to *advantage*.

* **High Ground:** Assign a flat bonus to any tile `Z > Target Z`.
* **Backstab/Flanking:** Bonus score for tiles located in the 180° arc behind a target (if your game uses facing).
* **Hazard Avoidance:** Negative score for tiles containing fire, acid, or queued AoE indicators.
* **Opportunity Attack Avoidance:** Huge negative score for movement paths that trigger reaction attacks.

### **C. Environmental Interaction (The Larian Signature)**

The AI must view the world as a weapon.

* **Shove Logic:** Check `Distance to Ledge`. If `Target Mass < Shove Capacity` AND `Fall Damage > Threshold`, make "Shove" a high-priority action.
* **Barrelmancy:** Scan for objects with the `Explosive` tag. If `(Explosion Radius hits Enemies > Allies)`, score highly.
* **Surface Creation:** If target is standing in Water, score `Shocking Grasp` higher than `Firebolt`.

---

## **3. Implementation Guide: The "Sensors"**

Your AI needs specific data structures to "see" the game like BG3 does.

### **The Perception Grid**

Implement a grid or navmesh that stores more than just walkability. Each node should contain:

* **Surface Type:** (None, Fire, Water, Ice, Web)
* **Light Level:** (Obscured, Clear) - Critical for hit chance calculation.
* **Occupancy:** (Who is standing here?)
* **Height:** (Z-coordinate)

### **The Resource Monitor**

The AI must respect the Action Economy (Action, Bonus Action, Movement).

* **Optimization:** If the AI uses its Action to attack but has a Bonus Action left, it should scan specifically for Bonus Action skills (e.g., Potion, Shove, Dip Weapon).
* **Fallbacks:** If no useful Action is available, always default to a defensive stance (e.g., "Dash" to safety or "Hide").

---

## **4. Specific Behavior Routines (Pseudocode)**

### **Routine 1: The "Shove" Check (high priority)**

```python
def CalculateShoveUtility(me, target):
    if distance(me, target) > melee_range:
        return 0
    
    # Raycast behind target to find drop
    landing_spot = FindKnockbackSpot(target, push_distance)
    fall_damage = CalculateFallDamage(target.z, landing_spot.z)
    
    if fall_damage > 0 or landing_spot.is_lava:
        return 100 + fall_damage # Massive priority
    return 0 # Don't just shove for no reason

```

### **Routine 2: The "Cluster" Punisher (AoE)**

```python
def EvaluateFireball(me, spell):
    best_location = None
    max_score = -1
    
    for valid_point in range(spell.range):
        enemies_hit = CountEnemiesInRadius(valid_point, spell.radius)
        allies_hit = CountAlliesInRadius(valid_point, spell.radius)
        
        # BG3 AI doesn't mind hitting allies if it hits MORE enemies
        score = (enemies_hit * 2) - (allies_hit * 1.5)
        
        if score > max_score:
            max_score = score
            best_location = valid_point
            
    return max_score, best_location

```

---

## **5. Prototyping Roadmap: Step-by-Step**

If you are building this from scratch, follow this order to avoid complexity bloat.

1. **Phase 1: The Brawler (Base Layer)**
* Implement "Move to closest enemy" and "Attack".
* Add the **Action Point** system (Movement vs. Action).
* *Result:* Zombies that run at you and hit you.


2. **Phase 2: The Tactician (Utility Layer)**
* Add **Hit Chance** calculation to the decision logic (AI prefers high % shots).
* Add **AC Scanning** (AI prefers targeting low AC).
* *Result:* Enemies that gang up on your Wizard.


3. **Phase 3: The Physicist (Environmental Layer)**
* Add **Surface checks** (Walking on ice causes slip).
* Add **Shove/Throw**.
* *Result:* Complex, emergent gameplay where positioning matters.



## **6. Common Pitfalls to Avoid**

* **The "Kite" Loop:** Don't let AI run away, shoot, run away indefinitely. Add a "Bravery" weight that forces engagement unless they are critically low on HP.
* **Analysis Paralysis:** In a prototype, limit the depth of the AI's search. Don't let it simulate 3 turns ahead. Just simulate "Best outcome for *this* turn."
* **Cheating:** Players hate when AI knows things it shouldn't (like invisible units). Ensure your AI's "Sensors" filter out Hidden/Invisible targets unless a `Detect Invisibility` check is passed.