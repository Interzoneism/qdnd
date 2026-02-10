### **1. Global Resolution Engine & Mathematical Models**

**1.1. Core Resolution Formula**
All non-deterministic checks (Attacks, Saving Throws, Skills) generally follow this resolution logic:


* 
**Success Condition:** .


* 
**Ability Modifier:** .


* **Proficiency Bonus:** Scales with *Character Level*, not Class Level.
* Levels 1-4: +2 | Levels 5-8: +3 | Levels 9-12: +4 .





**1.2. Advantage/Disadvantage State**

* 
**Advantage:** Roll two d20s, take the highest.


* 
**Disadvantage:** Roll two d20s, take the lowest.


* 
**Math equivalence:** Roughly equivalent to a +/- 3.3 to 5 modifier.



**1.3. Difficulty Class (DC) Calculation**
For spells and weapon actions, the DC imposed on a target is:


* 
**Weapon Actions:** Uses STR or DEX.


* 
**Spells:** Uses Class Spellcasting Ability (INT for Wizard, WIS for Cleric/Druid, CHA for Bard/Sorc/Warlock/Paladin) .



---

### **2. Entity State Machine & Attributes**

**2.1. Primary Attributes & Governance**

* 
**Strength (STR):** Jump distance (), Carry Weight, Shove distance, Melee Attacks (unless Finesse).


* 
**Dexterity (DEX):** Initiative (d4 + DEX), Armour Class (AC), Ranged/Finesse Attacks, Reflex Saves.


* 
**Constitution (CON):** HP/Level, Concentration Saves (Vital for sustaining spells).


* 
**Intelligence (INT):** Wizard Spellcasting, Knowledge Skills.


* 
**Wisdom (WIS):** Cleric/Druid/Ranger/Monk Spellcasting, Perception, Will Saves.


* 
**Charisma (CHA):** Bard/Paladin/Sorcerer/Warlock Spellcasting, Social Skills, Shop Prices.



**2.2. Resource Pools (Per Turn / Per Rest)**

* **Per Turn Resources:**
* 
**Action:** Main resource for Attacks/Spells.


* 
**Bonus Action:** Secondary resource (Jump, Shove, Potion, Off-hand Attack).


* 
**Reaction:** Off-turn conditional resource (Opportunity Attack, Counterspell).


* 
**Movement:** Distance pool (meters), replenishable at turn start.




* 
**Short Rest Resources:** Warlock Spell Slots, Ki Points, Superiority Dice, Channel Oath/Divinity, Action Surge, Bardic Inspiration (Lv5+).


* 
**Long Rest Resources:** Standard Spell Slots, Rage Charges, Hit Points, Sorcery Points, Arcane Recovery.



---

### **3. Class Mechanics & Special Resource Logic**

**3.1. Barbarian**

* **Unique Resource:** Rage Charges (Restores on Long Rest).
* **Mechanic (Rage):** Bonus Action. +2 Damage (Melee), Resistance to Physical Dmg, Adv on STR checks.


* **Mechanic (Reckless Attack):** Grant Adv on own attack; Enemy gains Adv on attacks vs Barbarian.


* **Subclass Logic:**
* *Berserker:* **Frenzy** (Bonus Action Attack). **Enraged Throw** (Prone on hit).


* 
*Wildheart:* **Bear Heart** (Resist all dmg except Psychic).





**3.2. Bard**

* **Unique Resource:** Bardic Inspiration (Die size scales d6  d10).
* **Mechanic:** Bonus Action. Adds die result to Ally Attack/Save/Check.


* **Subclass Logic:**
* 
*Lore:* **Cutting Words** (Reaction: Subtract die from Enemy roll).


* 
*Swords:* **Blade Flourish** (Expend die for extra Dmg + AC or Mobility).





**3.3. Cleric**

* **Unique Resource:** Channel Divinity (Restores on Short Rest).
* **Mechanic (Turn Undead):** Fear effect on Undead. Lv5: 4d6 Radiant Dmg.


* **Subclass Logic:**
* 
*Life:* Bonus Healing ().


* 
*Tempest:* **Destructive Wrath** (Max Dmg on Thunder/Lightning roll).


* 
*War:* **Guided Strike** (+10 to Hit).





**3.4. Druid**

* **Unique Resource:** Wild Shape Charges (2/Short Rest).
* **Mechanic:** Transform into beast; HP is separate buffer. Revert when HP=0.


* **Subclass Logic:**
* *Moon:* **Combat Wild Shape** (Bonus Action transform). Exclusive forms (Bear, Myrmidon).


* 
*Spores:* **Symbiotic Entity** (Temp HP + Necrotic Dmg on weapon hits).





**3.5. Fighter**

* **Mechanic (Action Surge):** Gain 1 additional Action. (Short Rest) .


