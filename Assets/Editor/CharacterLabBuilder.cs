using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterLabBuilder
{
    const string Folder = "Assets/CharacterLab";

    [MenuItem("Tools/Top Down Shooter/Open Character Lab")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(Folder + "/CharacterLab.unity")) Build();
        EditorSceneManager.OpenScene(Folder + "/CharacterLab.unity");
    }

    // Also callable from Unity batch mode. Never writes the existing SampleScene.
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/tds_characters.png");
        if (sheet == null) throw new System.InvalidOperationException("Character asset sheet missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Pixel.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, Folder + "/Pixel.mat");
        }
        string pixelPath = Folder + "/White.png";
        if (!File.Exists(pixelPath))
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            File.WriteAllBytes(pixelPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pixelPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pixelPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 1;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(pixelPath);
        System.Action<string, Vector2, Vector2, Color, bool> block = (name, pos, size, color, solid) =>
        {
            var obj = new GameObject(name);
            obj.transform.position = pos; obj.transform.localScale = size;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = square; renderer.sharedMaterial = material;
            renderer.color = color; renderer.sortingOrder = solid ? 0 : -10;
            if (solid) obj.AddComponent<BoxCollider2D>();
        };
        for (int x = -12; x < 12; x++)
            for (int y = -8; y < 8; y++)
                block("Floor", new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 0.97f,
                    (x + y) % 2 == 0 ? new Color(0.12f, 0.15f, 0.20f) : new Color(0.14f, 0.17f, 0.22f), false);
        var wall = new Color(0.29f, 0.23f, 0.36f);
        block("North", new Vector2(0, 8), new Vector2(25, 0.5f), wall, true);
        block("South", new Vector2(0, -8), new Vector2(25, 0.5f), wall, true);
        block("West", new Vector2(-12, 0), new Vector2(0.5f, 16), wall, true);
        block("East", new Vector2(12, 0), new Vector2(0.5f, 16), wall, true);
        block("Cover A", new Vector2(-4, 1), new Vector2(0.6f, 4), wall, true);
        block("Cover B", new Vector2(4, -2), new Vector2(4, 0.6f), wall, true);
        var player = new GameObject("Player");
        var controller = player.AddComponent<ActionPlayer>();
        controller.characterSheet = sheet;
        player.GetComponent<Rigidbody2D>().gravityScale = 0;
        player.GetComponent<CircleCollider2D>().radius = 0.3f;
        PrefabUtility.SaveAsPrefabAsset(player, Folder + "/Player.prefab");
        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 6;
        cam.backgroundColor = new Color(0.055f, 0.065f, 0.09f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.gameObject.AddComponent<AudioListener>();
        cam.gameObject.AddComponent<CharacterTestCamera>().player = controller;
        var targetSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/tds_characters.png")
            .OfType<Sprite>().First(s => s.name == "tds_characters_0");
        for (int i = 0; i < 5; i++)
        {
            var target = new GameObject("Target " + (i + 1));
            target.transform.position = new Vector3(-6 + i * 3, 5, 0);
            target.transform.localScale = Vector3.one * (targetSprite.pixelsPerUnit / 24);
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = targetSprite; renderer.sharedMaterial = material; renderer.sortingOrder = 2;
            target.AddComponent<BoxCollider2D>(); target.AddComponent<CharacterTestTarget>();
        }
        EditorSceneManager.SaveScene(scene, Folder + "/CharacterLab.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_LAB_BUILD_OK: scene and player prefab saved.");
    }
}
