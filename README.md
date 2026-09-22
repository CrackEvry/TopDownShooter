# TopDownShooter

Open **Tools > Top Down Shooter > Open Character Lab** in Unity, then press Play.
The generated scene lives in `Assets/CharacterLab/CharacterLab.unity`; the reusable
player prefab is `Assets/CharacterLab/Player.prefab`. The existing SampleScene is
preserved.

- WASD / arrow keys: immediate movement, normalized diagonals.
- Mouse: independent 360-degree aim. Left click attacks; hold for automatic fire.
- E or right click: pick up the nearest weapon / swap the equipped weapon.
- Q: put your weapon down and return to empty hands. Ammo stays with the weapon.
- R: reload your current firearm. Dropping/swapping cancels its reload.
- The player starts unarmed. Knife, pistol, shotgun and automatic rifle pickups
  are along the south side of the room. Pickups float above a soft coloured halo;
  their interaction positions stay still.
- Feet follow actual travel. Fixed hips and connected knee joints keep the legs
  attached; the stride blends into idle and smooths changes of direction.
- The empty-hand pose and four melee poses come directly from the supplied blue
  character. Knife and punch attacks have wind-up, contact and recovery frames.
- Five targets flash when hit and reset after 1.2 seconds. Cover blocks shots.

Tune `ActionPlayer`: Move Speed (7 units/sec), Stride Length (5.6 units), Starting
Weapon and Pickup Radius. Firearm settings live in `CharacterWeapons.cs`:
pistol 12 rounds, shotgun 6 shells / 7 pellets, automatic rifle 30 rounds.
The guns use raycast hits and short-lived tracers. Melee hits occur during the
contact frames, once per target per swing, in front of the character. Walls block
both bullets and melee. Pickups cannot be taken through a wall.
At the default movement speed the walk loop lasts 0.8 seconds, matching the eight
100 ms frames in the supplied GIF; poses are interpolated at the rendering rate.

`CharacterSpriteAtlas` documents each unchanged source sprite rectangle and its
head/hand pivot. `CharacterBodyVisual` animates the two-bone legs and swaps body
poses. Visual parts are assembled on Play. Four reusable weapon prefabs live in
`Assets/CharacterLab/Weapons`. This remains a reference-inspired prototype, not
a verified exact reproduction of Hotline Miami.

Validation: `CharacterLabValidation.Run` can run in a separate Unity batch project.
It checks movement, fixed hips/knees, pickup/drop, ammo/reload, weapon fire modes,
melee timing and obstruction. With graphics enabled it renders weapon poses,
eight walk samples, eight knife samples and ground pickups for visual inspection.

Art: **Jestan** (@jestanpixels), supplied TopDownShooterAssets pack.
Original terms are preserved in `Assets/Art/Jestan-License.txt`.
