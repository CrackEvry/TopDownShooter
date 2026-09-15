using UnityEditor;
using UnityEngine;

public class AnimationCreator
{
    [MenuItem("Tools/Create Animation From Selected Sprites")]
    private static void CreateAnimation()
    {
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Selecteer eerst sprites in je Project window.");
            return;
        }

        AnimationClip clip = new AnimationClip
        {
            frameRate = 8f
        };

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes =
            new ObjectReferenceKeyframe[selectedObjects.Length];

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            Sprite sprite = selectedObjects[i] as Sprite;

            if (sprite == null)
                continue;

            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / clip.frameRate,
                value = sprite
            };
        }

        AnimationUtility.SetObjectReferenceCurve(
            clip,
            spriteBinding,
            keyframes
        );

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Animation",
            "Player_Walk",
            "anim",
            "Kies waar je de animatie wilt opslaan."
        );

        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();

        Debug.Log("Animation gemaakt: " + path);
    }
}