using UnityEngine;
using UnityEngine.InputSystem;

// The physics root never rotates. Feet follow travel; shoulders follow the cursor.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ActionPlayer : MonoBehaviour
{
    public Texture2D characterSheet;
    [Min(0)] public float moveSpeed = 7f;
    [Min(0.02f)] public float shotInterval = 0.12f;
    public float strideLength = 1.15f;
    public LayerMask shotMask = ~0;
    public int magazineSize = 12;
    public float reloadSeconds = 0.75f;
    public int Ammo { get; private set; }
    public bool Reloading => reloadUntil > 0;
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 TravelVelocity { get; private set; }
    public float ShotKick { get; private set; }
    Rigidbody2D body;
    Camera view;
    Transform shoulders, legs, leftFoot, rightFoot, flash;
    SpriteRenderer torso;
    Vector2 input, previousPosition;
    float phase, nextShot, reloadUntil, flashUntil;
    readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    Material spriteMaterial;
    readonly System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetComponent<CircleCollider2D>().radius = 0.30f;
        view = Camera.main;
        previousPosition = body.position;
        Ammo = magazineSize;
        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        legs = new GameObject("Legs - travel direction").transform;
        legs.SetParent(transform, false);
        leftFoot = Part("Left foot", legs, Slice(9, 464, 7, 15), 1).transform;
        rightFoot = Part("Right foot", legs, Slice(9, 464, 7, 15), 1).transform;
        shoulders = new GameObject("Shoulders - mouse aim").transform;
        shoulders.SetParent(transform, false);
        // Pistol pose from the supplied sheet. Pivot aligns the head with the root.
        torso = Part("Pistol stance", shoulders, Slice(5, 527, 23, 28, new Vector2(0.48f, 0.72f)), 3);
        torso.transform.localRotation = Quaternion.Euler(0, 0, 90);
        flash = Part("Muzzle flash", shoulders, Slice(232, 220, 15, 16), 5).transform;
        flash.localPosition = new Vector3(0.84f, -0.18f, 0);
        flash.localScale = Vector3.one * 0.65f;
        flash.gameObject.SetActive(false);
    }

    Sprite Slice(int x, int y, int w, int h, Vector2? pivot = null)
    {
        var sprite = Sprite.Create(characterSheet, new Rect(x, y, w, h), pivot ?? Vector2.one * 0.5f, 24);
        sprites.Add(sprite);
        return sprite;
    }

    SpriteRenderer Part(string label, Transform parent, Sprite sprite, int order)
    {
        var part = new GameObject(label).AddComponent<SpriteRenderer>();
        part.transform.SetParent(parent, false);
        part.sprite = sprite;
        part.sharedMaterial = spriteMaterial;
        part.sortingOrder = order;
        return part;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        input = Vector2.zero;
        if (keyboard != null && Application.isFocused)
        {
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            input = Vector2.ClampMagnitude(input, 1);
            if (keyboard.rKey.wasPressedThisFrame && Ammo < magazineSize && !Reloading)
                reloadUntil = Time.time + reloadSeconds;
        }
        if (Reloading && Time.time >= reloadUntil) { Ammo = magazineSize; reloadUntil = 0; }
        if (mouse != null && view != null)
        {
            Vector2 delta = (Vector2)view.ScreenToWorldPoint(mouse.position.ReadValue()) - (Vector2)transform.position;
            if (delta.sqrMagnitude > 0.01f) AimDirection = delta.normalized;
            shoulders.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);
            if (Application.isFocused && mouse.leftButton.isPressed && Time.time >= nextShot && !Reloading && Ammo > 0) Shoot();
        }
        ShotKick = Mathf.MoveTowards(ShotKick, 0, Time.deltaTime * 2);
        torso.transform.localPosition = Vector3.left * ShotKick;
        flash.gameObject.SetActive(Time.time < flashUntil);
    }

    void FixedUpdate()
    {
        // Actual displacement prevents walking in place against a wall.
        TravelVelocity = (body.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = body.position;
        body.linearVelocity = input * moveSpeed;
    }

    void LateUpdate()
    {
        float speed = TravelVelocity.magnitude;
        if (speed > 0.1f)
        {
            legs.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(TravelVelocity.y, TravelVelocity.x) * Mathf.Rad2Deg);
            phase += speed * Time.deltaTime / Mathf.Max(0.1f, strideLength) * Mathf.PI * 2;
        }
        float step = speed > 0.1f ? Mathf.Sin(phase) * 0.23f : 0;
        leftFoot.localPosition = new Vector3(step, 0.18f, 0);
        rightFoot.localPosition = new Vector3(-step, -0.18f, 0);
        leftFoot.localRotation = Quaternion.Euler(0, 0, 90 + step * 25);
        rightFoot.localRotation = Quaternion.Euler(0, 0, 90 - step * 25);
    }

    void Shoot()
    {
        nextShot = Time.time + shotInterval;
        Ammo--;
        ShotKick = 0.095f;
        flashUntil = Time.time + 0.035f;
        Vector2 origin = transform.position;
        // Cast from the body, so a barrel clipping a wall cannot shoot through it.
        var filter = new ContactFilter2D();
        filter.SetLayerMask(shotMask);
        filter.useTriggers = false;
        int count = Physics2D.Raycast(origin, AimDirection, filter, hits, 35);
        float distance = 35;
        Collider2D closest = null;
        for (int i = 0; i < count; i++)
            if (hits[i].collider.attachedRigidbody != body && hits[i].distance < distance)
            { distance = hits[i].distance; closest = hits[i].collider; }
        if (closest != null)
        {
            closest.GetComponentInParent<CharacterTestTarget>()?.Hit();
            closest.GetComponentInParent<EnemyHealth>()?.TakeDamage(1);
        }
        var trace = new GameObject("Shot tracer").AddComponent<LineRenderer>();
        trace.sharedMaterial = spriteMaterial;
        trace.sortingOrder = 8;
        trace.startWidth = 0.045f;
        trace.endWidth = 0.015f;
        trace.startColor = new Color(1, 0.9f, 0.55f);
        trace.endColor = new Color(1, 0.65f, 0.3f, 0);
        trace.positionCount = 2;
        trace.SetPosition(0, origin + AimDirection * Mathf.Min(0.7f, distance));
        trace.SetPosition(1, origin + AimDirection * distance);
        Destroy(trace.gameObject, 0.055f);
    }

    void OnApplicationFocus(bool focused) { if (!focused) { input = Vector2.zero; if (body != null) body.linearVelocity = Vector2.zero; } }
    void OnDisable() { if (body != null) body.linearVelocity = Vector2.zero; }
    void OnDestroy() { foreach (var sprite in sprites) Destroy(sprite); if (spriteMaterial != null) Destroy(spriteMaterial); }
}
