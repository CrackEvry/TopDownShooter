using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterTestCamera : MonoBehaviour
{
    public ActionPlayer player;
    [Range(0, 1)] public float shakeStrength = 0.07f;
    public float shakeDuration = 0.12f;
    public Vector2 CurrentShakeOffset { get; private set; }
    public int ShakeCount { get; private set; }
    [Range(0, 3)] public float feedbackIntensity = 1.8f;
    float shakeRemaining, shakeAmplitude, shakePhase, impulseDuration;
    Vector3 velocity;
    Vector3 follow;
    void Start() { follow = player.transform.position; }
    void OnEnable() => CombatSignals.Fired += OnShot;
    void OnDisable() { CombatSignals.Fired -= OnShot; CurrentShakeOffset = Vector2.zero; }
    void OnShot(Gunshot shot)
    {
        if (player == null || shot.Shooter != player.transform) return;
        Impulse(shakeStrength * feedbackIntensity * (shot.Weapon == CharacterWeapon.Shotgun ? 1.5f : 1), shakeDuration);
        ShakeCount++;
    }
    public void Impulse(float amplitude, float duration)
    {
        shakeRemaining = Mathf.Max(shakeRemaining, duration);
        impulseDuration = shakeRemaining;
        shakeAmplitude = Mathf.Min(0.3f, Mathf.Max(shakeAmplitude * 0.7f, amplitude));
        shakePhase += 1.7f;
    }
    public Vector2 ScreenToAimWorld(Vector2 screen) => (Vector2)GetComponent<Camera>().ScreenToWorldPoint(screen) - CurrentShakeOffset;
    void LateUpdate()
    {
        if (player == null) return;
        Vector3 target = player.transform.position + (Vector3)player.AimDirection * 1.1f + (Vector3)Vector2.ClampMagnitude(player.TravelVelocity * 0.035f, 0.4f);
        follow = Vector3.SmoothDamp(follow, target, ref velocity, 0.085f);
        shakeRemaining = Mathf.Max(0, shakeRemaining - Time.deltaTime);
        float envelope = impulseDuration > 0 ? Mathf.Clamp01(shakeRemaining / impulseDuration) : 0;
        CurrentShakeOffset = new Vector2(Mathf.Sin(Time.time * 103 + shakePhase), Mathf.Sin(Time.time * 137 + shakePhase * 2)) * (shakeAmplitude * envelope * envelope);
        transform.position = follow + (Vector3)CurrentShakeOffset + Vector3.back * 10;
    }
    void OnGUI()
    {
        if (player == null) return;
        GUI.Box(new Rect(18, 18, 465, 112), "TRAINING");
        GUI.Label(new Rect(30, 43, 440, 25), "WASD bewegen   Muis richten   Klik aanval   Shift/Spatie dash");
        GUI.Label(new Rect(30, 66, 440, 25), "E / Rechtsklik  oppakken     Q  neerleggen     R  herladen");
        string status = player.Weapon.IsMelee ? player.Weapon.Name : $"{player.Weapon.Name}   {player.Ammo:00} / {player.magazineSize}";
        GUI.Label(new Rect(30, 91, 440, 25), player.Reloading ? status + "    HERLADEN…" : status);
        if (player.NearbyWeapon != null)
            GUI.Box(new Rect(Screen.width / 2 - 165, Screen.height - 70, 330, 32), "E  OPPAKKEN  /  " + CharacterWeaponSettings.For(player.NearbyWeapon.kind).Name);
        var camera = GetComponent<Camera>();
        foreach (var pickup in WorldWeapon.Available)
        {
            if (pickup == null || pickup.Collected) continue;
            Vector3 label = camera.WorldToScreenPoint(pickup.transform.position + Vector3.down * 0.55f);
            if (label.z > 0) GUI.Label(new Rect(label.x - 60, Screen.height - label.y, 140, 24), CharacterWeaponSettings.For(pickup.kind).Name);
        }
        if (Mouse.current == null) return;
        Vector2 p = Mouse.current.position.ReadValue();
        p.y = Screen.height - p.y;
        GUI.color = new Color(0.4f, 1, 0.85f);
        GUI.DrawTexture(new Rect(p.x - 7, p.y, 15, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(p.x, p.y - 7, 1, 15), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
