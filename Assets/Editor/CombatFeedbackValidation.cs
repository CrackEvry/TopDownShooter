using System;
using UnityEngine;

public static class CombatFeedbackValidation
{
    public static void RunRules()
    {
        var style = new CombatStyle();
        Check(style.Kill(1, CharacterWeapon.Pistol, 0, out _) == 100 && style.Combo == 1, "First kill awards 100");
        Check(style.Kill(1, CharacterWeapon.Pistol, 1, out _) == 0 && style.Combo == 1, "Same victim never awards twice");
        style.Kill(2, CharacterWeapon.Pistol, 1, out _);
        Check(style.Kill(3, CharacterWeapon.Knife, 2, out _) == 200, "Melee and weapon-switch bonuses");
        Check(style.Kill(4, CharacterWeapon.Knife, 3, out _) == 300 && style.Multiplier == 2, "Fourth kill increases multiplier");
        int score = style.Score; style.Tick(7);
        Check(style.Combo == 0 && style.Score == score && style.BestCombo == 4, "Timeout clears chain, preserves score and best");
        Check(style.Kill(5, CharacterWeapon.Pistol, 8, out _) == 100, "Expired chain has no stale weapon bonus");
        style.Break(); Check(style.Combo == 0 && style.Score > 0, "Damage breaks chain without removing score");
        for (int i = 10; i < 35; i++) style.Kill(i, CharacterWeapon.Pistol, 9, out _);
        Check(style.Multiplier == 5, "Multiplier capped at five");
        style.Reset(); Check(style.Score == 0 && style.Combo == 0 && style.BestCombo == 0, "Encounter resets score");
        Check(style.Kill(1, CharacterWeapon.Pistol, 10, out _) == 100, "Reset permits the same NPC in a new encounter");
        Debug.Log("COMBAT_STYLE_RULES_OK: 10 checks");
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception("Feedback check failed: " + message); Debug.Log("PASS: " + message); }
}
