using UnityEngine;
using UnityEngine.InputSystem;

public class GoofySoundtrack : MonoBehaviour
{
    static GoofySoundtrack instance;
    public AudioSource Source { get; private set; }
    public float Volume { get; private set; }
    public bool Muted { get; private set; }
    float duckUntil;
    public static GoofySoundtrack EnsurePlaying()
    {
        if (instance != null) return instance;
        return new GameObject("Soundtrack - Shopping Cart Parade").AddComponent<GoofySoundtrack>();
    }
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Soundtrack.Volume", 0.24f));
        Source = gameObject.AddComponent<AudioSource>();
        Source.playOnAwake = false; Source.loop = true; Source.spatialBlend = 0;
        Source.priority = 160; Source.clip = Resources.Load<AudioClip>("Audio/GoofyShuffle");
        Source.volume = 0;
        if (Source.clip != null) Source.Play();
        else Debug.LogError("Missing soundtrack: Resources/Audio/GoofyShuffle", this);
    }
    void OnEnable() => CombatSignals.Fired += OnShot;
    void OnDisable() => CombatSignals.Fired -= OnShot;
    void OnShot(Gunshot shot) { if (shot.FromPlayer) duckUntil = Time.unscaledTime + 0.12f; }
    public void SetVolume(float value) { Volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Soundtrack.Volume", Volume); }
    public void SetMuted(bool value) => Muted = value;
    void Update()
    {
        if (Application.isFocused && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) Muted = !Muted;
        float target = Muted ? 0 : Volume * (Time.unscaledTime < duckUntil ? 0.65f : 1);
        Source.volume = Mathf.MoveTowards(Source.volume, target, Time.unscaledDeltaTime * 1.2f);
    }
    void OnGUI()
    {
        var matrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float y = Screen.height / scale - 43;
        GUI.Box(new Rect(18, y - 5, 280, 36), GUIContent.none);
        if (GUI.Button(new Rect(25, y, 88, 25), Muted ? "M: UNMUTE" : "M: MUSIC")) Muted = !Muted;
        float value = GUI.HorizontalSlider(new Rect(123, y + 8, 120, 20), Volume, 0, 1);
        if (Mathf.Abs(value - Volume) > 0.001f) SetVolume(value);
        GUI.Label(new Rect(252, y + 2, 45, 24), Mathf.RoundToInt(Volume * 100) + "%");
        GUI.matrix = matrix;
    }
    void OnDestroy() { if (instance == this) instance = null; }
}
