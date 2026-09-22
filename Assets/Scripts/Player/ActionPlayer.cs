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
            }
            if (mouse != null && view != null)
            {
                Vector2 delta = (Vector2)view.ScreenToWorldPoint(mouse.position.ReadValue()) - (Vector2)transform.position;
                SetAimDirection(delta);
            }
            if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)) TryPickupNearest();
            if (mouse != null) TryAttack(mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed);
        }
        TickActions();
        NearbyWeapon = FindNearbyWeapon();
        ShotKick = Mathf.MoveTowards(ShotKick, 0, Time.deltaTime * 2);
    }

    public void SetMoveInput(Vector2 direction) => input = Vector2.ClampMagnitude(direction, 1);
    public void SetAimDirection(Vector2 direction) { if (direction.sqrMagnitude > 0.01f) AimDirection = direction.normalized; }
    void FixedUpdate()
    {
        TravelVelocity = (body.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = body.position;
        body.linearVelocity = input * moveSpeed;
    }
    void LateUpdate()
    {
        float progress = Attacking ? Mathf.Clamp01((Time.time - attackStarted) / Weapon.Interval) : -1;
        BodyVisual?.Pose(Time.deltaTime, TravelVelocity, Attacking ? attackDirection : AimDirection, EquippedWeapon, ShotKick, progress, Time.time < flashUntil, strideLength);
    }
    void Equip(CharacterWeapon kind, int rounds)
    {
        EquippedWeapon = kind;
        Ammo = Mathf.Clamp(rounds, 0, Weapon.Capacity);
        reloadUntil = 0; attackStarted = -1; flashUntil = 0; ShotKick = 0;
        BodyVisual?.Pose(0, TravelVelocity, AimDirection, kind, 0, -1, false, strideLength);
    }
    public bool TryReload()
    {
        if (Weapon.IsMelee || Reloading || Ammo >= Weapon.Capacity || Attacking) return false;
        reloadUntil = Time.time + Weapon.Reload; return true;
    }
    public void TickActions()
    {
        if (Reloading && Time.time >= reloadUntil) { Ammo = Weapon.Capacity; reloadUntil = 0; }
        if (!Attacking) return;
        float progress = (Time.time - attackStarted) / Weapon.Interval;
        if (progress >= 0.28f && progress <= 0.72f) MeleeContact();
        if (progress >= 1) { attackStarted = -1; struck.Clear(); }
    }
    public bool TryAttack(bool pressed = true, bool held = false)
    {
        if ((!pressed && !(held && EquippedWeapon == CharacterWeapon.Automatic)) || Time.time < nextActionTime || Reloading || Attacking) return false;
        if (!Weapon.IsMelee && Ammo <= 0) return false;
        nextActionTime = Time.time + Weapon.Interval;
        if (Weapon.IsMelee)
        {
            attackStarted = Time.time; attackDirection = AimDirection; struck.Clear(); LastPelletCount = 0;
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
        Equip(kind, rounds); NearbyWeapon = null; return true;
    }
    public bool DropWeapon()
    {
        if (EquippedWeapon == CharacterWeapon.Unarmed || Attacking) return false;
        Cast(body.position, AimDirection, 0.65f, out float distance);
        Vector2 point = body.position + AimDirection * Mathf.Max(0, distance - 0.2f);
        WorldWeapon.Spawn(characterSheet, EquippedWeapon, point, Ammo);
        Equip(CharacterWeapon.Unarmed, 0); return true;
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
            GameObject victim = test != null ? test.gameObject : enemy != null ? enemy.gameObject : null;
            if (victim == null || struck.Contains(victim)) continue;
            Vector2 delta = (Vector2)other.bounds.center - body.position;
            if (Vector2.Angle(attackDirection, delta) > 65) continue;
            Collider2D obstruction = Cast(body.position, delta.normalized, delta.magnitude + 0.01f, out _);
            if (obstruction != null && obstruction != other && !obstruction.transform.IsChildOf(victim.transform)) continue;
            struck.Add(victim);
            test?.Hit(); enemy?.TakeDamage(Weapon.Damage);
        }
    }
    void Shoot()
    {
        Ammo--; LastPelletCount = Weapon.Pellets;
        ShotKick = EquippedWeapon == CharacterWeapon.Shotgun ? 0.14f : 0.075f;
        flashUntil = Time.time + 0.04f;
        for (int i = 0; i < Weapon.Pellets; i++)
        {
            float offset = Weapon.Pellets == 1 ? Random.Range(-Weapon.Spread, Weapon.Spread) * 0.5f : Mathf.Lerp(-Weapon.Spread / 2, Weapon.Spread / 2, i / (float)(Weapon.Pellets - 1));
            Vector2 direction = Quaternion.Euler(0, 0, offset) * AimDirection;
            Collider2D hit = Cast(body.position, direction, Weapon.Reach, out float distance);
            if (hit != null)
            {
                hit.GetComponentInParent<CharacterTestTarget>()?.Hit();
                hit.GetComponentInParent<EnemyHealth>()?.TakeDamage(Weapon.Damage);
            }
            var trace = new GameObject("Shot tracer").AddComponent<LineRenderer>();
            trace.sharedMaterial = atlas.TraceMaterial; trace.sortingOrder = 8;
            trace.startWidth = 0.035f; trace.endWidth = 0.01f;
            trace.startColor = new Color(1, 0.9f, 0.55f); trace.endColor = new Color(1, 0.65f, 0.3f, 0);
            trace.positionCount = 2;
            trace.SetPosition(0, body.position + direction * Mathf.Min(0.65f, distance));
            trace.SetPosition(1, body.position + direction * distance);
            Destroy(trace.gameObject, 0.055f);
        }
    }
    void OnApplicationFocus(bool focused) { if (!focused) { input = Vector2.zero; if (body != null) body.linearVelocity = Vector2.zero; } }
    void OnDisable() { if (body != null) body.linearVelocity = Vector2.zero; }
    void OnDestroy() => atlas?.Dispose();
}
