using UnityEngine;

// Straight top-down strides. Legs telescope along the travel axis; no sideways IK swing.
public sealed class CharacterBodyVisual
{
    SpriteRenderer corpse;
    public bool IsCorpse => corpse != null && corpse.enabled;
    public void ShowCorpse(Vector2 direction)
    {
        Legs.gameObject.SetActive(false); Shoulders.gameObject.SetActive(false);
        if (corpse == null) corpse = atlas.Part("Corpse - asset pack death pose", Root, atlas.Corpse, -1);
        corpse.enabled = true;
        corpse.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
    }
    public void RestoreAlive()
    {
        if (corpse != null) corpse.enabled = false;
        Legs.gameObject.SetActive(true); Shoulders.gameObject.SetActive(true);
    }
    public readonly Transform Root, Legs, Shoulders, LeftHip, RightHip, Muzzle;
    public readonly SpriteRenderer Torso, HeldWeapon;
    readonly Transform leftThigh, rightThigh, leftShin, rightShin, weaponGrip;
    readonly SpriteRenderer flash;
    readonly SpriteRenderer[] legRenderers;
    readonly CharacterSpriteAtlas atlas;
    float phase, walkBlend, travelAngle;
    const float UpperLength = 7f / 24, LowerLength = 8f / 24;

    public CharacterBodyVisual(Transform parent, CharacterSpriteAtlas atlas)
    {
        this.atlas = atlas;
        Root = Child("Character visuals", parent);
        Legs = Child("Legs - travel direction", Root);
        LeftHip = Child("Left hip - fixed", Legs); LeftHip.localPosition = new Vector3(-0.16f, 0.02f, 0);
        RightHip = Child("Right hip - fixed", Legs); RightHip.localPosition = new Vector3(0.16f, 0.02f, 0);
        leftThigh = atlas.Part("Left thigh", LeftHip, atlas.Thigh, 1).transform;
        rightThigh = atlas.Part("Right thigh", RightHip, atlas.Thigh, 1).transform;
        leftShin = atlas.Part("Left shin and boot", leftThigh, atlas.Shin, 1).transform;
        rightShin = atlas.Part("Right shin and boot", rightThigh, atlas.Shin, 1).transform;
        leftShin.localPosition = rightShin.localPosition = Vector3.down * UpperLength;
        legRenderers = new[] { leftThigh.GetComponent<SpriteRenderer>(), rightThigh.GetComponent<SpriteRenderer>(), leftShin.GetComponent<SpriteRenderer>(), rightShin.GetComponent<SpriteRenderer>() };
        Shoulders = Child("Shoulders - mouse aim", Root);
        Torso = atlas.Part("Body pose", Shoulders, atlas.Idle, 3);
        Torso.transform.localRotation = Quaternion.Euler(0, 0, 90);
        weaponGrip = Child("Hand grip", Shoulders);
        HeldWeapon = atlas.Part("Held weapon", weaponGrip, null, 2);
        Muzzle = Child("Muzzle - barrel tip", HeldWeapon.transform);
        flash = atlas.Part("Muzzle flash", Muzzle, atlas.Flash, 5);
        flash.transform.localRotation = Quaternion.Euler(0, 0, -90);
        flash.transform.localScale = Vector3.one * 0.55f;
        Pose(0, Vector2.zero, Vector2.right, CharacterWeapon.Unarmed, 0, -1, false, 5.6f);
    }
    static Transform Child(string name, Transform parent)
    { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }

