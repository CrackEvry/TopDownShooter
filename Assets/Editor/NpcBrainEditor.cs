using Shooter.AI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcBrain))]
public class NpcBrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var npc = (NpcBrain)target;
        if (!Application.isPlaying || npc.Tree == null) return;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Live behaviour tree", EditorStyles.boldLabel);
        DrawNode(npc.Tree); Repaint();
    }
    static void DrawNode(BehaviourNode node)
    {
        Color previous = GUI.color;
        GUI.color = node.Status == NodeStatus.Running ? Color.cyan : node.Status == NodeStatus.Success ? Color.green : node.Status == NodeStatus.Failure ? new Color(1, 0.65f, 0.55f) : Color.gray;
        EditorGUILayout.LabelField(node.Name + "  [" + node.Status + "]"); GUI.color = previous;
        EditorGUI.indentLevel++;
        foreach (var child in node.Children) DrawNode(child);
        EditorGUI.indentLevel--;
    }
}
