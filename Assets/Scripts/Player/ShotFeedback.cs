using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ShotFeedback : MonoBehaviour
{
    public AudioClip pistol, shotgun, automatic;
    [Range(0, 1)] public float volume = 0.35f;
    public bool spatial;
    public int PlayedShots { get; private set; }
    AudioSource source;
    void Awake()
    {
        source = GetComponent<AudioSource>(); source.playOnAwake = false;
        source.spatialBlend = spatial ? 0.8f : 0; source.dopplerLevel = 0;
        source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 2; source.maxDistance = 22;
    }
    void OnEnable() => CombatSignals.Fired += OnShot;
    void OnDisable() => CombatSignals.Fired -= OnShot;
    void OnShot(Gunshot shot)
    {
        if (shot.Shooter != transform) return;
        AudioClip clip = shot.Weapon == CharacterWeapon.Shotgun ? shotgun : shot.Weapon == CharacterWeapon.Automatic ? automatic : pistol;
        if (clip == null) return;
        source.pitch = Random.Range(0.96f, 1.04f);
        source.PlayOneShot(clip, volume); PlayedShots++;
    }
}