* 
**Mechanic (Second Wind):** Bonus Action Self-Heal ().


* **Subclass Logic:**
* *Battle Master:* **Superiority Dice** (d8). Used for Manoeuvres (Trip, Disarm, Riposte).


* 
*Champion:* Crit threshold reduces by 1 (Crit on 19).





**3.6. Monk**

* **Unique Resource:** Ki Points (Level = Count; Short Rest).
* **Mechanics:**
* 
**Flurry of Blows:** 2 Unarmed Strikes (Bonus Action + 1 Ki).


* 
**Stunning Strike:** Melee Hit + 1 Ki + CON Save -> **Stunned**.


* 
**Step of the Wind:** Dash/Disengage as Bonus Action.





**3.7. Paladin**

* **Unique Resource:** Lay on Hands (Pool), Channel Oath.
* **Mechanic (Divine Smite):** Free Action on Hit. Expend Slot for  Radiant Dmg.


* 
**Mechanic (Aura of Protection):** Add CHA mod to all Saves for self/allies (3m radius).



**3.8. Rogue**

* **Mechanic (Sneak Attack):** Once per turn. Extra Dmg (scales 1d6 per 2 lvls) if Advantage OR Ally within 1.5m.


* 
**Mechanic (Cunning Action):** Dash, Disengage, Hide as **Bonus Action**.


* **Subclass Logic:**
* 
*Thief:* **Fast Hands** (Gain 2nd Bonus Action per turn).


* 
*Assassin:* Auto-Crit vs **Surprised** targets.





**3.9. Sorcerer**

* **Unique Resource:** Sorcery Points (SP).
* 
**Mechanic (Metamagic):** Alter spell properties (e.g., **Quickened**: Cast Action spell as Bonus Action; **Twinned**: Target 2 entities).


* 
**Mechanic:** Convert Slots  SP.



**3.10. Warlock**

* 
**Unique Resource:** Pact Slots (Always max level, Short Rest recharge).


* **Mechanic (Eldritch Blast):** Force cantrip. Scales beams at Lv5/10. Invocation **Agonizing Blast** adds CHA to dmg.



---

### **4. Action Economy Registry**

**4.1. Standard Actions**
| Action | Cost | Logic / Effect | Source |
| :--- | :--- | :--- | :--- |
| **Main Hand Attack** | Action |  |  |
| **Dash** | Action | Add Base Speed to Current Movement (Does not multiply) |  |
| **Disengage** | Action | Prevent Opportunity Attacks for turn |  |
| **Throw** | Action | Range 18m. Dmg based on weight. Can throw Potions/Creatures. |  |
| **Hide** | Action | Stealth Check vs Perception. Requires "Out of Sight". |  |
| **Help** | Action | Removes: Downed, Prone, Burning, Ensnared, Sleep. Range 1.5m. |  |
| **Shove** | Bonus | Athletics vs Athletics/Acrobatics. Displacement depends on Weight/STR. |  |
| **Jump** | Bonus | Cost 3m Move. Dist: . |  |
| **Dip** | Bonus | Add surface element (Fire/Poison) to weapon (+1d4 dmg). |  |

**4.2. Weapon Actions (Short Rest Recharge)**
*Req: Weapon Proficiency*
| Action | Weapon Type | Effect / Condition | Save | Source |
| :--- | :--- | :--- | :--- | :--- |
| **Cleave** | Axe/Halberd | Cone Attack (3 targets), Half Dmg | None |  |
| **Lacerate** | Sword/Glaive | Inflict **Bleeding** | CON |  |
| **Smash** | Mace/Hammer | Inflict **Dazed** | CON |  |
| **Topple** | Staff/Spear | Inflict **Prone** | DEX |  |
| **Tenacity** | Mace/Hammer | Reaction: Deal STR Mod dmg on **Miss** | None |  |
| **Pommel Strike** | Sword | Bonus Action Attack + **Dazed** | CON |  |

---

### **5. Feat Registry**

*Triggered at Levels 4, 8, 12 (Fighter 6, Rogue 10).*

**5.1. Meta/High-Impact Feats**

* **Great Weapon Master:**
* Toggle: -5 Attack / +10 Damage.
* Passive: Crit/Kill grants Bonus Action Attack.




* **Sharpshooter:**
* Toggle: -5 Attack / +10 Damage.
* Passive: Ignore "Low Ground" penalty (-2 hit).




* **Tavern Brawler:**
* Logic: Add STR Mod **twice** to Attack & Damage (Unarmed/Throw).




* **War Caster:**
* Passive: Advantage on CON Saves (Concentration).
* Reaction: Cast *Shocking Grasp* as Opportunity Attack.




* **Alert:**
* Passive: +5 Initiative. Cannot be **Surprised**.





