using System;
using UnityEngine;

public readonly struct Gunshot
{
    public readonly Transform Shooter;
    public readonly Vector2 Position;
    public readonly CharacterWeapon Weapon;
    public readonly bool FromPlayer;
    public Gunshot(Transform shooter, Vector2 position, CharacterWeapon weapon, bool fromPlayer)
    { Shooter = shooter; Position = position; Weapon = weapon; FromPlayer = fromPlayer; }
}

public static class CombatSignals
{
    public static event Action<Gunshot> Fired;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Fired = null;
    public static void Emit(Gunshot shot) => Fired?.Invoke(shot);
}

public readonly struct ShotPath
{
    public readonly Vector2 Origin, End;
    public readonly Collider2D Hit;
    public readonly bool BarrelBlocked;
    public ShotPath(Vector2 origin, Vector2 end, Collider2D hit, bool blocked)
    { Origin = origin; End = end; Hit = hit; BarrelBlocked = blocked; }
}

public static class CombatBallistics
{
    static readonly RaycastHit2D[] hits = new RaycastHit2D[128];
    public static Collider2D Cast(Vector2 origin, Vector2 direction, float range, Transform owner, LayerMask mask, out Vector2 point, bool wallsOnly = false)
    {
        var filter = new ContactFilter2D(); filter.SetLayerMask(mask); filter.useTriggers = false;
        int count = Physics2D.Raycast(origin, direction, filter, hits, range);
        float distance = range; Collider2D closest = null; point = origin + direction * range;
        for (int i = 0; i < count; i++)
        {
            var collider = hits[i].collider;
            if (collider == null || (owner != null && collider.transform.IsChildOf(owner))) continue;
            if (wallsOnly && !IsWall(collider)) continue;
            if (hits[i].distance > distance) continue;
            distance = hits[i].distance; point = hits[i].point; closest = collider;
        }
        return closest;
    }
    public static bool IsWall(Collider2D collider) => !collider.isTrigger && collider.attachedRigidbody == null && collider.GetComponentInParent<CharacterTestTarget>() == null;
    public static bool WallBetween(Vector2 from, Vector2 to)
    { Vector2 delta = to - from; return delta.sqrMagnitude > 0.0001f && Cast(from, delta.normalized, delta.magnitude, null, ~0, out _, true) != null; }
    public static ShotPath Resolve(Transform owner, Vector2 body, Vector2 muzzle, Vector2 direction, float range, LayerMask mask)
    {
        Vector2 barrel = muzzle - body;
        var obstruction = Cast(body, barrel.normalized, barrel.magnitude, owner, mask, out Vector2 blockedPoint);
        if (obstruction != null) return new ShotPath(blockedPoint, blockedPoint, obstruction, true);
        var hit = Cast(muzzle, direction.normalized, range, owner, mask, out Vector2 end);
        return new ShotPath(muzzle, end, hit, false);
    }
    public static void DrawTracer(ShotPath path, Material material, Color color)
    {
        if (path.BarrelBlocked) return;
        var trace = new GameObject("Shot tracer - muzzle to impact").AddComponent<LineRenderer>();
        trace.sharedMaterial = material; trace.sortingOrder = 8;
        trace.startWidth = 0.035f; trace.endWidth = 0.012f;
        trace.startColor = color; trace.endColor = new Color(color.r, color.g, color.b, 0.3f);
        trace.positionCount = 2; trace.SetPosition(0, path.Origin); trace.SetPosition(1, path.End);
        UnityEngine.Object.Destroy(trace.gameObject, 0.055f);
    }
}