    public void Pose(float dt, Vector2 velocity, Vector2 aim, CharacterWeapon weapon, float recoil, float attackProgress, bool muzzleFlash, float strideLength)
    {
        float speed = velocity.magnitude;
        float targetBlend = Mathf.Clamp01(speed / 2);
        walkBlend = Mathf.MoveTowards(walkBlend, targetBlend, dt * 10);
        if (speed > 0.1f)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90;
            travelAngle = Mathf.LerpAngle(travelAngle, angle, 1 - Mathf.Exp(-22 * dt));
            phase += Mathf.Min(speed, 10) * dt / Mathf.Max(0.5f, strideLength) * Mathf.PI * 2;
        }
        Legs.rotation = Quaternion.Euler(0, 0, travelAngle);
        PoseStraightLeg(leftThigh, leftShin, phase, walkBlend);
        PoseStraightLeg(rightThigh, rightShin, phase + Mathf.PI, walkBlend);
        // At rest the supplied idle pose is the complete silhouette. Blend the
        // stepping limbs beneath it instead of leaving a bent boot above the head.
        foreach (var renderer in legRenderers) renderer.color = new Color(1, 1, 1, walkBlend);
        float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        Shoulders.rotation = Quaternion.Euler(0, 0, aimAngle);
        // Keep the head on one anchor across every pose and every weapon.
        Shoulders.localPosition = -(Vector3)aim * recoil;
        bool striking = attackProgress >= 0 && attackProgress < 1;
        Torso.sprite = weapon == CharacterWeapon.Unarmed ? atlas.Idle : weapon == CharacterWeapon.Knife ? atlas.Strike[0] :
            weapon == CharacterWeapon.Pistol ? atlas.PistolPose : atlas.LongGunPose;
        int strikeFrame = 0;
        if (striking)
        {
            strikeFrame = attackProgress < 0.2f ? 1 : attackProgress < 0.46f ? 2 : attackProgress < 0.72f ? 3 : 0;
            Torso.sprite = atlas.Strike[strikeFrame];
        }
        HeldWeapon.sprite = atlas.Weapon(weapon);
        HeldWeapon.enabled = weapon != CharacterWeapon.Unarmed;
        HeldWeapon.transform.localRotation = Quaternion.Euler(0, 0, CharacterSpriteAtlas.WeaponRotation(weapon));
        Muzzle.localPosition = CharacterSpriteAtlas.BarrelTip(weapon);
        Muzzle.localRotation = Quaternion.Euler(0, 0, -CharacterSpriteAtlas.WeaponRotation(weapon));
        weaponGrip.localPosition = weapon == CharacterWeapon.Knife ? KnifeHand(0) :
            weapon == CharacterWeapon.Pistol ? new Vector3(0.52f, 0) : new Vector3(0.34f, -0.27f);
        weaponGrip.localRotation = Quaternion.identity;
        if (weapon == CharacterWeapon.Knife && striking)
        {
            // These measured grip points are the ends of the hands in reference 3.
            // Never interpolate the grip away from a hand in a discrete sprite frame.
            weaponGrip.localPosition = KnifeHand(strikeFrame);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(attackProgress / 0.5f));
            float angle = Mathf.Lerp(-65, 40, t);
            if (attackProgress > 0.5f) angle = Mathf.Lerp(40, 0, Mathf.SmoothStep(0, 1, (attackProgress - 0.5f) / 0.5f));
            weaponGrip.localRotation = Quaternion.Euler(0, 0, angle);
        }
        flash.enabled = muzzleFlash && !CharacterWeaponSettings.For(weapon).IsMelee;
        flash.transform.localPosition = Vector3.zero;
    }

    static Vector3 KnifeHand(int frame)
    {
        switch (frame)
        {
            case 1: return new Vector3(3f / 24, -10f / 24, 0);
            case 2: return new Vector3(12f / 24, -2f / 24, 0);
            case 3: return new Vector3(11f / 24, -5f / 24, 0);
            default: return new Vector3(11f / 24, -7f / 24, 0);
        }
    }

    static void PoseStraightLeg(Transform thigh, Transform shin, float phase, float blend)
    {
        // Signed foreshortening sends the boot forward/back on the same line.
        // The zero-length crossing is hidden under the torso; hips never slide.
        float extension = Mathf.Lerp(0.25f, Mathf.Sin(phase) * 0.92f, blend);
        thigh.localRotation = shin.localRotation = Quaternion.identity;
        thigh.localScale = new Vector3(1, extension, 1);
        shin.localPosition = Vector3.down * UpperLength;
        shin.localScale = Vector3.one;
    }
}
