using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum NpcDisposition { Flee, Fight, Hide }
public enum NpcState { Idle, Observe, React, Flee, Escaped, SeekCover, Hide, Investigate, Aim, Attack, Reload, Stunned, Dead }

public class NpcRoom : MonoBehaviour
{
    public Rect area = new Rect(-12, -8, 24, 16);
    [Range(0, 100)] public float fleeWeight = 90, fightWeight = 5, hideWeight = 5;
    public int seed = 2309;
    public NpcNavigationGrid navigation;
    public Transform[] exits, cover;
    public bool showDebug;
    public NpcBrain[] Population { get; private set; }
    int resetCount;
    void OnEnable() => CombatSignals.Fired += HearShot;
    void OnDisable() => CombatSignals.Fired -= HearShot;
    void Start() => ResetEncounter();
    public void ResetEncounter()
    {
        BloodEffects.Clear();
        FindFirstObjectByType<ActionPlayer>()?.Feedback?.ResetEncounter();
        Population = GetComponentsInChildren<NpcBrain>(true);
        var deck = BuildReactionDeck(Population.Length, fleeWeight, fightWeight, hideWeight, seed + resetCount++);
        for (int i = 0; i < Population.Length; i++) { Population[i].room = this; Population[i].ResetEncounter(deck[i]); }
        if (navigation != null) navigation.Bake();
    }
    public static NpcDisposition[] BuildReactionDeck(int count, float flee, float fight, float hide, int seed)
    {
        var weights = new[] { Mathf.Max(0, flee), Mathf.Max(0, fight), Mathf.Max(0, hide) };
        float sum = weights[0] + weights[1] + weights[2]; if (sum <= 0) { weights[0] = 1; sum = 1; }
        var counts = new int[3]; var fractions = new float[3]; int assigned = 0;
        for (int i = 0; i < 3; i++) { float quota = count * weights[i] / sum; counts[i] = Mathf.FloorToInt(quota); fractions[i] = quota - counts[i]; assigned += counts[i]; }
        while (assigned++ < count)
        { int largest = 0; for (int i = 1; i < 3; i++) if (fractions[i] > fractions[largest]) largest = i; counts[largest]++; fractions[largest] = -1; }
        var deck = new NpcDisposition[count]; int index = 0;
        for (int i = 0; i < 3; i++) for (int k = 0; k < counts[i]; k++) deck[index++] = (NpcDisposition)i;
        var random = new System.Random(seed);
        for (int i = count - 1; i > 0; i--) { int j = random.Next(i + 1); var swap = deck[i]; deck[i] = deck[j]; deck[j] = swap; }
        return deck;
    }
    void HearShot(Gunshot shot)
    {
        if (!shot.FromPlayer || Population == null || shot.Shooter == null) return;
        var player = shot.Shooter.GetComponent<ActionPlayer>();
        foreach (var npc in Population)
        {
            if (npc == null || !npc.isActiveAndEnabled) continue;
            float distance = Vector2.Distance(npc.transform.position, shot.Position);
            bool sameRoom = area.Contains(shot.Shooter.position);
            float audible = CombatBallistics.WallBetween(npc.transform.position, shot.Position) ? npc.hearingRadius * 0.5f : npc.hearingRadius;
            if (sameRoom || distance <= audible) npc.HearThreat(player, shot.Position);
        }
    }
    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.f2Key.wasPressedThisFrame) showDebug = !showDebug;
        if (Keyboard.current.f6Key.wasPressedThisFrame) ResetEncounter();
    }
    void OnGUI()
    {
        if (!showDebug || Population == null || Camera.main == null) return;
        GUI.Box(new Rect(Screen.width - 330, 18, 312, 60), "NPC DEBUG   F2 verbergen / F6 reset");
        GUI.Label(new Rect(Screen.width - 318, 45, 300, 24), $"Verdeling: {fleeWeight:0}% vlucht / {fightWeight:0}% vecht / {hideWeight:0}% dekking");
        foreach (var npc in Population)
        {
            if (npc == null || npc.State == NpcState.Escaped) continue;
            Vector3 p = Camera.main.WorldToScreenPoint(npc.transform.position + Vector3.up * 0.7f);
            if (p.z < 0) continue;
            GUI.color = npc.Disposition == NpcDisposition.Fight ? new Color(1, 0.5f, 0.45f) : npc.Disposition == NpcDisposition.Hide ? new Color(0.65f, 0.7f, 1) : new Color(1, 0.88f, 0.5f);
            GUI.Label(new Rect(p.x - 72, Screen.height - p.y, 160, 24), $"{npc.name}: {npc.State}");
        }
        GUI.color = Color.white;
    }
}
