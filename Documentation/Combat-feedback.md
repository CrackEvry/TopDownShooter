# Combat feedback

## Movement and handling

WASD accelerates to full speed in about 0.07 seconds, with firmer braking and
reversal. **Shift or Space** dashes in the movement direction (or aim direction
while standing still): 19 units/second for 0.14 seconds, 0.7-second cooldown.
A swept body-width collision check prevents dashing through walls or people.
Dashing allows shooting and aiming but grants no invulnerability. Focus loss
and disabling the player cancel the dash and any buffered shot.

Clicks are buffered for 0.12 seconds, making slightly early semi-auto inputs
reliable without changing each gun's fire-rate limit. Automatic weapons still
fire while held. The camera follows faster with a small movement look-ahead.

Dash adds fading mint afterimages and a rush sound. Travel adds quiet footsteps;
melee swings have a whoosh. Empty fire gives a rate-limited click and warning,
the last round prompts a reload, dropping a weapon is acknowledged, and a new
left-hand HUD shows dash recovery, ammo/low ammo and reload progress.
Acceleration, braking, dash speed/duration/cooldown are editable on ActionPlayer.

Open **Tools > Top Down Shooter > Open NPC Lab** and press Play. The right-hand
COMBAT / STYLE panel shows total score, rank, multiplier, current chain, best
chain, a countdown bar and the five most recent actions. Character Lab also
shows the panel, but its resetting practice targets only give hit confirmation.

- Every successful shot uses the existing weapon-specific sound and a stronger,
  bounded camera impulse. Shotgun shots have more kick. Aim compensates for shake.
- Hits show a crosshair marker, floating damage and impact particles, with a
  short confirmation tone. Wall impacts show sparks without awarding points.
- Confirmed NPC kills show a gold burst, FINISH text, a rising confirmation tone
  and a larger camera impulse. A multiplier increase plays a distinct tone.
- NPC hits create directional world-space blood sprays. Fatal hits leave larger
  floor pools and replace the living character with the matching corpse from the
  supplied atlas's bottom row. Corpses have no blocking collider.
- Fatal sprays now contain 86 pieces, including nine bouncing flesh/bone
  fragments; shotgun and knife kills produce 112 pieces with sixteen fragments.
  Fragments settle with the blood and share its bounded cleanup. Nonfatal hits
  spray 32 droplets and briefly tint the torso red.
- Impact sounds layer synthesized noise, a sharp transient and a low thump under
  hit/kill confirmation tones. Hits add a small camera kick; kills add a stronger
  kick, a brief red border accent and an EXECUTED / SLAUGHTER notification.
  Each shot also expands the reticle. These effects do not pause the game.
- Blood settles and stays until reset, with at most 80 stain groups retained.
  F6 and respawn remove stains and restore living body parts and colliders.
- Firearm intervals are pistol 0.13s, shotgun 0.48s, automatic 0.065s; reloads
  take 0.60s, 0.85s and 0.75s respectively, for faster combat pacing.
- Pickups, reload start and reload completion appear in the feed with sounds.
- Taking damage shows red screen edges, a low damage tone and camera shake;
  it breaks the chain but preserves earned score.

## Scoring

A kill earns 100 base points. Melee adds 50; changing weapon since the previous
kill in the same chain adds 50. These points are multiplied by the current
multiplier. A kill within four seconds continues the chain. Multipliers rise
every three kills after the first (kill 4 = x2, 7 = x3, 10 = x4, 13 = x5).
The maximum multiplier is x5. Ranks progress at chains of 1, 3, 5, 8 and 12.

Missing, hitting walls and nonlethal hits award no score and do not extend the
chain. Each NPC can award kill points only once per encounter; extra shotgun
pellets and repeated damage to a dead NPC cannot award duplicate kills.
F6 / encounter reset and respawn clear score, chain and best chain. Scores are
session-only: there is no saved leaderboard yet. The panel is an original arcade
style display inspired by the requested reference.

## Implementation and tuning

`CombatStyle.cs` contains scoring rules independent of presentation.
`CombatFeedback.cs` draws the responsive HUD and world-positioned screen effects,
and synthesizes short confirmation tones. It is automatically attached by
`ActionPlayer.Awake`, so existing Character Lab and NPC Lab scenes work immediately.
Accepted NPC damage calls it from `NpcBrain.TakeDamage`; rejected damage does not.

Adjust `feedbackIntensity`, `shakeStrength` and `shakeDuration` on the camera's
`CharacterTestCamera`; `effectVolume` on the player's `CombatFeedback` during Play;
and `volume` on `ShotFeedback` for gunfire. Feedback uses unscaled time and never
changes global time scale. Effects and action history are capped to avoid growth
during sustained shooting. Sparks and damage labels are screen overlays anchored
to world positions. BloodEffects.cs separately creates persistent world-space
blood meshes beneath the corpses and above the floor.

`CombatFeedbackValidation.RunRules` checks scoring, duplicate prevention, expiry,
weapon bonuses, multiplier caps and resets. `NpcLabValidation.Run` also checks the
integration through actual NPC damage, kills, player damage and encounter resets.
