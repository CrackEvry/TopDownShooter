using System.Collections.Generic;
using UnityEngine;

// Pure scoring rules, separate from rendering and sound. Only confirmed kills
// advance the chain; firing into empty space never earns points.
public sealed class CombatStyle
{
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public int Multiplier => Mathf.Clamp(1 + (Combo - 1) / 3, 1, 5);
    public float ExpiresAt { get; private set; }
    public const float Window = 4;
    readonly HashSet<object> defeated = new HashSet<object>();
    CharacterWeapon previousWeapon;
    bool hasWeapon;
    public string Rank => Combo >= 12 ? "S / UNSTOPPABLE" : Combo >= 8 ? "A / RELENTLESS" : Combo >= 5 ? "B / BRUTAL" : Combo >= 3 ? "C / HEATED" : Combo > 0 ? "D / WARMING UP" : "READY";
    public void Tick(float now) { if (Combo > 0 && now >= ExpiresAt) Break(); }
    public int Kill(object victim, CharacterWeapon weapon, float now, out string label)
    {
        label = "";
        if (!defeated.Add(victim)) return 0;
        Tick(now);
        bool varied = hasWeapon && previousWeapon != weapon;
        Combo++; BestCombo = Mathf.Max(BestCombo, Combo); ExpiresAt = now + Window;
        bool melee = weapon == CharacterWeapon.Knife || weapon == CharacterWeapon.Unarmed;
        int points = (100 + (melee ? 50 : 0) + (varied ? 50 : 0)) * Multiplier;
        label = melee ? "CLOSE CALL" : "TAKEDOWN";
        if (varied) label += " + SWITCH";
        if (Combo > 1) label += " / " + Combo + " CHAIN";
        Score += points; previousWeapon = weapon; hasWeapon = true; return points;
    }
    public void Break() { Combo = 0; ExpiresAt = 0; hasWeapon = false; }
    public void Reset() { Score = BestCombo = 0; Break(); defeated.Clear(); }
}
