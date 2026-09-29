using UnityEngine;

public enum CharacterWeapon { Unarmed, Knife, Pistol, Shotgun, Automatic }

public readonly struct CharacterWeaponSettings
{
    public readonly string Name;
    public readonly int Capacity, Pellets, Damage;
    public readonly float Interval, Spread, Reload, Reach;
    public bool IsMelee => Capacity == 0;
    public CharacterWeaponSettings(string name, int capacity, float interval, int pellets, float spread, float reload, int damage, float reach)
    { Name = name; Capacity = capacity; Interval = interval; Pellets = pellets; Spread = spread; Reload = reload; Damage = damage; Reach = reach; }
    public static CharacterWeaponSettings For(CharacterWeapon kind)
    {
        switch (kind)
        {
            case CharacterWeapon.Knife: return new CharacterWeaponSettings("MES", 0, 0.34f, 0, 0, 0, 2, 1.18f);
            case CharacterWeapon.Pistol: return new CharacterWeaponSettings("PISTOOL", 12, 0.13f, 1, 0, 0.6f, 1, 35);
            case CharacterWeapon.Shotgun: return new CharacterWeaponSettings("SHOTGUN", 6, 0.48f, 7, 16, 0.85f, 1, 22);
            case CharacterWeapon.Automatic: return new CharacterWeaponSettings("AUTOMATISCH", 30, 0.065f, 1, 3, 0.75f, 1, 35);
            default: return new CharacterWeaponSettings("LEGE HANDEN", 0, 0.38f, 0, 0, 0, 1, 0.85f);
        }
    }
}

// All coordinates refer to the user's unchanged Jestan atlas, using bottom-left pixels.
// Each pose pivots on the same head centre, rather than the varying bounds of its arms.
public sealed class CharacterSpriteAtlas : System.IDisposable
{
    public const float PixelsPerUnit = 24;
    public readonly Material Material;
    public readonly Material TraceMaterial;
    readonly Material glowMaterial;
    public readonly Sprite Idle, PistolPose, LongGunPose, Thigh, Shin, Flash, Glow, Corpse;
    public readonly Sprite[] Strike;
    readonly Sprite[] weapons = new Sprite[5];
    readonly System.Collections.Generic.List<Sprite> owned = new System.Collections.Generic.List<Sprite>();
    readonly Texture2D texture, glowTexture;

    public CharacterSpriteAtlas(Texture2D sheet, int characterVariant = 0)
    {
        texture = sheet;
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material = new Material(shader) { mainTexture = sheet };
        TraceMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = Texture2D.whiteTexture };
        int column = Mathf.Clamp(characterVariant, 0, 4) * 32;
        Corpse = Slice("Fallen character / supplied death pose", Mathf.Clamp(characterVariant, 0, 4) * 64, 16, 64, 64, 32, 32);
        Idle = Slice("Empty hands / reference 2", 4 + column, 571, 24, 15, 12, 9);
        PistolPose = Slice("Two handed pistol", 4 + column, 498, 24, 24, 12, 18);
        LongGunPose = Slice("Long gun support", 5 + column, 527, 23, 28, 11, 21);
        Strike = new[] {
            Slice("Strike 1", 451, 566, 25, 20, 13, 14),
            Slice("Strike 2", 483, 571, 25, 16, 13, 9),
            Slice("Strike 3", 517, 564, 23, 22, 11, 16),
            Slice("Strike 4", 548, 565, 24, 21, 12, 15)
        };
        Thigh = Slice("Upper leg / hip pivot", 9, 472, 7, 7, 3.5f, 7);
        Shin = Slice("Lower leg / knee pivot", 9, 464, 7, 8, 3.5f, 8);
        Flash = Slice("Muzzle flash", 231, 219, 15, 13, 7.5f, 0);
        weapons[(int)CharacterWeapon.Knife] = Slice("Knife", 13, 153, 4, 12, 2, 2);
        weapons[(int)CharacterWeapon.Pistol] = Slice("Pistol", 106, 136, 8, 7, 6, 3);
        weapons[(int)CharacterWeapon.Shotgun] = Slice("Shotgun", 8, 200, 7, 30, 3.5f, 21);
        weapons[(int)CharacterWeapon.Automatic] = Slice("Automatic rifle", 70, 199, 9, 28, 4.5f, 19);
        glowTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Soft pickup halo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - radius), 2);
                pixels[y * 64 + x] = new Color(1, 1, 1, alpha);
            }
        glowTexture.SetPixels(pixels); glowTexture.Apply();
        glowMaterial = new Material(shader) { mainTexture = glowTexture };
        Glow = Sprite.Create(glowTexture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 48);
        owned.Add(Glow);
    }

    Sprite Slice(string name, int x, int y, int w, int h, float px, float py)
    {
        var sprite = Sprite.Create(texture, new Rect(x, y, w, h), new Vector2(px / w, py / h), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = name; owned.Add(sprite); return sprite;
    }
    public Sprite Weapon(CharacterWeapon kind) => weapons[(int)kind];
    public static float WeaponRotation(CharacterWeapon kind) => kind == CharacterWeapon.Knife ? -90 : kind == CharacterWeapon.Pistol ? 180 : 90;
    // Pixel positions measured at the end of each barrel, relative to its grip pivot.
    public static Vector3 BarrelTip(CharacterWeapon kind)
    {
        switch (kind)
        {
            case CharacterWeapon.Pistol: return new Vector3(-6f, 2f, 0) / PixelsPerUnit;
            case CharacterWeapon.Shotgun: return new Vector3(0.5f, -21f, 0) / PixelsPerUnit;
            case CharacterWeapon.Automatic: return new Vector3(0.5f, -19f, 0) / PixelsPerUnit;
            default: return Vector3.zero;
        }
    }
    public SpriteRenderer Part(string name, Transform parent, Sprite sprite, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite; renderer.sharedMaterial = sprite == Glow ? glowMaterial : Material; renderer.sortingOrder = order;
        return renderer;
    }
    public void Dispose()
    {
        foreach (var sprite in owned) Object.Destroy(sprite);
        Object.Destroy(glowTexture); Object.Destroy(Material); Object.Destroy(glowMaterial); Object.Destroy(TraceMaterial);
    }
}
