using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ActionPlayer : MonoBehaviour
{
    public Texture2D characterSheet;
    public CharacterWeapon startingWeapon = CharacterWeapon.Unarmed;
    [Min(0)] public float moveSpeed = 7;
    [Min(0.5f)] public float strideLength = 5.6f;
    public LayerMask shotMask = ~0;
    public float pickupRadius = 1.35f;
    public float acceleration = 105, braking = 160;
    public float dashSpeed = 19, dashDuration = 0.14f, dashCooldown = 0.7f;
    public bool Dashing => Time.time < dashUntil;
    public float DashReady => Mathf.Clamp01(1 - (nextDash - Time.time) / Mathf.Max(0.01f, dashCooldown));
    public float ReloadProgress => Reloading ? Mathf.Clamp01(1 - (reloadUntil - Time.time) / Weapon.Reload) : 0;
    float dashUntil, nextDash, bufferedUntil;
    Vector2 dashDirection;
    public CharacterWeapon EquippedWeapon { get; private set; }
    public CharacterWeaponSettings Weapon => CharacterWeaponSettings.For(EquippedWeapon);
    public int Ammo { get; private set; }
    public int magazineSize => Weapon.Capacity;
    public bool Reloading => reloadUntil > 0;
    public bool Attacking => attackStarted >= 0;
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 TravelVelocity { get; private set; }
    public float ShotKick { get; private set; }
    public CharacterBodyVisual BodyVisual { get; private set; }
    public WorldWeapon NearbyWeapon { get; private set; }
    public int LastPelletCount { get; private set; }
    public ShotPath LastShot { get; private set; }
    public CombatFeedback Feedback { get; private set; }
    Rigidbody2D body;
    Camera view;
    CharacterSpriteAtlas atlas;
    Vector2 input, previousPosition;
    float nextActionTime, reloadUntil, flashUntil, attackStarted = -1;
    Vector2 attackDirection;
    readonly RaycastHit2D[] hits = new RaycastHit2D[64];
    readonly Collider2D[] overlaps = new Collider2D[64];
    readonly HashSet<GameObject> struck = new HashSet<GameObject>();

    void Awake()
    {
        GoofySoundtrack.EnsurePlaying();
        Feedback = GetComponent<CombatFeedback>() ?? gameObject.AddComponent<CombatFeedback>();
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0; body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetComponent<CircleCollider2D>().radius = 0.30f;
        previousPosition = body.position;
        view = Camera.main;
        if (characterSheet == null) { Debug.LogError("Assign the Jestan character atlas to ActionPlayer.", this); enabled = false; return; }
        atlas = new CharacterSpriteAtlas(characterSheet);
        BodyVisual = new CharacterBodyVisual(transform, atlas);
        Equip(startingWeapon, CharacterWeaponSettings.For(startingWeapon).Capacity);
    }

    void Update()
    {
        ShotKick = Mathf.MoveTowards(ShotKick, 0, Time.deltaTime * 2);
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        SetMoveInput(Vector2.zero);
        if (Application.isFocused)
        {
            if (keyboard != null)
            {
                SetMoveInput(new Vector2(
                    (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)));
                if (keyboard.rKey.wasPressedThisFrame) TryReload();
                if (keyboard.qKey.wasPressedThisFrame) DropWeapon();
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame) TryDash();
            }
            if (mouse != null && view != null)
            {
                var follow = view.GetComponent<CharacterTestCamera>();
                Vector2 point = follow != null ? follow.ScreenToAimWorld(mouse.position.ReadValue()) : (Vector2)view.ScreenToWorldPoint(mouse.position.ReadValue());
                SetAimPoint(point);
            }
            if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)) TryPickupNearest();
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) BufferAttack();
                else if (mouse.leftButton.isPressed) TryAttack(false, true);
            }
        }
        TickActions();
        TickBufferedAttack();
        NearbyWeapon = FindNearbyWeapon();
    }

    public void SetMoveInput(Vector2 direction) => input = Vector2.ClampMagnitude(direction, 1);
    public void SetAimDirection(Vector2 direction) { if (direction.sqrMagnitude > 0.01f) AimDirection = direction.normalized; }
    public void SetAimPoint(Vector2 point)
    {
        Vector2 delta = point - (Vector2)transform.position;
        SetAimDirection(delta);
        if (BodyVisual == null || Weapon.IsMelee || delta.magnitude < 1.4f) return;
        // Converge from the offset barrel instead of shooting parallel to a centre-to-cursor ray.
        for (int i = 0; i < 4; i++)
        {
            BodyVisual.Pose(0, TravelVelocity, AimDirection, EquippedWeapon, ShotKick, -1, false, strideLength);
            SetAimDirection(point - (Vector2)BodyVisual.Muzzle.position);
        }
    }
    void FixedUpdate()
    {
        TravelVelocity = (body.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = body.position;
        Vector2 target = input * moveSpeed;
        if (Dashing)
        {
            float distance = dashSpeed * Time.fixedDeltaTime;
            int count = Physics2D.CircleCast(body.position, 0.30f, dashDirection, Filter(), hits, distance + 0.02f);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0, hits[i].distance - 0.02f));
            body.linearVelocity = dashDirection * (distance / Time.fixedDeltaTime);
            if (distance < 0.01f) dashUntil = 0;
        }
        else body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target,
            (input.sqrMagnitude < 0.01f || Vector2.Dot(body.linearVelocity, input) < 0 ? braking : acceleration) * Time.fixedDeltaTime);
    }
    public bool TryDash()
    {
        var health = GetComponent<PlayerVitality>();
        if (Time.time < nextDash || (health != null && health.IsDead)) return false;
        dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : AimDirection;
        dashUntil = Time.time + dashDuration; nextDash = Time.time + dashCooldown;
        Feedback.Dash(); return true;
    }
    public void BufferAttack()
    {
        if (!Weapon.IsMelee && Ammo <= 0) { Feedback.Rejected("EMPTY / PRESS R"); bufferedUntil = 0; return; }
        bufferedUntil = Time.time + 0.12f;
    }
    public void TickBufferedAttack()
    {
        if (bufferedUntil <= 0) return;
        if (Time.time > bufferedUntil) { bufferedUntil = 0; return; }
        if (TryAttack()) bufferedUntil = 0;
    }
    void LateUpdate()
    {
        float progress = Attacking ? Mathf.Clamp01((Time.time - attackStarted) / Weapon.Interval) : -1;
        BodyVisual?.Pose(Time.deltaTime, TravelVelocity, Attacking ? attackDirection : AimDirection, EquippedWeapon, ShotKick, progress, Time.time < flashUntil, strideLength);
    }
    void Equip(CharacterWeapon kind, int rounds)
    {
        EquippedWeapon = kind;
        bufferedUntil = 0;
        Ammo = Mathf.Clamp(rounds, 0, Weapon.Capacity);
        reloadUntil = 0; attackStarted = -1; flashUntil = 0; ShotKick = 0;
        BodyVisual?.Pose(0, TravelVelocity, AimDirection, kind, 0, -1, false, strideLength);
    }
    public bool TryReload()
    {
        if (Weapon.IsMelee || Reloading || Ammo >= Weapon.Capacity || Attacking) return false;
        reloadUntil = Time.time + Weapon.Reload; Feedback.ActionNotice("RELOADING"); return true;
    }
    public void TickActions()
    {
        if (Reloading && Time.time >= reloadUntil) { Ammo = Weapon.Capacity; reloadUntil = 0; Feedback.ActionNotice("MAGAZINE READY", true); }
        if (!Attacking) return;
        float progress = (Time.time - attackStarted) / Weapon.Interval;
        if (progress >= 0.28f && progress <= 0.72f) MeleeContact();
        if (progress >= 1) { attackStarted = -1; struck.Clear(); }
    }
    public bool TryAttack(bool pressed = true, bool held = false)
    {
        if ((!pressed && !(held && EquippedWeapon == CharacterWeapon.Automatic)) || Time.time < nextActionTime || Reloading || Attacking) return false;
        if (!Weapon.IsMelee && Ammo <= 0) { if (pressed || held) Feedback.Rejected("EMPTY / PRESS R"); return false; }
        nextActionTime = Time.time + Weapon.Interval;
        if (Weapon.IsMelee)
        {
            attackStarted = Time.time; attackDirection = AimDirection; struck.Clear(); LastPelletCount = 0;
            Feedback.Swing();
        }
        else Shoot();
        return true;
    }
    public WorldWeapon FindNearbyWeapon()
    {
        WorldWeapon nearest = null;
        float distance = pickupRadius;
        foreach (var pickup in WorldWeapon.Available)
        {
            if (pickup == null || pickup.Collected || !pickup.isActiveAndEnabled) continue;
            Vector2 delta = (Vector2)pickup.transform.position - body.position;
            float d = delta.magnitude;
            if (d > distance || Cast(body.position, delta.normalized, d, out _) != null) continue;
            distance = d; nearest = pickup;
        }
        return nearest;
    }
    public bool TryPickupNearest()
    {
        if (Attacking) return false;
        var pickup = FindNearbyWeapon();
        if (pickup == null) return false;
        pickup.InitializeVisual();
        var kind = pickup.kind; int rounds = pickup.ammo;
        if (!pickup.Take()) return false;
        if (EquippedWeapon != CharacterWeapon.Unarmed)
            WorldWeapon.Spawn(characterSheet, EquippedWeapon, body.position, Ammo);
        Equip(kind, rounds); Feedback.ActionNotice("EQUIPPED / " + kind.ToString().ToUpperInvariant(), true); NearbyWeapon = null; return true;
    }
    public bool DropWeapon()
    {
        if (EquippedWeapon == CharacterWeapon.Unarmed || Attacking) return false;
        Cast(body.position, AimDirection, 0.65f, out float distance);
        Vector2 point = body.position + AimDirection * Mathf.Max(0, distance - 0.2f);
        WorldWeapon.Spawn(characterSheet, EquippedWeapon, point, Ammo);
        Equip(CharacterWeapon.Unarmed, 0); Feedback.ActionNotice("WEAPON DROPPED"); return true;
    }
    ContactFilter2D Filter()
    { var filter = new ContactFilter2D(); filter.SetLayerMask(shotMask); filter.useTriggers = false; return filter; }
    Collider2D Cast(Vector2 origin, Vector2 direction, float range, out float distance)
    {
        int count = Physics2D.Raycast(origin, direction, Filter(), hits, range);
        distance = range; Collider2D closest = null;
        for (int i = 0; i < count; i++)
            if (!hits[i].collider.transform.IsChildOf(transform) && hits[i].distance < distance)
            { distance = hits[i].distance; closest = hits[i].collider; }
        return closest;
    }
    void MeleeContact()
    {
        int count = Physics2D.OverlapCircle(body.position, Weapon.Reach, Filter(), overlaps);
        for (int i = 0; i < count; i++)
        {
            var other = overlaps[i];
            if (other.transform.IsChildOf(transform)) continue;
            var test = other.GetComponentInParent<CharacterTestTarget>();
            var enemy = other.GetComponentInParent<EnemyHealth>();
            var npc = other.GetComponentInParent<NpcBrain>();
            GameObject victim = test != null ? test.gameObject : npc != null ? npc.gameObject : enemy != null ? enemy.gameObject : null;
            if (victim == null || struck.Contains(victim)) continue;
            Vector2 delta = (Vector2)other.bounds.center - body.position;
            if (Vector2.Angle(attackDirection, delta) > 65) continue;
            Collider2D obstruction = Cast(body.position, delta.normalized, delta.magnitude + 0.01f, out _);
            if (obstruction != null && obstruction != other && !obstruction.transform.IsChildOf(victim.transform)) continue;
            struck.Add(victim);
            test?.Hit(); enemy?.TakeDamage(Weapon.Damage); npc?.TakeDamage(Weapon.Damage, this);
            if (test != null) Feedback.ConfirmHit(other.bounds.center, Weapon.Damage, false, test, EquippedWeapon);
        }
    }
    void Shoot()
    {
        Ammo--; LastPelletCount = Weapon.Pellets;
        if (Ammo == 0) Feedback.ActionNotice("LAST ROUND / RELOAD");
        ShotKick = EquippedWeapon == CharacterWeapon.Shotgun ? 0.14f : 0.075f;
        flashUntil = Time.time + 0.04f;
        BodyVisual.Pose(0, TravelVelocity, AimDirection, EquippedWeapon, ShotKick, -1, true, strideLength);
        Vector2 muzzle = BodyVisual.Muzzle.position;
        Vector2 barrelDirection = BodyVisual.Muzzle.right;
        for (int i = 0; i < Weapon.Pellets; i++)
        {
            float offset = Weapon.Pellets == 1 ? Random.Range(-Weapon.Spread, Weapon.Spread) * 0.5f : Mathf.Lerp(-Weapon.Spread / 2, Weapon.Spread / 2, i / (float)(Weapon.Pellets - 1));
            Vector2 direction = Quaternion.Euler(0, 0, offset) * barrelDirection;
            LastShot = CombatBallistics.Resolve(transform, body.position, muzzle, direction, Weapon.Reach, shotMask);
            Collider2D hit = LastShot.Hit;
            if (hit != null)
            {
                hit.GetComponentInParent<CharacterTestTarget>()?.Hit();
                hit.GetComponentInParent<EnemyHealth>()?.TakeDamage(Weapon.Damage);
                hit.GetComponentInParent<NpcBrain>()?.TakeDamage(Weapon.Damage, this);
                if (hit.GetComponentInParent<CharacterTestTarget>() != null)
                    Feedback.ConfirmHit(LastShot.End, Weapon.Damage, false, hit, EquippedWeapon);
                else if (hit.GetComponentInParent<NpcBrain>() == null) Feedback.Impact(LastShot.End);
            }
            CombatBallistics.DrawTracer(LastShot, atlas.TraceMaterial, new Color(1, 0.9f, 0.55f));
        }
        if (LastShot.BarrelBlocked) flashUntil = 0;
        CombatSignals.Emit(new Gunshot(transform, muzzle, EquippedWeapon, true));
    }
    void OnApplicationFocus(bool focused) { if (!focused) CancelMotion(); }
    void OnDisable() => CancelMotion();
    void CancelMotion() { input = Vector2.zero; dashUntil = bufferedUntil = 0; if (body != null) body.linearVelocity = Vector2.zero; }
    void OnDestroy() => atlas?.Dispose();
}
