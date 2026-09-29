using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Shooter.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class NpcLabValidation
{
    const string Key = "NpcLabValidation.Running";
    static IEnumerator<float> checks;
    static double nextTick;
    static NpcRoom room;
    static ActionPlayer player;
    static SimulationMode2D previousMode;
    static int count;
    static string output;
    static readonly MethodInfo Motor = typeof(NpcBrain).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly MethodInfo Animate = typeof(NpcBrain).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
    static NpcLabValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { checks = (SessionState.GetBool("FeedbackPreview", false) ? Preview() : Checks()).GetEnumerator(); nextTick = EditorApplication.timeSinceStartup + 1; EditorApplication.update += Tick; }
        };
    }
    public static void Run()
    {
        NpcLabBuilder.Build();
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    public static void RunFeedbackPreview()
    {
        NpcLabBuilder.Build();
        var game = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        game.Show(); game.Focus();
        SessionState.SetBool("FeedbackPreview", true);
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static IEnumerable<float> Preview()
    {
        player = UnityEngine.Object.FindFirstObjectByType<ActionPlayer>();
        room = UnityEngine.Object.FindFirstObjectByType<NpcRoom>();
        player.enabled = false; foreach (var npc in room.Population) npc.enabled = false;
        Camera.main.GetComponent<CharacterTestCamera>().enabled = false;
        Camera.main.transform.position = new Vector3(0, -1.5f, -10); Camera.main.orthographicSize = 10.5f;
        for (int i = 0; i < 5; i++) room.Population[i].TakeDamage(99, player);
        yield return 0.6f;
        bool movement = Array.IndexOf(Environment.GetCommandLineArgs(), "-movementPreview") >= 0;
        if (movement)
        {
            Warp(player.gameObject, new Vector2(0, -5)); player.enabled = true;
            player.SetMoveInput(Vector2.right); player.TryDash();
            yield return 0.06f;
        }
        string path = Path.GetFullPath("../CombatDiagnostics/" + (movement ? "movement-feedback.png" : "blood-feedback.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        ScreenCapture.CaptureScreenshot(path);
        yield return 1.5f;
        Debug.Log("FEEDBACK_PREVIEW: " + path);
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        try
        {
            if (checks.MoveNext()) { nextTick = EditorApplication.timeSinceStartup + checks.Current; return; }
            if (!SessionState.GetBool("FeedbackPreview", false))
                Debug.Log($"NPC_LAB_TESTS_OK: {count} checks. Behaviour tree, 90/5/5 assignment, hearing, pathfinding, crowd escape, cover, combat and damage.");
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code)
    {
        EditorApplication.update -= Tick; Physics2D.simulationMode = previousMode; SessionState.SetBool(Key, false);
        bool preview = SessionState.GetBool("FeedbackPreview", false); SessionState.SetBool("FeedbackPreview", false);
        if (Application.isBatchMode || preview) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception("NPC check failed: " + message); count++; Debug.Log("PASS: " + message); }
    static void Warp(GameObject obj, Vector2 position)
    {
        var body = obj.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
        obj.transform.position = position; Physics2D.SyncTransforms();
    }
    static void Noise(Vector2 position)
    {
        Warp(player.gameObject, position);
        // Pause automatic updates between assertions, but deliver the event to
        // active brains just as gameplay does (the room ignores disabled NPCs).
        var enabled = room.Population.Select(n => n.enabled).ToArray();
        foreach (var npc in room.Population) npc.enabled = true;
        CombatSignals.Emit(new Gunshot(player.transform, position, CharacterWeapon.Pistol, true));
        for (int i = 0; i < enabled.Length; i++) room.Population[i].enabled = enabled[i];
    }
    static void Simulate(float seconds)
    {
        int steps = Mathf.CeilToInt(seconds / 0.02f);
        for (int i = 0; i < steps; i++)
        {
            if (i % 5 == 0) foreach (var npc in room.Population) npc.Think();
            foreach (var npc in room.Population) Motor.Invoke(npc, null);
            Physics2D.Simulate(0.02f);
            foreach (var npc in room.Population) Animate.Invoke(npc, null);
        }
    }
    static IEnumerable<float> Checks()
    {
        count = 0;
        CombatFeedbackValidation.RunRules();
        var args = Environment.GetCommandLineArgs(); int arg = Array.IndexOf(args, "-characterLabOutput");
        output = arg >= 0 ? args[arg + 1] : Path.GetFullPath("Temp/NpcDiagnostics"); Directory.CreateDirectory(output);
        room = UnityEngine.Object.FindFirstObjectByType<NpcRoom>(); player = UnityEngine.Object.FindFirstObjectByType<ActionPlayer>();
        Require(room != null && room.Population.Length == 20, "Twenty configured NPCs");
        previousMode = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
        player.enabled = false; foreach (var npc in room.Population) npc.enabled = false;
        room.ResetEncounter();
        Require(room.Population.Count(n => n.Disposition == NpcDisposition.Flee) == 18 && room.Population.Count(n => n.Disposition == NpcDisposition.Fight) == 1 && room.Population.Count(n => n.Disposition == NpcDisposition.Hide) == 1, "Population contains eighteen flee, one fight, one hide");
        for (int size = 0; size < 25; size++)
        { var deck = NpcRoom.BuildReactionDeck(size, 90, 5, 5, 42); Require(deck.Length == size, "Population allocation size " + size); }
        Require(NpcRoom.BuildReactionDeck(3, 0, 0, 0, 1).All(r => r == NpcDisposition.Flee), "Zero weights have a safe default");
        bool urgent = false; int high = 0, low = 0;
        var tree = new Selector("test", new Sequence("high", new Condition("urgent", () => urgent), new TaskNode("react", () => { high++; return NodeStatus.Running; })), new TaskNode("idle", () => { low++; return NodeStatus.Running; }));
        tree.Tick(); urgent = true; tree.Tick();
        Require(high == 1 && low == 1, "Reactive selector interrupts a lower-priority running task");
        var route = new List<Vector2>();
        foreach (var npc in room.Population)
        {
            bool reachable = room.exits.Any(exit => room.navigation.FindPath(npc.transform.position, exit.position, route));
            Require(reachable && route.All(p => room.navigation.IsWalkable(p)), "Navigable exit from " + npc.name);
        }
        Require(room.navigation.FindPath(new Vector2(-5.5f, 1), new Vector2(-2.5f, 1), route) && route.Count > 7, "A* detours around solid cover");
        bool segmentsClear = true;
        for (int i = 1; i < route.Count; i++) segmentsClear &= !CombatBallistics.WallBetween(route[i-1], route[i]);
        Require(segmentsClear, "Path segments do not cut through cover");
        Warp(player.gameObject, new Vector2(0, -6));
        foreach (var npc in room.Population) npc.Think();
        Require(room.Population.All(n => n.ShotsFired == 0 && n.State != NpcState.Flee), "Entering without firing does not start violence");
        Capture("npc-before.png");
        Noise(new Vector2(0, -6));
        var roles = room.Population.Select(n => n.Disposition).ToArray();
        for (int i = 0; i < 4; i++) Noise(new Vector2(0, -6));
        Require(room.Population.Select(n => n.Disposition).SequenceEqual(roles), "Repeated shots never reroll temperament");
        yield return 0.65f;
        foreach (var npc in room.Population) npc.Think();
        Require(room.Population.Count(n => n.State == NpcState.Flee) == 18, "Eighteen NPCs choose escape after hearing a shot");
        Require(room.Population.First(n => n.Disposition == NpcDisposition.Hide).State == NpcState.SeekCover, "Hider selects occluded cover");
        Simulate(1.5f); Capture("npc-reacting.png");
        Simulate(14.5f);
        foreach (var npc in room.Population) npc.Think();
        int escaped = room.Population.Count(n => n.State == NpcState.Escaped);
        foreach (var npc in room.Population.Where(n => n.Disposition == NpcDisposition.Flee && n.State != NpcState.Escaped))
            Debug.Log("ESCAPE DIAGNOSTIC: " + npc.name + " " + npc.State + " " + npc.transform.position + " " + npc.Reason);
        Require(escaped == 18, "All eighteen fleeing NPCs physically reach an exit");
        var hider = room.Population.First(n => n.Disposition == NpcDisposition.Hide);
        Require(hider.State == NpcState.Hide && CombatBallistics.WallBetween(player.transform.position, hider.transform.position), "Hider reaches cover and stays occluded");
        Capture("npc-escaped.png");
        room.ResetEncounter(); Noise(new Vector2(100, 100)); foreach (var npc in room.Population) npc.Think();
        Require(room.Population.All(n => n.State == NpcState.Idle), "A distant shot outside the room does not alert everybody");
        room.ResetEncounter();
        var fighter = room.Population.First(n => n.Disposition == NpcDisposition.Fight);
        var blocker = room.Population.First(n => n != fighter);
        foreach (var npc in room.Population) if (npc != fighter) npc.GetComponent<Collider2D>().enabled = false;
        Warp(fighter.gameObject, Vector2.zero); Warp(player.gameObject, new Vector2(3, 0));
        Warp(blocker.gameObject, new Vector2(1.5f, 0)); blocker.GetComponent<Collider2D>().enabled = true;
        fighter.HearThreat(player, player.transform.position);
        yield return 0.65f; fighter.Think();
        Require(fighter.State == NpcState.Aim && fighter.ShotsFired == 0, "Fighter aims before shooting");
        yield return 0.5f; fighter.Think();
        Require(fighter.ShotsFired == 0, "Fighter refuses to shoot through a bystander");
        blocker.GetComponent<Collider2D>().enabled = false; Physics2D.SyncTransforms();
        int health = player.GetComponent<PlayerVitality>().Health;
        fighter.Think();
        Require(fighter.ShotsFired == 1 && fighter.State == NpcState.Attack, "Brave NPC really fires back");
        Require(player.GetComponent<PlayerVitality>().Health < health, "NPC bullets can damage the player");
        Require(Vector2.Distance(fighter.LastShot.Origin, fighter.Visual.Muzzle.position) < 0.0001f, "NPC shot also starts at its barrel");
        Warp(fighter.gameObject, new Vector2(-2, 1)); Warp(player.gameObject, new Vector2(-5.5f, 1));
        Vector2 remembered = new Vector2(0, -6); fighter.HearThreat(player, remembered);
        yield return 0.9f; fighter.Think();
        Require(!fighter.CanSeePlayer && fighter.LastKnownPosition == remembered && fighter.State == NpcState.Investigate, "No vision through walls or knowledge of an unseen position");
        Require(fighter.ShotsFired == 1, "No shooting through cover");
        fighter.TakeDamage(99, player); fighter.Think(); Motor.Invoke(fighter, null);
        Require(player.Feedback != null && player.Feedback.Style.Combo == 1 && player.Feedback.Style.Score > 0, "Real NPC death awards score and starts combo");
        int earned = player.Feedback.Style.Score;
        fighter.TakeDamage(99, player);
        Require(player.Feedback.Style.Score == earned, "Dead NPC cannot be farmed for score");
        Require(fighter.State == NpcState.Dead && !fighter.GetComponent<Collider2D>().enabled && fighter.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "Dead NPC stops all movement and combat");
        Require(fighter.Visual.IsCorpse && !fighter.Visual.Shoulders.gameObject.activeSelf && !fighter.Visual.Legs.gameObject.activeSelf, "Death replaces all living parts with asset-pack corpse");
        Require(BloodEffects.Count > 0, "Confirmed damage creates world-space blood");
        var vitality = player.GetComponent<PlayerVitality>(); vitality.TakeDamage(99);
        Require(player.Feedback.Style.Combo == 0 && player.Feedback.Style.Score == earned, "Actual player damage breaks combo but preserves score");
        Require(vitality.IsDead && !player.enabled, "Player can be defeated");
        vitality.Respawn(); player.enabled = false;
        Require(!vitality.IsDead && room.Population.All(n => n.Health == n.maxHealth && n.State == NpcState.Idle), "Respawn resets the encounter");
        Require(BloodEffects.Count == 0 && room.Population.All(n => !n.Visual.IsCorpse && n.Visual.Legs.gameObject.activeSelf), "Reset clears blood and restores living sprites");
        Require(player.Feedback.Style.Score == 0 && player.Feedback.Style.BestCombo == 0, "Respawn resets style HUD");
        var dummy = room.Population[0];
        dummy.TakeDamage(1, player);
        Require(player.Feedback.ConfirmedHits == 1 && player.Feedback.Style.Score == 0, "Nonlethal hit confirms without kill score");
        for (int i = 0; i < 5; i++) room.Population[i].TakeDamage(99, player);
        Require(player.Feedback.Style.Combo == 5 && player.Feedback.Style.Multiplier == 2, "Real consecutive kills build multiplier");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "combat-feedback.png"));
        yield return 1.5f;
        room.ResetEncounter();
        Require(player.Feedback.Style.Score == 0, "Room reset also clears score");
    }
    static void Capture(string filename)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var camera = Camera.main; camera.GetComponent<CharacterTestCamera>().enabled = false;
        camera.transform.position = new Vector3(0, -1.5f, -10); camera.orthographicSize = 10.5f;
        foreach (var npc in room.Population) Animate.Invoke(npc, null);
        var render = new RenderTexture(1200, 840, 24); render.Create();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
        var previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(1200, 840, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1200, 840), 0, 0); image.Apply();
        RenderTexture.active = previous; File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
        render.Release(); UnityEngine.Object.Destroy(render); UnityEngine.Object.Destroy(image);
    }
}
