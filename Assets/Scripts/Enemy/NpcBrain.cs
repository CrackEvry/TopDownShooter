using System.Collections.Generic;
using Shooter.AI;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class NpcBrain : MonoBehaviour
{
    public Texture2D characterSheet;
    [Range(0, 4)] public int characterVariant = 1;
    public NpcRoom room;
    public float walkSpeed = 1.8f, runSpeed = 4.6f, sightRadius = 12, hearingRadius = 20, memorySeconds = 10;
    public int maxHealth = 2;
    [SerializeField] NpcDisposition disposition;
    [SerializeField] NpcState state;
    [SerializeField] string reason;
    [SerializeField] int health;
    [SerializeField] bool canSeePlayer, alarmed;
    [SerializeField] Vector2 lastKnownPosition;
    public NpcDisposition Disposition => disposition;
    public NpcState State => state;
    public string Reason => reason;
    public bool CanSeePlayer => canSeePlayer;
    public Vector2 LastKnownPosition => lastKnownPosition;
    public BehaviourNode Tree { get; private set; }
    public IReadOnlyList<Vector2> Route => route;
    public int ShotsFired { get; private set; }
    public int Health => health;
    public ShotPath LastShot { get; private set; }
    public CharacterBodyVisual Visual { get; private set; }
    Rigidbody2D body;
    CircleCollider2D hitbox;
    CharacterSpriteAtlas atlas;
    ActionPlayer player;
    Vector2 home, aim = Vector2.down, previousPosition, actualVelocity, destination;
    readonly List<Vector2> route = new List<Vector2>(), candidate = new List<Vector2>();
    readonly Collider2D[] neighbours = new Collider2D[24];
    int waypoint, ammo = 8;
    float desiredSpeed, nextThink, lastThreatTime, reactUntil, stunnedUntil, nextRepath, aimUntil, nextShot, reloadUntil, nextIdleGoal, recoil, flashUntil;
    bool hasGoal;
    float stuckSeconds;
    float hitFlashUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<CircleCollider2D>();
        body.gravityScale = 0; body.freezeRotation = true; body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; hitbox.radius = 0.27f;
        home = previousPosition = body.position; health = maxHealth;
        player = FindFirstObjectByType<ActionPlayer>();
        if (characterSheet != null) { atlas = new CharacterSpriteAtlas(characterSheet, characterVariant); Visual = new CharacterBodyVisual(transform, atlas); }
        BuildTree(); nextThink = Time.time + Random.Range(0, 0.1f);
    }
    void BuildTree()
    {
        Tree = new Selector("NPC priorities",
            new Sequence("Dead or escaped", new Condition("Terminal state?", () => state == NpcState.Dead || state == NpcState.Escaped), new TaskNode("Stop permanently", () => { Stop(); return NodeStatus.Success; })),
            new Sequence("Injured", new Condition("Still stunned?", () => Time.time < stunnedUntil), new TaskNode("Brief hit reaction", () => Hold(NpcState.Stunned, "Recovering from a hit"))),
            new Sequence("Threat response", new Condition("Remember a threat?", () => alarmed),
                new Selector("Response by temperament",
                    new Sequence("Startle", new Condition("Reaction delay?", () => Time.time < reactUntil), new TaskNode("Assess gunshot", () => Hold(NpcState.React, "Heard a shot; reacting"))),
                    new Sequence("Flee branch", new Condition("Flee temperament?", () => disposition == NpcDisposition.Flee), new TaskNode("Reach a safe exit", Flee)),
                    new Sequence("Fight branch", new Condition("Fight temperament?", () => disposition == NpcDisposition.Fight),
                        new Selector("Combat priorities",
                            new Sequence("Lost sight", new Condition("Cannot see target?", () => !canSeePlayer), new TaskNode("Investigate last known position", Investigate)),
                            new Sequence("Outside range", new Condition("More than seven units away?", () => Vector2.Distance(body.position, player.transform.position) > 7), new TaskNode("Approach to weapon range", Approach)),
                            new Sequence("Empty magazine", new Condition("Need reload?", () => ammo == 0 || reloadUntil > 0), new TaskNode("Reload before firing", Reload)),
                            new TaskNode("Aim, check friendly obstruction, fire", Fight))),
                    new TaskNode("Find occluded cover; flee if none", Hide))),
            new Sequence("Observe", new Condition("See the visitor?", () => canSeePlayer), new TaskNode("Watch without attacking", () => { aim = ((Vector2)player.transform.position - body.position).normalized; return Hold(NpcState.Observe, "Visitor visible; no violence detected"); })),
            new TaskNode("Idle and short patrol", Idle));
    }
    public void ResetEncounter(NpcDisposition role)
    {
        disposition = role; health = maxHealth; state = NpcState.Idle; reason = "No known threat";
        alarmed = canSeePlayer = false; hasGoal = false; route.Clear(); waypoint = 0; ammo = 8;
        reactUntil = stunnedUntil = reloadUntil = nextShot = 0; desiredSpeed = 0; ShotsFired = 0;
        stuckSeconds = 0;
        body.position = home; transform.position = home; body.linearVelocity = Vector2.zero; previousPosition = home;
        hitbox.enabled = true; aim = Vector2.down;
        hitFlashUntil = 0;
        if (Visual != null) { Visual.Root.gameObject.SetActive(true); Visual.RestoreAlive(); Visual.Root.localRotation = Quaternion.identity; Visual.Torso.color = Color.white; }
        nextIdleGoal = Time.time + Random.Range(1, 4);
    }
    void Update()
    {
        recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 2);
        if (Time.time < nextThink) return;
        nextThink = Time.time + 0.1f; Think();
    }
    public void Think()
    {
        if (room == null || room.navigation == null || Tree == null) return;
        Sense();
        Tree.ResetStatus(); Tree.Tick();
    }
    void Sense()
    {
        if (player == null) player = FindFirstObjectByType<ActionPlayer>();
        canSeePlayer = false;
        if (player == null || state == NpcState.Dead || state == NpcState.Escaped) return;
        var vitality = player.GetComponent<PlayerVitality>();
        if (vitality != null && vitality.IsDead) return;
        Vector2 delta = (Vector2)player.transform.position - body.position;
        canSeePlayer = delta.magnitude <= sightRadius && (alarmed || Vector2.Angle(aim, delta) < 110) && !CombatBallistics.WallBetween(body.position, player.transform.position);
        if (alarmed && canSeePlayer) { lastKnownPosition = player.transform.position; lastThreatTime = Time.time; }
        if (alarmed && Time.time - lastThreatTime > memorySeconds)
        { alarmed = false; hasGoal = false; route.Clear(); nextIdleGoal = Time.time + 1; }
    }
    public void HearThreat(ActionPlayer source, Vector2 position)
    {
        if (state == NpcState.Dead || state == NpcState.Escaped) return;
        if (source != null) player = source;
        if (!alarmed) { reactUntil = Time.time + Random.Range(0.18f, 0.55f); hasGoal = false; }
        alarmed = true; lastThreatTime = Time.time; lastKnownPosition = position;
    }
    NodeStatus Hold(NpcState value, string why) { Stop(); SetState(value, why); return NodeStatus.Running; }
    void Stop() { desiredSpeed = 0; route.Clear(); hasGoal = false; body.linearVelocity = Vector2.zero; }
    void SetState(NpcState value, string why) { state = value; reason = why; }

    NodeStatus Idle()
    {
        if (Time.time < nextIdleGoal) return Hold(NpcState.Idle, "Waiting before a short patrol");
        if (!hasGoal)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 goal = home + Random.insideUnitCircle * 1.3f;
                if (room.navigation.IsWalkable(goal) && Navigate(goal, walkSpeed, NpcState.Idle, "Short local patrol")) break;
            }
            if (!hasGoal) nextIdleGoal = Time.time + 2;
        }
        else if (Vector2.Distance(body.position, destination) < 0.45f)
        { Stop(); nextIdleGoal = Time.time + Random.Range(1.5f, 4); }
        return NodeStatus.Running;
    }
    NodeStatus Flee()
    {
        if (hasGoal && state == NpcState.Flee && Vector2.Distance(body.position, destination) < 0.5f)
        {
            Stop(); SetState(NpcState.Escaped, "Reached an exit beyond the room");
            hitbox.enabled = false; Visual?.Root.gameObject.SetActive(false); return NodeStatus.Success;
        }
        if (!hasGoal || state != NpcState.Flee)
        {
            float best = float.NegativeInfinity; Vector2 goal = body.position; bool found = false;
            if (room.exits != null) foreach (var exit in room.exits)
            {
                if (exit == null || !room.navigation.FindPath(body.position, exit.position, candidate)) continue;
                float score = Vector2.Distance(exit.position, lastKnownPosition) * 1.3f - candidate.Count * room.navigation.cellSize;
                if (score > best) { best = score; goal = exit.position; found = true; }
            }
            if (!found) return SeekCover(false);
            if (!Navigate(goal, runSpeed, NpcState.Flee, "Running to a reachable exit")) return SeekCover(false);
        }
        else Navigate(destination, runSpeed, NpcState.Flee, "Following escape route");
        return NodeStatus.Running;
    }
    NodeStatus Hide() => SeekCover(true);
    NodeStatus SeekCover(bool allowEscape)
    {
        if (hasGoal && (state == NpcState.SeekCover || state == NpcState.Hide) && CombatBallistics.WallBetween(lastKnownPosition, destination))
        {
            if (Vector2.Distance(body.position, destination) < 0.45f)
            { desiredSpeed = 0; body.linearVelocity = Vector2.zero; SetState(NpcState.Hide, "Occluded by solid cover; waiting"); }
            else Navigate(destination, runSpeed, NpcState.SeekCover, "Moving behind cover");
            return NodeStatus.Running;
        }
        float best = float.PositiveInfinity; Vector2 goal = body.position; bool found = false;
        if (room.cover != null) foreach (var spot in room.cover)
        {
            if (spot == null || !CombatBallistics.WallBetween(lastKnownPosition, spot.position) || !room.navigation.IsWalkable(spot.position)) continue;
            if (!room.navigation.FindPath(body.position, spot.position, candidate)) continue;
            float cost = candidate.Count * room.navigation.cellSize;
            if (cost < best) { best = cost; goal = spot.position; found = true; }
        }
        if (found && Navigate(goal, runSpeed, NpcState.SeekCover, "Selected cover that blocks the threat")) return NodeStatus.Running;
        if (allowEscape) return Flee();
        return Hold(NpcState.Hide, "No reachable exit or cover; holding position");
    }
    NodeStatus Investigate()
    {
        aim = (lastKnownPosition - body.position).normalized;
        if (Vector2.Distance(body.position, lastKnownPosition) < 0.8f) return Hold(NpcState.Investigate, "Checking the last seen/heard position");
        if (!Navigate(lastKnownPosition, walkSpeed * 1.4f, NpcState.Investigate, "No sight: follow last known position only")) return Hold(NpcState.Investigate, "Last known position is unreachable");
        return NodeStatus.Running;
    }
    NodeStatus Approach()
    {
        if (!Navigate(lastKnownPosition, walkSpeed * 1.5f, NpcState.Investigate, "Approaching weapon range")) return Hold(NpcState.Investigate, "No approach route");
        return NodeStatus.Running;
    }
    NodeStatus Reload()
    {
        if (reloadUntil == 0) reloadUntil = Time.time + 1.5f;
        if (Time.time >= reloadUntil) { ammo = 8; reloadUntil = 0; }
        return Hold(NpcState.Reload, "Reloading; cannot fire");
    }
    NodeStatus Fight()
    {
        aim = ((Vector2)player.transform.position - body.position).normalized;
        if (state != NpcState.Aim && state != NpcState.Attack) aimUntil = Time.time + 0.45f;
        Stop(); SetState(NpcState.Aim, "Aiming with a reaction delay and clear line of fire");
        if (Time.time >= aimUntil && Time.time >= nextShot && Fire()) SetState(NpcState.Attack, "Firing at a visible threat");
        return NodeStatus.Running;
    }
    bool Fire()
    {
        if (Visual == null || player == null) return false;
        Visual.Pose(0, Vector2.zero, aim, CharacterWeapon.Pistol, 0, -1, false, 5.6f);
        for (int i = 0; i < 3; i++) { aim = ((Vector2)player.transform.position - (Vector2)Visual.Muzzle.position).normalized; Visual.Pose(0, Vector2.zero, aim, CharacterWeapon.Pistol, 0, -1, false, 5.6f); }
        Vector2 muzzle = Visual.Muzzle.position;
        Vector2 toPlayer = (Vector2)player.transform.position - muzzle;
        var first = CombatBallistics.Cast(muzzle, toPlayer.normalized, toPlayer.magnitude + 0.5f, transform, ~0, out _);
        if (first == null || first.GetComponentInParent<ActionPlayer>() != player) return false; // Do not fire through a bystander.
        var guard = CombatBallistics.Resolve(transform, body.position, muzzle, aim, 14, ~0);
        if (guard.BarrelBlocked) return false;
        recoil = 0.07f;
        Visual.Pose(0, Vector2.zero, aim, CharacterWeapon.Pistol, recoil, -1, true, 5.6f);
        muzzle = Visual.Muzzle.position;
        Vector2 direction = Quaternion.Euler(0, 0, Random.Range(-3, 3)) * aim;
        LastShot = CombatBallistics.Resolve(transform, body.position, muzzle, direction, 14, ~0);
        LastShot.Hit?.GetComponentInParent<PlayerVitality>()?.TakeDamage(1);
        CombatBallistics.DrawTracer(LastShot, atlas.TraceMaterial, new Color(1, 0.45f, 0.35f));
        ammo--; ShotsFired++; nextShot = Time.time + 0.8f; flashUntil = Time.time + 0.05f;
        CombatSignals.Emit(new Gunshot(transform, muzzle, CharacterWeapon.Pistol, false)); return true;
    }
    bool Navigate(Vector2 goal, float speed, NpcState movingState, string why)
    {
        if (!hasGoal || (goal - destination).sqrMagnitude > 0.3f || Time.time >= nextRepath)
        {
            if (!room.navigation.FindPath(body.position, goal, route)) { hasGoal = false; desiredSpeed = 0; return false; }
            destination = route[route.Count - 1]; waypoint = 0; hasGoal = true; nextRepath = Time.time + 1.1f;
        }
        desiredSpeed = speed; SetState(movingState, why); return true;
    }
    void FixedUpdate()
    {
        actualVelocity = (body.position - previousPosition) / Time.fixedDeltaTime; previousPosition = body.position;
        if (state == NpcState.Dead || state == NpcState.Escaped || desiredSpeed <= 0 || waypoint >= route.Count) { body.linearVelocity = Vector2.zero; return; }
        while (waypoint < route.Count && Vector2.Distance(body.position, route[waypoint]) < 0.17f) waypoint++;
        if (waypoint >= route.Count) { body.linearVelocity = Vector2.zero; return; }
        // Do not insist on touching an old cell centre after another person has
        // pushed us past it. Look ahead only across a body-width clear segment.
        for (int i = waypoint + 1; i < route.Count && i <= waypoint + 6; i++)
        {
            if (Vector2.Distance(body.position, route[i]) > 1.7f) break;
            if (room.navigation.CanTravelDirect(body.position, route[i])) waypoint = i;
        }
        Vector2 direction = (route[waypoint] - body.position).normalized;
        var filter = new ContactFilter2D(); filter.SetLayerMask(~0); filter.useTriggers = false;
        int count = Physics2D.OverlapCircle(body.position, 0.75f, filter, neighbours);
        Vector2 separation = Vector2.zero;
        Vector2 sideStep = Vector2.zero;
        Vector2 right = new Vector2(direction.y, -direction.x);
        for (int i = 0; i < count; i++)
        {
            var rb = neighbours[i].attachedRigidbody;
            if (rb == null || rb == body) continue;
            Vector2 away = body.position - rb.position;
            if (away.sqrMagnitude > 0.001f) separation += away.normalized * Mathf.Clamp01((0.7f - away.magnitude) / 0.7f);
            Vector2 towards = -away;
            if (Vector2.Dot(towards, direction) > 0.05f && Mathf.Abs(Vector2.Dot(towards, right)) < 0.5f)
            {
                Vector2 passingSide = room.navigation.CanTravelDirect(body.position, body.position + right * 0.65f) ? right : -right;
                if (room.navigation.CanTravelDirect(body.position, body.position + passingSide * 0.65f)) sideStep += passingSide * 0.6f;
            }
        }
        Vector2 steering = direction + Vector2.ClampMagnitude(separation, 1) * 0.8f + Vector2.ClampMagnitude(sideStep, 0.65f);
        if (!room.navigation.CanTravelDirect(body.position, body.position + steering.normalized * 0.35f)) steering = direction * 0.7f;
        body.linearVelocity = Vector2.ClampMagnitude(steering, 1) * desiredSpeed;
        stuckSeconds = actualVelocity.magnitude < 0.2f ? stuckSeconds + Time.fixedDeltaTime : 0;
        if (stuckSeconds > 0.8f) { nextRepath = 0; stuckSeconds = 0; }
        if (state != NpcState.Aim && state != NpcState.Attack) aim = direction;
    }
    void LateUpdate()
    {
        if (state == NpcState.Dead || state == NpcState.Escaped || Visual == null) return;
        var weapon = disposition == NpcDisposition.Fight && alarmed ? CharacterWeapon.Pistol : CharacterWeapon.Unarmed;
        Visual.Pose(Time.deltaTime, actualVelocity, aim, weapon, recoil, -1, Time.time < flashUntil, 5.6f);
        Visual.Torso.color = Time.time < hitFlashUntil ? new Color(1, 0.15f, 0.2f) : Color.white;
    }
    public void TakeDamage(int amount, ActionPlayer attacker)
    {
        if (health <= 0 || state == NpcState.Escaped || amount <= 0) return;
        HearThreat(attacker, attacker != null ? (Vector2)attacker.transform.position : body.position);
        int actualDamage = Mathf.Min(health, amount);
        health = Mathf.Max(0, health - amount);
        Vector2 impactDirection = attacker != null ? ((Vector2)transform.position - (Vector2)attacker.transform.position).normalized : Vector2.up;
        BloodEffects.Spawn(transform.position, impactDirection, health == 0, attacker != null && (attacker.EquippedWeapon == CharacterWeapon.Shotgun || attacker.EquippedWeapon == CharacterWeapon.Knife));
        hitFlashUntil = Time.time + 0.1f;
        if (attacker != null) attacker.Feedback?.ConfirmHit(transform.position, actualDamage, health == 0, this, attacker.EquippedWeapon);
        if (health > 0) { stunnedUntil = Time.time + 0.22f; return; }
        Stop(); SetState(NpcState.Dead, "Health depleted"); hitbox.enabled = false;
        Visual?.ShowCorpse(impactDirection);
    }
    void OnDisable() { if (body != null) body.linearVelocity = Vector2.zero; }
    void OnDestroy() => atlas?.Dispose();
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 1; i < route.Count; i++) Gizmos.DrawLine(route[i - 1], route[i]);
        if (alarmed) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(lastKnownPosition, 0.3f); }
        Gizmos.color = new Color(1, 1, 0, 0.25f); Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}
