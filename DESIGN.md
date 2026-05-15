# LoneFighter — Design Document

## 1. Vision & Player Fantasy

LoneFighter is a five-minute, single-thumb power fantasy. You are the last operative dropped into a closing arena: outnumbered from the first second, gradually transformed from a brittle gunslinger into a walking storm of bullets, blades, and lightning. The thrill is the curve — minute one is tight and worried, minute five is laughing-out-loud absurd, and the gap between the two is paved with thirty good decisions you made at level-up modals.

The vibe is *expensive arcade*. Every kill pops with HDR particles and a snap of screen shake; every level-up freezes the frame for a beat and unfurls three choices like trading cards. The 120 Hz target isn't a spec line — it's the texture of the game. Bullets feel like silk, dodges feel like instinct, and the chaos of three hundred enemies on screen reads cleanly because each frame is half as old as the last one.

LoneFighter respects the player's commute. A full run is exactly five minutes — start it on the elevator, finish it before your coffee cools. Portrait orientation, one virtual stick, zero menus between tap-to-play and bullets-in-the-air. Death is cheap, retry is instant, and the meta layer (post-slice) will be measured in seconds, not hours.

## 2. Core Gameplay Loop

1. **Move.** Drag the on-screen stick; the camera follows you, enemies pour in from the off-screen ring.
2. **Auto-fight.** Equipped weapons fire on their own cooldowns at the nearest valid target. The player chooses positioning, not triggers.
3. **Harvest.** Killed enemies drop XP gems. Walking into the magnet radius sucks them in; collecting them fills the XP bar.
4. **Choose.** Every level-up pauses the run and surfaces three upgrades from the pool. Pick one — stats up, new weapon, new defensive layer.
5. **Survive.** The wave script ramps spawn rate, enemy variety, and aggression. Reach 300 seconds alive (clear the Warden at ~270s) and the run wins.

## 3. Run Shape — 60-Second Beats

**0:00–1:00 — Calm intro.** Pure Grunts at ~1/sec, low contact damage. The player learns the stick, watches the first XP gems pop, hits level 2–3, picks their first upgrade. Density is intentionally thin so the level-up modal feels like a *gift* rather than a tax. Soundtrack is mid-tempo; the screen is mostly empty.

**1:00–2:00 — Ramp.** Grunt rate doubles, the first Runners arrive at 60s and quickly outpace the player's walking speed — kiting becomes a real verb. Spitters join at 90s and force the player to keep moving instead of camping a corner. Level 5–7 hits here, which is when the second weapon slot typically fills. Music adds a layer.

**2:00–3:00 — Mid-pressure.** Tanks roll in at 150s, soaking damage and acting as moving terrain that splits the swarm. The on-screen enemy count crosses 60 and the player has to start *reading* the field — using Tanks as cover, peeling Runners off, popping Spitters before their volleys land. Three upgrades deep, the build identity starts to set: ranged spam, melee orbital, or chain-shock.

**3:00–4:00 — Climax windup.** Dashers at 180s introduce telegraphed lunges — the first enemy that punishes standing still in a bad spot. Bombers at 210s explode on death, forcing the player to think about *where* a kill happens, not just *that* it happens. The arena feels full; HP rarely sits at max. Level 10+ unlocks high-impact upgrades and the build comes online.

**4:00–5:00 — Climax.** Full roster mixed at maximum spawn rate, ~6 enemies/sec. The Warden mini-boss enters at 270s while regular spawns continue. The kill-feed is constant, particles never stop, the player is one weapon-firing avatar at the center of a five-color hurricane. Survive 30 more seconds → victory screen.

## 4. Upgrade Philosophy

The upgrade pool is split roughly **50% offensive / 30% defensive / 20% utility** — biased toward offense because a survivor run is fundamentally a damage-race against the spawn curve, but defense and utility have to exist or the meta collapses into "always pick damage."

- **Offensive (≈50%, 7–8 of 15).** Weapon damage, cooldown, projectile speed, pierce, and new-weapon grants. These are the build-defining picks. They include the weapon-grant upgrades for Spread Shotgun, Orbital Blade, and Lightning Chain, which are the most exciting reveal moments in a run.
- **Defensive (≈30%, 4–5 of 15).** Max HP, move speed (defensive in a kiting game). These are the "I'm dying, give me a lifeline" picks. They cap lower than offensive stacks because too much defense flattens the curve.
- **Utility (≈20%, 2–3 of 15).** Magnet radius, XP multiplier. These amplify *future* picks rather than the current state — they're high-skill-ceiling choices that pay off across a run.

Every upgrade can stack 3–5 times. Weapon-grant upgrades are single-stack and removed from the pool once taken. Stack curves are linear in magnitude but multiplicative in feel — +25% damage at stack 5 becomes meaningful only because cooldown and pierce also stacked.

## 5. Difficulty Curve Theory

Minute five feels *earned* because three orthogonal pressures all peak at once:

1. **Spawn rate** scales from ~1/sec to ~6/sec — a 6× raw load.
2. **Enemy mix** shifts from one archetype (Grunt) to seven (Grunt, Runner, Tank, Spitter, Dasher, Bomber, Warden). Each archetype demands a different reaction, so the player's *attention budget* shrinks even when DPS scales linearly.
3. **The Warden mini-boss** at 270s is the explicit gate. It has more HP than the player can chew through without their level-10+ build, which means the run rewards players who picked at least 2 offensive synergies.

Crucially the player's power *also* scales — by minute five they'll be roughly level 14–18, with 2–3 weapons and ~20 upgrade stacks. The fantasy is that the player's growth and the wave's growth race each other, and the player wins by *seconds*. Hit the curve right and most runs end with HP < 30% and the timer at 4:58, which is exactly where you want the camera to fade out.

The contract: a *good* player wins ~70% of runs. A *bad* player dies somewhere in the 2:30–3:30 valley (Tank+Spitter combo), learns one thing, and tries again.

## 6. Future-Direction Wishlist

- **Meta-progression**: a small persistent currency dropped at end-of-run, spent on permanent +1% baselines and starting-weapon variants.
- **Character roster**: 4–6 starting characters with distinct base weapons (e.g. a Pistol main, a Shotgun main, a melee Orbital main).
- **Stage variants**: 3 arenas with different hazards — open field, narrow corridors, lava pools.
- **Elite enemies**: tinted, super-stat enemies that drop guaranteed rare upgrades.
- **Synergy items**: "evolved" weapons unlocked when a weapon hits max stacks alongside a specific passive (Vampire Survivors-style).
- **Daily seed**: shared global wave-config and upgrade-pool for leaderboard competition.
- **Boss rush mode**: 2-minute mode that throws three mini-bosses back to back.
- **On-screen pet/familiar**: an orbital companion that fires its own slow weapon, ties into the Orbital Blade theme.
- **Cosmetic skins**: per-character outfit packs as the only IAP — purely visual.
- **Replay export**: 30s GIF/video share of the climax burst of a run for socials.
- **Cloud sync**: keep meta-progression across devices via Game Center / Play Games Services.
- **Accessibility pass**: colorblind palettes for gem rarities, reduced-flash mode for the level-up burst, and adjustable virtual-stick size/deadzone.
