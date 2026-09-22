using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class CharacterLabValidation
{
    const string Key = "CharacterLabValidation.Running";
    static IEnumerator<float> checks;
    static double nextTick;
    static ActionPlayer player;
    static Rigidbody2D body;
    static CharacterTestTarget target;
    static SimulationMode2D previousMode;
    static string output;
    static int count;
    static CharacterLabValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { nextTick = EditorApplication.timeSinceStartup + 1; checks = Checks().GetEnumerator(); EditorApplication.update += Tick; }
        };
    }
    public static void Run()
    {
        CharacterLabBuilder.Build();
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTick) return;
        try
        {
            if (checks.MoveNext()) { nextTick = EditorApplication.timeSinceStartup + checks.Current; return; }
            Debug.Log($"CHARACTER_LAB_TESTS_OK: {count} checks. Movement, anchored legs, four weapons, pickup/drop, ammo, reload, spread, melee timing and cover.");
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        Physics2D.simulationMode = previousMode;
        SessionState.SetBool(Key, false);
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception("Character lab check failed: " + message); count++; Debug.Log("PASS: " + message); }
    static void Warp(Vector2 position)
    { body.position = position; player.transform.position = position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static void Motor() => typeof(ActionPlayer).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
    static void PlaceTarget(Vector2 position)
    { target.transform.position = position; target.GetComponent<Collider2D>().enabled = true; Physics2D.SyncTransforms(); }
    static void Pickup(CharacterWeapon kind)
    {
        var pickup = WorldWeapon.Available.First(p => p.kind == kind);
        Warp(pickup.transform.position);
        Require(player.TryPickupNearest() && player.EquippedWeapon == kind && pickup.Collected, "Equip " + kind + " from ground");
    }
    static IEnumerable<float> Checks()
    {
        count = 0;
        player = UnityEngine.Object.FindFirstObjectByType<ActionPlayer>();
        Require(player != null && player.BodyVisual != null, "Player visual assembly");
        player.enabled = false;
        body = player.GetComponent<Rigidbody2D>();
        target = UnityEngine.Object.FindFirstObjectByType<CharacterTestTarget>();
        previousMode = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
        Require(player.EquippedWeapon == CharacterWeapon.Unarmed && !player.BodyVisual.HeldWeapon.enabled && player.BodyVisual.Torso.sprite.name.StartsWith("Empty hands"), "Reference idle sprite, no weapon");
        Require(WorldWeapon.Available.Count == 4 && WorldWeapon.Available.Select(p => p.kind).Distinct().Count() == 4, "All four ground weapons");
        foreach (var pickup in WorldWeapon.Available)
        {
            var position = pickup.transform.position;
            pickup.Animate(0); var visualStart = pickup.Visual.localPosition;
            pickup.Animate(0.31f);
            Require(pickup.transform.position == position && (pickup.Visual.localPosition - visualStart).sqrMagnitude > 0.000001f && pickup.Halo.color.a > 0 && pickup.Halo.color.a < 0.5f, "Stationary interaction point, floating sprite, subtle glow: " + pickup.kind);
        }
        Warp(Vector2.zero); player.SetMoveInput(Vector2.one); Motor();
        Require(Mathf.Abs(body.linearVelocity.magnitude - player.moveSpeed) < 0.001f, "Diagonal movement is normalized");
        player.SetMoveInput(Vector2.zero); Motor();
        Require(body.linearVelocity == Vector2.zero, "Immediate stop");
        Warp(new Vector2(-5, 1)); player.SetMoveInput(Vector2.right);
        for (int i = 0; i < 30; i++) { Motor(); Physics2D.Simulate(0.02f); }
        Require(body.position.x < -4.5f, "Wall collision");
        player.SetMoveInput(Vector2.zero); Motor(); Warp(Vector2.zero);
        var left = player.BodyVisual.LeftHip.localPosition; var right = player.BodyVisual.RightHip.localPosition;
        bool hipsFixed = true, kneesConnected = true;
        for (int i = 0; i < 180; i++)
        {
            var direction = Quaternion.Euler(0, 0, i * 2) * Vector2.right;
            player.BodyVisual.Pose(1f / 60, direction * 7, Vector2.right, CharacterWeapon.Unarmed, 0, -1, false, player.strideLength);
            hipsFixed &= player.BodyVisual.LeftHip.localPosition == left && player.BodyVisual.RightHip.localPosition == right;
            foreach (var hip in new[] { player.BodyVisual.LeftHip, player.BodyVisual.RightHip })
            {
                var thigh = hip.GetChild(0); var shin = thigh.GetChild(0);
                kneesConnected &= Vector3.Distance(thigh.TransformPoint(Vector3.down * (7f / 24)), shin.position) < 0.0001f;
            }
        }
        Require(hipsFixed && kneesConnected, "Fixed hips and connected knees through 180 changing movement directions");
        Pickup(CharacterWeapon.Pistol); Warp(Vector2.zero); PlaceTarget(new Vector2(3, 0)); player.SetAimDirection(Vector2.right);
        int initial = target.HitCount;
        Require(player.TryAttack() && player.Ammo == 11 && target.HitCount == initial + 1, "Pistol shot and ammo");
        Require(!player.TryAttack(), "Fire cooldown");
        yield return 0.2f;
        Require(!player.TryAttack(false, true), "Pistol needs a new click");
        Require(player.TryReload() && !player.TryAttack(), "Reload blocks firing");
        yield return 0.8f; player.TickActions();
        Require(player.Ammo == 12 && !player.Reloading, "Reload completes");
        Warp(new Vector2(-2, 1)); PlaceTarget(new Vector2(-6, 1)); player.SetAimDirection(Vector2.left); initial = target.HitCount;
        Require(player.TryAttack() && target.HitCount == initial, "Wall blocks bullet damage");
        yield return 0.2f;
        Pickup(CharacterWeapon.Shotgun); Warp(Vector2.zero); player.SetAimDirection(Vector2.right);
        Require(player.TryAttack() && player.Ammo == 5 && player.LastPelletCount == 7, "Shotgun: seven pellets, one shell");
        yield return 0.7f;
        Pickup(CharacterWeapon.Automatic); Warp(Vector2.zero);
        Require(player.TryAttack(false, true) && player.Ammo == 29, "Automatic fires while held");
        yield return 0.1f;
        Require(player.TryAttack(false, true) && player.Ammo == 28, "Automatic repeats after cooldown");
        Require(player.TryReload() && player.DropWeapon() && !player.Reloading && !player.BodyVisual.HeldWeapon.enabled, "Drop cancels reload and shows empty hands");
        var dropped = WorldWeapon.Available.First(p => p.kind == CharacterWeapon.Automatic);
        Require(dropped.ammo == 28, "Dropped weapon preserves ammo");
        Warp(dropped.transform.position);
        Require(player.TryPickupNearest() && player.Ammo == 28, "Pickup preserves ammo");
        Warp(new Vector2(6, -5));
        WorldWeapon.Spawn(player.characterSheet, CharacterWeapon.Pistol, body.position, 0);
        Require(player.TryPickupNearest(), "Empty weapon can be picked up");
        yield return 0.2f;
        Require(!player.TryAttack() && player.Ammo == 0, "Empty weapon cannot fire");
        Pickup(CharacterWeapon.Knife); Warp(Vector2.zero); PlaceTarget(new Vector2(0.7f, 0)); player.SetAimDirection(Vector2.right);
        initial = target.HitCount;
        Require(player.TryAttack() && target.HitCount == initial, "Knife wind-up has no early damage");
        yield return 0.12f; player.TickActions();
        Require(target.HitCount == initial + 1, "Knife impact during attack frames");
        target.GetComponent<Collider2D>().enabled = true; player.TickActions();
        Require(target.HitCount == initial + 1, "Only one hit per swing");
        yield return 0.3f; player.TickActions();
        Warp(new Vector2(-3.5f, 1)); PlaceTarget(new Vector2(-4.55f, 1)); player.SetAimDirection(Vector2.left); initial = target.HitCount;
        Require(player.TryAttack(), "Second knife swing");
        yield return 0.12f; player.TickActions();
        Require(target.HitCount == initial, "Knife cannot hit through cover");
        yield return 0.3f; player.TickActions();
        Require(!player.Attacking && player.DropWeapon() && player.BodyVisual.Torso.sprite.name.StartsWith("Empty hands"), "Knife recovery returns to unarmed idle after drop");
        PlaceTarget(new Vector2(-0.65f, 0)); Warp(Vector2.zero); player.SetAimDirection(Vector2.right); initial = target.HitCount;
        Require(player.TryAttack(), "Unarmed strike starts");
        yield return 0.14f; player.TickActions();
        Require(target.HitCount == initial, "Melee cannot hit behind the player");
        yield return 0.3f; player.TickActions();
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            CaptureVisuals();
            yield return 0.2f;
            CaptureGround();
        }
    }
    static void CaptureVisuals()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-characterLabOutput");
        output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Temp/CharacterDiagnostics");
        Directory.CreateDirectory(output);
        Warp(Vector2.zero);
        var camera = Camera.main;
        camera.GetComponent<CharacterTestCamera>().enabled = false;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 1.2f;
        // Park targets and dropped weapons outside the close-up.
        foreach (var t in UnityEngine.Object.FindObjectsByType<CharacterTestTarget>(FindObjectsSortMode.None)) t.transform.position += Vector3.up * 30;
        foreach (var pickup in WorldWeapon.Available) pickup.transform.position += Vector3.up * 30;
        CaptureStrip("weapon-poses.png", 5, 256, i => player.BodyVisual.Pose(1, Vector2.zero, Vector2.down, (CharacterWeapon)i, 0, -1, false, player.strideLength), camera);
        CaptureStrip("walk-cycle.png", 8, 192, i => player.BodyVisual.Pose(player.strideLength / 7 / 8, Vector2.down * 7, Vector2.down, CharacterWeapon.Automatic, 0, -1, false, player.strideLength), camera);
        CaptureStrip("walk-smooth.png", 32, 192, i => player.BodyVisual.Pose(player.strideLength / 7 / 32, Vector2.down * 7, Vector2.down, CharacterWeapon.Automatic, 0, -1, false, player.strideLength), camera);
        CaptureStrip("knife-swing.png", 8, 192, i => player.BodyVisual.Pose(0.05f, Vector2.zero, Vector2.down, CharacterWeapon.Knife, 0, i / 8f, false, player.strideLength), camera);
        player.BodyVisual.Root.gameObject.SetActive(false);
        for (int i = 1; i <= 4; i++) WorldWeapon.Spawn(player.characterSheet, (CharacterWeapon)i, new Vector2(-2.4f + (i - 1) * 1.6f, 0), CharacterWeaponSettings.For((CharacterWeapon)i).Capacity);
    }
    static void CaptureGround()
    {
        var camera = Camera.main;
        camera.orthographicSize = 0.9f;
        CaptureStrip("ground-weapons.png", 4, 256, i => {
            camera.transform.position = new Vector3(-2.4f + i * 1.6f, 0, -10);
            foreach (var pickup in WorldWeapon.Available)
                foreach (var renderer in pickup.GetComponentsInChildren<SpriteRenderer>())
                    renderer.enabled = Mathf.Abs(pickup.transform.position.x - camera.transform.position.x) < 0.1f && Mathf.Abs(pickup.transform.position.y) < 1;
        }, camera);
        Debug.Log("CHARACTER_LAB_VISUALS_OK: " + output);
    }
    static void CaptureStrip(string file, int frames, int size, Action<int> pose, Camera camera)
    {
        var strip = new Texture2D(size * frames, size, TextureFormat.RGBA32, false);
        var render = new RenderTexture(size, size, 24);
        render.Create();
        var frame = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int i = 0; i < frames; i++)
        {
            pose(i);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
            var previous = RenderTexture.active; RenderTexture.active = render;
            frame.ReadPixels(new Rect(0, 0, size, size), 0, 0); frame.Apply();
            RenderTexture.active = previous;
            strip.SetPixels(i * size, 0, size, size, frame.GetPixels());
        }
        strip.Apply(); File.WriteAllBytes(Path.Combine(output, file), strip.EncodeToPNG());
        UnityEngine.Object.Destroy(frame); UnityEngine.Object.Destroy(strip); render.Release(); UnityEngine.Object.Destroy(render);
    }
}
