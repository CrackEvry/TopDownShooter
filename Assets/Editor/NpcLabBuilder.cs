using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NpcLabBuilder
{
    public const string ScenePath = "Assets/CharacterLab/NpcLab.unity";
    [MenuItem("Tools/Top Down Shooter/Open NPC Lab")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Build();
        EditorSceneManager.OpenScene(ScenePath);
    }
    public static void Build()
    {
        if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
        CharacterLabBuilder.Build();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        foreach (var target in Object.FindObjectsByType<CharacterTestTarget>(FindObjectsSortMode.None)) Object.DestroyImmediate(target.gameObject);
        foreach (string name in new[] { "South", "West", "East" })
        { var old = GameObject.Find(name); if (old != null) Object.DestroyImmediate(old); }
        var player = Object.FindFirstObjectByType<ActionPlayer>();
        player.transform.position = new Vector3(0, -9.5f, 0);
        player.startingWeapon = CharacterWeapon.Pistol;
        player.gameObject.AddComponent<PlayerVitality>();
        var camera = Camera.main; camera.transform.position = new Vector3(0, -7, -10);
        var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CharacterLab/White.png");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CharacterLab/Pixel.mat");
        var wall = new Color(0.29f, 0.23f, 0.36f);
        System.Action<string, Vector2, Vector2, Color, bool> block = (name, position, size, color, solid) =>
        {
            var obj = new GameObject(name); obj.transform.position = position; obj.transform.localScale = size;
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sharedMaterial = material;
            renderer.color = color; renderer.sortingOrder = solid ? 0 : -10;
            if (solid) obj.AddComponent<BoxCollider2D>();
        };
        block("South left", new Vector2(-7, -8), new Vector2(10, 0.5f), wall, true);
        block("South right", new Vector2(7, -8), new Vector2(10, 0.5f), wall, true);
        for (int side = -1; side <= 1; side += 2)
        {
            block("Side lower", new Vector2(side * 12, -2.75f), new Vector2(0.5f, 10.5f), wall, true);
            block("Side upper", new Vector2(side * 12, 6.25f), new Vector2(0.5f, 3.5f), wall, true);
            block("Exit floor", new Vector2(side * 13.5f, 3.5f), new Vector2(3, 2), new Color(0.10f, 0.24f, 0.21f), false);
            block("Exit rail lower", new Vector2(side * 13.5f, 2.4f), new Vector2(3, 0.2f), wall, true);
            block("Exit rail upper", new Vector2(side * 13.5f, 4.6f), new Vector2(3, 0.2f), wall, true);
        }
        block("Entrance floor", new Vector2(0, -10), new Vector2(4, 4), new Color(0.16f, 0.15f, 0.22f), false);
        block("Entrance left", new Vector2(-2.15f, -10), new Vector2(0.3f, 4), wall, true);
        block("Entrance right", new Vector2(2.15f, -10), new Vector2(0.3f, 4), wall, true);
        block("Cover C", new Vector2(2, 2), new Vector2(2.8f, 0.6f), wall, true);
        block("Cover D", new Vector2(7, 4), new Vector2(0.6f, 3), wall, true);
        var root = new GameObject("NPC Room - 90 flee, 5 fight, 5 hide");
        var room = root.AddComponent<NpcRoom>();
        var nav = root.AddComponent<NpcNavigationGrid>(); room.navigation = nav;
        nav.walkAreas = new[] { new Rect(-12, -8, 24, 16), new Rect(-15, 2.5f, 3.3f, 2), new Rect(11.7f, 2.5f, 3.3f, 2), new Rect(-2, -12, 4, 4.3f) };
        room.exits = new[] { Point(root.transform, "West safe exit", new Vector2(-14.25f, 3.5f)), Point(root.transform, "East safe exit", new Vector2(14.25f, 3.5f)) };
        room.cover = new[] {
            Point(root.transform, "A west", new Vector2(-4.85f, 1)), Point(root.transform, "A east", new Vector2(-3.15f, 1)),
            Point(root.transform, "B north", new Vector2(4, -1.1f)), Point(root.transform, "B south", new Vector2(4, -2.9f)),
            Point(root.transform, "C north", new Vector2(2, 2.9f)), Point(root.transform, "C south", new Vector2(2, 1.1f)),
            Point(root.transform, "D west", new Vector2(6.15f, 4)), Point(root.transform, "D east", new Vector2(7.85f, 4)) };
        float[] xs = { -8, -5.7f, -1, 3.8f, 9 };
        float[] ys = { -0.2f, 1.6f, 3.4f, 6 };
        for (int i = 0; i < 20; i++)
        {
            var obj = new GameObject("NPC " + (i + 1).ToString("00")); obj.transform.SetParent(root.transform);
            obj.transform.position = new Vector3(xs[i % 5], ys[i / 5], 0);
            var npc = obj.AddComponent<NpcBrain>(); npc.characterSheet = player.characterSheet; npc.characterVariant = 1 + i % 4; npc.room = room;
            CharacterLabBuilder.ConfigureFeedback(obj, true);
            obj.GetComponent<Rigidbody2D>().gravityScale = 0; obj.GetComponent<CircleCollider2D>().radius = 0.27f;
            if (i == 0) { npc.room = null; PrefabUtility.SaveAsPrefabAsset(obj, "Assets/CharacterLab/EnemyNpc.prefab"); npc.room = room; }
        }
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        Debug.Log("NPC_LAB_BUILD_OK: twenty NPCs, walkable exits, cover and entrance.");
    }
    static Transform Point(Transform root, string name, Vector2 position)
    { var point = new GameObject(name).transform; point.SetParent(root); point.position = position; return point; }
}
