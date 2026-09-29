using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class CombatFeedback : MonoBehaviour
{
    public CombatStyle Style { get; } = new CombatStyle();
    [Range(0, 1)] public float effectVolume = 0.4f;
    public int ConfirmedHits { get; private set; }
    struct Notice { public string text; public float time; public Color color; }
    struct Burst { public Vector3 world; public float time; public bool kill; public int damage; }
    readonly List<Notice> notices = new List<Notice>();
    readonly List<Burst> bursts = new List<Burst>();
    AudioSource effects;
    AudioClip hitTone, killTone, hurtTone, reloadTone, pickupTone, rankTone;
    AudioClip fleshHit, fleshKill;
    AudioClip dashSound, stepSound, emptySound;
    float nextReject, nextGhost, strideDistance;
    Vector3 lastStepPosition;
    struct Ghost { public SpriteRenderer sprite; public float born; }
    readonly List<Ghost> ghosts = new List<Ghost>();
    float shotUntil;
    void OnEnable() => CombatSignals.Fired += Shot;
    void OnDisable() => CombatSignals.Fired -= Shot;
    void Shot(Gunshot shot) { if (shot.Shooter == transform) shotUntil = Time.unscaledTime + 0.09f; }
    Camera view;
    ActionPlayer player;
    float hitUntil, killUntil, hurtUntil, pulseUntil, nextHitSound;
    GUIStyle small, large, medium;
    static readonly Color Mint = new Color(0.35f, 1, 0.8f), Gold = new Color(1, 0.75f, 0.25f);
    void Awake()
    {
        player = GetComponent<ActionPlayer>(); view = Camera.main;
        // A separate source keeps confirmation pitch independent of gunfire.
        effects = gameObject.AddComponent<AudioSource>(); effects.playOnAwake = false; effects.spatialBlend = 0;
        hitTone = Tone("Hit confirm", 950, 0.065f, false);
        killTone = Tone("Kill confirm", 480, 0.18f, true);
        hurtTone = Tone("Damage", 110, 0.2f, false);
        reloadTone = Tone("Action click", 220, 0.055f, false);
        pickupTone = Tone("Weapon ready", 660, 0.12f, true);
        rankTone = Tone("Multiplier up", 880, 0.24f, true);
        fleshHit = Crunch("Flesh impact", 0.11f, 135);
        fleshKill = Crunch("Fatal crunch", 0.28f, 65);
        dashSound = Crunch("Dash rush", 0.18f, 230);
        stepSound = Crunch("Footstep", 0.045f, 155);
        emptySound = Tone("Empty magazine click", 170, 0.045f, false);
        lastStepPosition = transform.position;
    }
    public void Rejected(string label)
    {
        if (Time.unscaledTime < nextReject) return;
        nextReject = Time.unscaledTime + 0.5f; AddNotice(label, new Color(1, 0.35f, 0.25f)); Sound(emptySound);
    }
    public void Dash()
    {
        Sound(dashSound); nextGhost = 0;
        view?.GetComponent<CharacterTestCamera>()?.Impulse(0.07f, 0.09f);
    }
    public void Swing() { effects.PlayOneShot(dashSound, effectVolume * 0.45f); }
    void MovementFeedback()
    {
        float now = Time.unscaledTime;
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            var g = ghosts[i]; float t = (now - g.born) / 0.18f;
            if (t >= 1 || g.sprite == null) { if (g.sprite != null) Destroy(g.sprite.gameObject); ghosts.RemoveAt(i); }
            else g.sprite.color = new Color(0.3f, 1, 0.8f, (1 - t) * 0.4f);
        }
        float moved = Vector3.Distance(lastStepPosition, transform.position); lastStepPosition = transform.position;
        if (player == null || !player.enabled) { strideDistance = 0; return; }
        if (player.Dashing && now >= nextGhost && player.BodyVisual != null && ghosts.Count < 10)
        {
            nextGhost = now + 0.025f;
            var original = player.BodyVisual.Torso;
            var ghost = new GameObject("Dash afterimage").AddComponent<SpriteRenderer>();
            ghost.sprite = original.sprite; ghost.sharedMaterial = original.sharedMaterial; ghost.sortingOrder = 1;
            ghost.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            ghost.transform.localScale = original.transform.lossyScale; ghost.color = new Color(0.3f, 1, 0.8f, 0.4f);
            ghosts.Add(new Ghost { sprite = ghost, born = now });
        }
        if (!player.Dashing && moved < 0.5f) strideDistance += moved;
        if (strideDistance > 1.55f) { strideDistance = 0; effects.PlayOneShot(stepSound, effectVolume * 0.2f); }
    }
    static AudioClip Crunch(string name, float duration, float bass)
    {
        const int rate = 22050; var data = new float[(int)(rate * duration)];
        var random = new System.Random(872); float filtered = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)data.Length;
            float noise = (float)random.NextDouble() * 2 - 1;
            filtered = Mathf.Lerp(filtered, noise, 0.24f);
            float crack = Mathf.Exp(-t * 35) * noise;
            float low = Mathf.Sin(i / (float)rate * bass * (1 - t * 0.35f) * Mathf.PI * 2);
            data[i] = Mathf.Clamp((filtered * 1.6f + low * 0.45f + crack * 0.5f) * Mathf.Pow(1 - t, 2), -0.85f, 0.85f);
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    static AudioClip Tone(string name, float frequency, float duration, bool rising)
    {
        const int rate = 22050;
        var samples = new float[Mathf.CeilToInt(rate * duration)]; float phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)samples.Length;
            phase += frequency * (rising ? 1 + Mathf.Floor(t * 3) * 0.25f : 1 - t * 0.55f) / rate;
            float wave = Mathf.Sin(phase * Mathf.PI * 2);
            samples[i] = (wave * 0.7f + Mathf.Sign(wave) * 0.3f) * Mathf.Min(1, t * 40) * (1 - t) * (1 - t) * 0.45f;
        }
        var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
    }
    void Sound(AudioClip clip, float pitch = 1) { effects.pitch = pitch; effects.PlayOneShot(clip, effectVolume); }
    void Update()
    {
        MovementFeedback();
        Style.Tick(Time.unscaledTime);
        notices.RemoveAll(n => Time.unscaledTime - n.time > 4);
        bursts.RemoveAll(b => Time.unscaledTime - b.time > 0.65f);
    }
    public void ConfirmHit(Vector3 position, int damage, bool killed, object victim, CharacterWeapon weapon)
    {
        ConfirmedHits++; float now = Time.unscaledTime; hitUntil = now + 0.14f;
        if (bursts.Count >= 32) bursts.RemoveAt(0);
        bursts.Add(new Burst { world = position, damage = damage, kill = killed, time = now });
        if (!killed)
        {
            if (now >= nextHitSound) { Sound(hitTone); effects.PlayOneShot(fleshHit, effectVolume); nextHitSound = now + 0.045f; }
            view?.GetComponent<CharacterTestCamera>()?.Impulse(0.09f, 0.09f);
            return;
        }
        int oldMultiplier = Style.Multiplier;
        int points = Style.Kill(victim, weapon, now, out string label);
        if (points <= 0) return;
        killUntil = now + 0.38f; pulseUntil = now + 0.22f;
        AddNotice("+" + points + "  " + label, Gold);
        Sound(Style.Multiplier > oldMultiplier ? rankTone : killTone, 1 + Mathf.Min(Style.Combo, 10) * 0.025f);
        effects.PlayOneShot(fleshKill, effectVolume * 1.3f);
        view?.GetComponent<CharacterTestCamera>()?.Impulse(0.23f, 0.18f);
    }
    public void Impact(Vector3 position)
    {
        if (bursts.Count >= 32) bursts.RemoveAt(0);
        bursts.Add(new Burst { world = position, time = Time.unscaledTime });
    }
    public void Hurt()
    {
        Style.Break(); hurtUntil = Time.unscaledTime + 0.35f;
        AddNotice("HIT TAKEN / CHAIN LOST", new Color(1, 0.3f, 0.35f)); Sound(hurtTone);
        view?.GetComponent<CharacterTestCamera>()?.Impulse(0.2f, 0.22f);
    }
    public void ActionNotice(string label, bool ready = false) { AddNotice(label, Mint); Sound(ready ? pickupTone : reloadTone); }
    void AddNotice(string label, Color color)
    {
        notices.Insert(0, new Notice { text = label, time = Time.unscaledTime, color = color });
        if (notices.Count > 5) notices.RemoveAt(notices.Count - 1);
    }
    public void ResetEncounter() { Style.Reset(); notices.Clear(); bursts.Clear(); hitUntil = killUntil = hurtUntil = pulseUntil = shotUntil = 0; ConfirmedHits = 0; ClearGhosts(); lastStepPosition = transform.position; strideDistance = 0; }
    void ClearGhosts() { foreach (var g in ghosts) if (g.sprite != null) Destroy(g.sprite.gameObject); ghosts.Clear(); }
    void OnDestroy()
    {
        ClearGhosts();
        foreach (var clip in new[] { hitTone, killTone, hurtTone, reloadTone, pickupTone, rankTone, fleshHit, fleshKill, dashSound, stepSound, emptySound }) if (clip != null) Destroy(clip);
        if (effects != null) Destroy(effects);
    }
    void Box(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); }
    void Text(Rect rect, string text, GUIStyle style, Color color) { GUI.color = color; GUI.Label(rect, text, style); }
    void OnGUI()
    {
        if (player == null) return;
        if (small == null)
        {
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            medium = new GUIStyle(small) { fontSize = 20 }; large = new GUIStyle(small) { fontSize = 38 };
        }
        var matrix = GUI.matrix; var color = GUI.color;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        float width = Screen.width / scale, height = Screen.height / scale, now = Time.unscaledTime;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        Box(new Rect(18, 166, 254, 79), new Color(0.025f, 0.035f, 0.06f, 0.85f));
        Text(new Rect(30, 170, 230, 22), player.Dashing ? "DASH!" : player.DashReady >= 1 ? "SHIFT / SPACE  •  DASH READY" : "DASH RECHARGING", small, Mint);
        Box(new Rect(30, 195, 230, 4), new Color(0.2f, 0.23f, 0.27f));
        Box(new Rect(30, 195, 230 * player.DashReady, 4), Mint);
        if (!player.Weapon.IsMelee)
        {
            Color ammoColor = player.Ammo <= 3 ? new Color(1, 0.35f, 0.25f) : Gold;
            Text(new Rect(30, 206, 230, 24), player.Reloading ? "RELOADING " + Mathf.RoundToInt(player.ReloadProgress * 100) + "%" : player.Ammo == 0 ? "EMPTY  /  R TO RELOAD" : "AMMO  " + player.Ammo + " / " + player.magazineSize, small, ammoColor);
            Box(new Rect(30, 234, 230 * (player.Reloading ? player.ReloadProgress : player.Ammo / (float)player.magazineSize), 4), ammoColor);
        }
        else Text(new Rect(30, 207, 230, 24), "CLOSE RANGE / KEEP MOVING", small, Gold);
        if (now < killUntil)
        {
            float fade = Mathf.Clamp01((killUntil - now) / 0.18f);
            Text(new Rect(width / 2 - 115, 65, 300, 48), Style.Combo > 1 ? Style.Combo + " / SLAUGHTER" : "EXECUTED", medium, new Color(1, 0.25f, 0.23f, fade));
            Box(new Rect(0, 0, width, 5), new Color(0.9f, 0.05f, 0.12f, fade));
            Box(new Rect(0, height - 5, width, 5), new Color(0.9f, 0.05f, 0.12f, fade));
        }
        if (Mouse.current != null && now < shotUntil)
        {
            Vector2 p = Mouse.current.position.ReadValue() / scale; p.y = height - p.y;
            float radius = 10 + 130 * (shotUntil - now);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2; Box(new Rect(p.x + Mathf.Cos(a) * radius - 2, p.y + Mathf.Sin(a) * radius - 2, 4, 4), Gold); }
        }
        float x = width - 300, y = 185;
        Color accent = Style.Combo > 0 ? Gold : Mint;
        Box(new Rect(x, y, 276, 270), new Color(0.025f, 0.035f, 0.06f, 0.91f));
        Box(new Rect(x, y, 4, 270), accent);
        Text(new Rect(x + 18, y + 12, 245, 24), "COMBAT / STYLE", small, Mint);
        Text(new Rect(x + 18, y + 38, 245, 30), Style.Rank, medium, accent);
        Text(new Rect(x + 18, y + 73, 245, 48), Style.Score.ToString("000000"), large, Color.white);
        Text(new Rect(x + 18, y + 127, 245, 26), "x" + Style.Multiplier + "    " + Style.Combo + " CHAIN    BEST " + Style.BestCombo, small, accent);
        Box(new Rect(x + 18, y + 161, 240, 5), new Color(0.2f, 0.23f, 0.27f));
        Box(new Rect(x + 18, y + 161, 240 * Mathf.Clamp01((Style.ExpiresAt - now) / CombatStyle.Window), 5), accent);
        for (int i = 0; i < notices.Count; i++)
        {
            var n = notices[i]; Color c = n.color; c.a = Mathf.Clamp01(4 - (now - n.time));
            Text(new Rect(x + 18, y + 177 + i * 17, 245, 22), n.text, small, c);
        }
        if (notices.Count == 0) Text(new Rect(x + 18, y + 183, 242, 55), "CHAIN KILLS. SWITCH WEAPONS.\nKEEP MOVING.", small, new Color(0.6f, 0.68f, 0.75f));
        if (now < pulseUntil) Box(new Rect(x, y, 276, 270), new Color(1, 0.75f, 0.3f, (pulseUntil - now) * 0.3f));
        if (now < hurtUntil)
        {
            float alpha = (hurtUntil - now) / 0.35f;
            for (int i = 0; i < 7; i++)
            {
                float inset = i * 8; Color c = new Color(1, 0.08f, 0.15f, alpha * (7 - i) * 0.035f);
                Box(new Rect(inset, inset, width - inset * 2, 8), c); Box(new Rect(inset, height - inset - 8, width - inset * 2, 8), c);
                Box(new Rect(inset, inset, 8, height - inset * 2), c); Box(new Rect(width - inset - 8, inset, 8, height - inset * 2), c);
            }
        }
        if (Mouse.current != null && now < hitUntil)
        {
            Vector2 cursor = Mouse.current.position.ReadValue() / scale; cursor.y = height - cursor.y;
            Color c = now < killUntil ? Gold : Color.white;
            for (int i = 0; i < 4; i++)
            {
                float a = (45 + i * 90) * Mathf.Deg2Rad;
                for (int k = 7; k < 15; k += 2) Box(new Rect(cursor.x + Mathf.Cos(a) * k, cursor.y + Mathf.Sin(a) * k, 2, 2), c);
            }
        }
        if (view != null) foreach (var burst in bursts)
        {
            Vector3 screen = view.WorldToScreenPoint(burst.world); if (screen.z <= 0) continue;
            float t = (now - burst.time) / 0.65f;
            Vector2 p = new Vector2(screen.x / scale, height - screen.y / scale);
            Color c = burst.kill ? Gold : burst.damage > 0 ? Mint : new Color(1, 0.85f, 0.5f); c.a = 1 - t;
            int rays = burst.kill ? 12 : 6;
            for (int i = 0; i < rays; i++)
            {
                float angle = i * Mathf.PI * 2 / rays; float distance = (burst.kill ? 55 : 26) * t;
                Box(new Rect(p.x + Mathf.Cos(angle) * distance, p.y + Mathf.Sin(angle) * distance + 20 * t * t, 3, 3), c);
            }
            if (burst.damage > 0) Text(new Rect(p.x - 30, p.y - 24 - t * 35, 100, 30), burst.kill ? "FINISH" : "-" + burst.damage, small, c);
        }
        GUI.matrix = matrix; GUI.color = color;
    }
}
