using System.Collections.Generic;
using UnityEngine;

public class WorldWeapon : MonoBehaviour
{
    public Texture2D characterSheet;
    public CharacterWeapon kind = CharacterWeapon.Pistol;
    public int ammo = -1;
    public float bobHeight = 0.055f;
    public float bobFrequency = 0.8f;
    public static readonly HashSet<WorldWeapon> Available = new HashSet<WorldWeapon>();
    public bool Collected { get; private set; }
    public Transform Visual { get; private set; }
    public SpriteRenderer Halo { get; private set; }
    CharacterSpriteAtlas atlas;
    float phase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => Available.Clear();
    void OnEnable() { if (!Collected) Available.Add(this); }
    void OnDisable() => Available.Remove(this);
    void Start() => InitializeVisual();
    public void InitializeVisual()
    {
        if (atlas != null || characterSheet == null || kind == CharacterWeapon.Unarmed) return;
        if (ammo < 0) ammo = CharacterWeaponSettings.For(kind).Capacity;
        atlas = new CharacterSpriteAtlas(characterSheet);
        phase = transform.position.x * 1.7f + transform.position.y * 0.9f;
        Halo = atlas.Part("Soft floor glow", transform, atlas.Glow, -3);
        Halo.transform.localScale = new Vector3(1.4f, 0.9f, 1);
        var shadow = atlas.Part("Ground shadow", transform, atlas.Glow, -2);
        shadow.color = new Color(0, 0, 0, 0.6f);
        shadow.transform.localScale = new Vector3(0.85f, 0.36f, 1);
        Visual = new GameObject("Floating weapon").transform;
        Visual.SetParent(transform, false);
        var renderer = atlas.Part(kind.ToString(), Visual, atlas.Weapon(kind), -1);
        renderer.transform.localRotation = Quaternion.Euler(0, 0, CharacterSpriteAtlas.WeaponRotation(kind) + 25);
        // Centre the visible sprite, despite its grip pivot used when held.
        renderer.transform.localPosition = -(renderer.transform.localRotation * renderer.sprite.bounds.center);
        Animate(Time.time);
    }
    void Update() => Animate(Time.time);
    public void Animate(float time)
    {
        if (Visual == null) return;
        float wave = Mathf.Sin(time * bobFrequency * Mathf.PI * 2 + phase);
        Visual.localPosition = new Vector3(0, 0.12f + wave * bobHeight, 0);
        Color color = kind == CharacterWeapon.Knife ? new Color(0.40f, 1, 0.82f) :
            kind == CharacterWeapon.Shotgun ? new Color(1, 0.67f, 0.30f) :
            kind == CharacterWeapon.Automatic ? new Color(0.75f, 0.50f, 1) : new Color(0.40f, 0.75f, 1);
        color.a = 0.38f + wave * 0.04f;
        Halo.color = color;
    }
    public bool Take()
    {
        if (Collected || !isActiveAndEnabled) return false;
        Collected = true; Available.Remove(this);
        gameObject.SetActive(false); Destroy(gameObject); return true;
    }
    public static WorldWeapon Spawn(Texture2D sheet, CharacterWeapon kind, Vector2 position, int ammo)
    {
        var pickup = new GameObject(kind + " pickup").AddComponent<WorldWeapon>();
        pickup.transform.position = position; pickup.characterSheet = sheet; pickup.kind = kind; pickup.ammo = ammo;
        pickup.InitializeVisual(); return pickup;
    }
    void OnDestroy() => atlas?.Dispose();
}
