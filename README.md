# TopDownShooter

Open **Tools > Top Down Shooter > Open Character Lab** in Unity, then press Play.
The generated scene lives in `Assets/CharacterLab/CharacterLab.unity`; the reusable
player prefab is `Assets/CharacterLab/Player.prefab`. The existing SampleScene is
preserved.

- WASD / arrow keys: immediate movement, normalized diagonals.
- Mouse: independent 360-degree aim. Hold left click to shoot.
- R: reload the 12-round practice pistol.
- Feet follow actual travel; the pistol stance follows aim. Walking stops at walls.
- Five targets flash when hit and reset after 1.2 seconds. Cover blocks shots.

Tune `ActionPlayer` on the player: Move Speed (7 units/sec), Shot Interval
(0.12 sec), Stride Length (1.15 units), Magazine Size and Reload Seconds.
The test weapon uses instantaneous raycast hits and short-lived visible tracers.
Reloading is a practice-scene feature; this is an initial movement/aim prototype,
not a verified exact reproduction of Hotline Miami or its main character.
Walking animates separate leg sprites procedurally, rather than inventing frames
that are not present in the supplied pack. Visual body parts are created on Play.

Art: **Jestan** (@jestanpixels), supplied TopDownShooterAssets pack.
Original terms are preserved in `Assets/Art/Jestan-License.txt`.
