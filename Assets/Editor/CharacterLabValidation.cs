using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterLabValidation
{
    const string Key = "CharacterLabValidation.Running";
    static int frames;
    static CharacterLabValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { frames = 0; EditorApplication.update += Tick; }
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/CharacterLab/CharacterLab.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (++frames != 40) return;
        EditorApplication.update -= Tick;
        try
        {
            var player = UnityEngine.Object.FindFirstObjectByType<ActionPlayer>();
            Require(player != null && player.GetComponentsInChildren<SpriteRenderer>(true).Length == 4, "Player visual assembly");
            var body = player.GetComponent<Rigidbody2D>();
            var mode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            body.position = new Vector2(-5, 1);
            body.linearVelocity = Vector2.right * player.moveSpeed;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 30; i++) Physics2D.Simulate(0.02f);
            Require(body.position.x < -4.5f, "Wall blocks player");
            body.linearVelocity = Vector2.zero;
            body.position = Vector2.zero;
            player.transform.position = Vector3.zero;
            var target = UnityEngine.Object.FindFirstObjectByType<CharacterTestTarget>();
            target.transform.position = new Vector3(3, 0, 0);
            var aim = typeof(ActionPlayer).GetProperty("AimDirection");
            aim.SetValue(player, Vector2.right);
            Physics2D.SyncTransforms();
            var shoot = typeof(ActionPlayer).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
            shoot.Invoke(player, null);
            Require(player.Ammo == 11 && !target.GetComponent<Collider2D>().enabled, "Shot hits target and spends one round");
            target.GetComponent<Collider2D>().enabled = true;
            target.transform.position = new Vector3(-6, 1, 0);
            body.position = new Vector2(-2, 1);
            player.transform.position = new Vector3(-2, 1, 0);
            aim.SetValue(player, Vector2.left);
            Physics2D.SyncTransforms();
            shoot.Invoke(player, null);
            Require(target.GetComponent<Collider2D>().enabled, "Cover blocks damage");
            Physics2D.simulationMode = mode;
            Debug.Log("CHARACTER_LAB_TESTS_OK: visual assembly, wall collision, target hit, ammo, cover.");
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(1);
        }
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception("Character lab check failed: " + message); }
}
