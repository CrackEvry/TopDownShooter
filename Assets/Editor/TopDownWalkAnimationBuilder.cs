using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class TopDownWalkAnimationBuilder : EditorWindow
{
    private GameObject player;
    private Sprite torso, head, leftArm, rightArm, leftLeg, rightLeg;
    private float legSwingDegrees = 12f;
    private float armSwingDegrees = 6f;
    private float walkFps = 12f;

    [MenuItem("Tools/Top Down Shooter/Create Player Walk Animation")]
    public static void ShowWindow() => GetWindow<TopDownWalkAnimationBuilder>("Player Walk Builder");

    private void OnGUI()
    {
        GUILayout.Label("PLAYER", EditorStyles.boldLabel);
        player = (GameObject)EditorGUILayout.ObjectField("Player Root", player, typeof(GameObject), true);

        EditorGUILayout.Space(8);
        GUILayout.Label("BODY PART SPRITES", EditorStyles.boldLabel);
        torso = (Sprite)EditorGUILayout.ObjectField("Torso", torso, typeof(Sprite), false);
        head = (Sprite)EditorGUILayout.ObjectField("Head", head, typeof(Sprite), false);
        leftArm = (Sprite)EditorGUILayout.ObjectField("Left Arm", leftArm, typeof(Sprite), false);
        rightArm = (Sprite)EditorGUILayout.ObjectField("Right Arm", rightArm, typeof(Sprite), false);
        leftLeg = (Sprite)EditorGUILayout.ObjectField("Left Leg", leftLeg, typeof(Sprite), false);
        rightLeg = (Sprite)EditorGUILayout.ObjectField("Right Leg", rightLeg, typeof(Sprite), false);

        EditorGUILayout.Space(8);
        GUILayout.Label("WALK FEEL", EditorStyles.boldLabel);
        walkFps = EditorGUILayout.FloatField("Walk FPS", walkFps);
        legSwingDegrees = EditorGUILayout.FloatField("Leg Swing", legSwingDegrees);
        armSwingDegrees = EditorGUILayout.FloatField("Arm Swing", armSwingDegrees);

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("Creates Idle/Walk clips and Animator. No body/view bobbing; Player root is never animated.", MessageType.Info);

        GUI.enabled = player != null && torso != null && leftLeg != null && rightLeg != null;
        if (GUILayout.Button("BUILD EVERYTHING", GUILayout.Height(42))) Build();
        GUI.enabled = true;
    }

    private void Build()
    {
        string folder = "Assets/GeneratedPlayerAnimation";
        Directory.CreateDirectory(folder);

        Transform existingVisual = player.transform.Find("Visual");
        if (existingVisual != null)
        {
            if (!EditorUtility.DisplayDialog("Visual already exists", "Replace the existing Visual child?", "Replace", "Cancel")) return;
            Undo.DestroyObjectImmediate(existingVisual.gameObject);
        }

        GameObject visual = CreateEmpty("Visual", player.transform);
        GameObject lowerBody = CreateEmpty("LowerBody", visual.transform);
        GameObject upperBody = CreateEmpty("UpperBody", visual.transform);

        CreateSpritePart("LeftLeg", leftLeg, lowerBody.transform, 0);
        CreateSpritePart("RightLeg", rightLeg, lowerBody.transform, 0);
        CreateSpritePart("Torso", torso, upperBody.transform, 2);
        if (head != null) CreateSpritePart("Head", head, upperBody.transform, 4);
        if (leftArm != null) CreateSpritePart("LeftArm", leftArm, upperBody.transform, 3);
        if (rightArm != null) CreateSpritePart("RightArm", rightArm, upperBody.transform, 3);

        Animator animator = visual.AddComponent<Animator>();

        string idlePath = folder + "/Player_Idle.anim";
        string walkPath = folder + "/Player_Walk.anim";
        string controllerPath = folder + "/Player.controller";
        DeleteAssetIfExists(idlePath);
        DeleteAssetIfExists(walkPath);
        DeleteAssetIfExists(controllerPath);

        AnimationClip idle = CreateIdleClip();
        AnimationClip walk = CreateWalkClip();
        AssetDatabase.CreateAsset(idle, idlePath);
        AssetDatabase.CreateAsset(walk, walkPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle"); idleState.motion = idle; sm.defaultState = idleState;
        AnimatorState walkState = sm.AddState("Walk"); walkState.motion = walk;
        AnimatorStateTransition toWalk = idleState.AddTransition(walkState); toWalk.hasExitTime = false; toWalk.duration = 0f; toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        AnimatorStateTransition toIdle = walkState.AddTransition(idleState); toIdle.hasExitTime = false; toIdle.duration = 0f; toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        animator.runtimeAnimatorController = controller;

        PlayerAnimationDriver driver = player.GetComponent<PlayerAnimationDriver>();
        if (driver == null) driver = Undo.AddComponent<PlayerAnimationDriver>(player);
        SerializedObject so = new SerializedObject(driver);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("rb").objectReferenceValue = player.GetComponent<Rigidbody2D>();
        so.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = player;
        EditorGUIUtility.PingObject(player);
        Debug.Log("Walk animation setup created. Position the body parts under Player > Visual, then press Play.");
    }

    private AnimationClip CreateIdleClip()
    {
        AnimationClip clip = new AnimationClip { name = "Player_Idle", frameRate = walkFps };
        SetConstantRotation(clip, "LowerBody/LeftLeg", 0f);
        SetConstantRotation(clip, "LowerBody/RightLeg", 0f);
        if (leftArm != null) SetConstantRotation(clip, "UpperBody/LeftArm", 0f);
        if (rightArm != null) SetConstantRotation(clip, "UpperBody/RightArm", 0f);
        SetLoop(clip);
        return clip;
    }

    private AnimationClip CreateWalkClip()
    {
        AnimationClip clip = new AnimationClip { name = "Player_Walk", frameRate = walkFps };
        float duration = 4f / walkFps;
        SetSwingCurve(clip, "LowerBody/LeftLeg", legSwingDegrees, duration, false);
        SetSwingCurve(clip, "LowerBody/RightLeg", legSwingDegrees, duration, true);
        if (leftArm != null) SetSwingCurve(clip, "UpperBody/LeftArm", armSwingDegrees, duration, true);
        if (rightArm != null) SetSwingCurve(clip, "UpperBody/RightArm", armSwingDegrees, duration, false);
        SetLoop(clip);
        return clip;
    }

    private static void SetSwingCurve(AnimationClip clip, string path, float angle, float duration, bool reverse)
    {
        float a = reverse ? -angle : angle;
        float b = -a;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, a),
            new Keyframe(duration * 0.25f, 0f),
            new Keyframe(duration * 0.5f, b),
            new Keyframe(duration * 0.75f, 0f),
            new Keyframe(duration, a));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", curve);
    }

    private static void SetConstantRotation(AnimationClip clip, string path, float angle)
    {
        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, 1f, angle));
    }

    private static void SetLoop(AnimationClip clip)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
        if (settings != null)
        {
            SerializedProperty loopTime = settings.FindPropertyRelative("m_LoopTime");
            if (loopTime != null) loopTime.boolValue = true;
        }
        serializedClip.ApplyModifiedProperties();
    }

    private static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateSpritePart(string name, Sprite sprite, Transform parent, int sortingOrder)
    {
        GameObject go = CreateEmpty(name, parent);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    private static void DeleteAssetIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
    }
}