**5.2. Utility & Mobility Feats**

* **Mobile:** Dash ignores difficult terrain. Melee hit prevents Opportunity Attack from target.


* **Sentinel:** Reaction attack if ally hit. Opportunity Attack sets enemy speed to 0.


* 
**Resilient:** +1 Stat, Proficiency in that Stat's Save (Vital for CON/Concentration).


* **Spell Sniper:** Crit Threshold -1 for Spells. Learn 1 Cantrip.


* **Lucky:** 3 Points/Day. Roll extra d20 (Advantage) or force enemy reroll.



---

### **6. Spellcasting & Environmental Physics**

**6.1. Spell Mechanics**

* 
**Upcasting:** Using a slot level  base spell level increases Dice (+1 die) or Targets (+1 target).


* **Concentration:** Single active slot. Damage forces CON Save (DC 10 or Half Dmg). Failure ends spell.


* 
**Rituals:** No slot cost if cast out of combat (e.g., *Longstrider*).



**6.2. Surface Interactions (The Larian Engine)**

* **Water Surface:**
* Created by: *Create Water*, Ice melting.
* Effect: Applies **Wet** (Vulnerable to Lightning/Cold; Resistant to Fire).




* **Ice Surface:**
* Created by: *Ice Knife*, *Sleet Storm*.
* Effect: **Difficult Terrain**. Move check DEX Save  **Prone**.




* **Fire Surface:**
* Created by: Fire dmg + Grease/Oil/Web.
* Effect: **Burning** (DoT).




* **Electrified Water:**
* Created by: Lightning + Water.
* Effect: 1d4 Lightning Dmg + **Shocked**.





**6.3. Spell Registry (Mechanic Highlights)**
| Spell | Level | Effect / Condition | Interaction Note | Source |
| :--- | :--- | :--- | :--- | :--- |
| **Magic Missile** | 1 | Force Dmg. **Auto-Hit**. | Breaks Conc (3 hits = 3 saves). |  |
| **Bless** | 1 | +1d4 Attack/Save (3 targets). | Conc. |  |
| **Create Water** | 1 | Rain (Area). Applies **Wet**. | Setup for Lightning Dmg (2x). |  |
| **Shield** | 1 | Reaction: +5 AC, Immune to Magic Missile. | Lasts 1 round. |  |
| **Cloud of Daggers** | 2 | Area Slash Dmg. No Save. | Ticks on Cast AND Turn Start. |  |
| **Hold Person** | 2 | **Paralyzed** (Humanoid). | Auto-Crits from <3m. |  |
| **Spike Growth** | 2 | Area: 2d4 Dmg per 1.5m moved. | No Save. Hard movement counter. |  |
| **Fireball** | 3 | 8d6 Fire (Area). DEX Save. | Ignites surfaces. |  |
| **Haste** | 3 | +Action, +2 AC, Dbl Speed. | End: **Lethargic** (Skip turn). |  |
| **Spirit Guardians** | 3 | Aura: 3d8 Rad/Necro. Halves Speed. | Triggers on Enter/Turn Start. |  |
| **Counterspell** | 3 | Reaction: Negate Spell. | Check req if Slot < Spell Lvl. |  |
| **Globe of Invulnerability** | 6 | Dome: **Immune to All Dmg**. | Entities inside are immune. |  |

---

### **7. Condition & Status Registry**

**7.1. Hard Control (Turn Loss/Denial)**

* **Paralyzed:** Incapacitated. Auto-Fail STR/DEX Saves. **Auto-Crit** from <3m.


* **Stunned:** Incapacitated. Auto-Fail STR/DEX Saves. Advantage to hit target.


* **Sleep:** Incapacitated. Breaks on Dmg/Help. **Auto-Crit** from <1.5m.


* **Hypnotised:** Incapacitated. Ends on Dmg.



**7.2. Soft Control (Debuffs)**

* **Prone:** Disadvantage on Attacks/DEX Saves. Melee attackers have **Advantage**. Costs half move to stand.


* **Dazed:** No Reactions. No DEX to AC. Disadvantage WIS Saves.


* **Blinded:** Disadvantage on Attacks. Range limited to 3m. Attacks against have Advantage.


* 
**Threatened:** Disadvantage on Ranged/Spell Attacks (Enemy within 1.5m).



**7.3. Damage & Item Conditions**

* **Wet:** Vulnerable to Cold/Lightning. Resistant to Fire. Prevents Burning.


* **Lightning Charges:** +1 Hit/Dmg. 5 stacks = 1d8 Burst. Decays per turn.


* 
**Radiating Orb:** -1 Attack Roll per stack.


* **Reverberation:** -1 STR/DEX/CON Save per stack. 5 stacks = Dmg + **Prone**.


* **Bleeding:** CON Saves Disadvantage. DoT.