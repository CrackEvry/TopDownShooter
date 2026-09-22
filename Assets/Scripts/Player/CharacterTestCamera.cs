using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterTestCamera : MonoBehaviour
{
    public ActionPlayer player;
    Vector3 velocity;
    Vector3 follow;
    void Start() { follow = player.transform.position; }
    void LateUpdate()
    {
        if (player == null) return;
        Vector3 target = player.transform.position + (Vector3)player.AimDirection * 1.1f;
        follow = Vector3.SmoothDamp(follow, target, ref velocity, 0.12f);
        transform.position = follow - (Vector3)player.AimDirection * player.ShotKick * 0.35f + Vector3.back * 10;
    }
    void OnGUI()
    {
        if (player == null) return;
        GUI.Box(new Rect(18, 18, 370, 88), "CHARACTER LAB  /  JESTAN ASSETS");
        GUI.Label(new Rect(30, 46, 350, 25), "WASD  bewegen    •    Muis  richten / schieten");
        GUI.Label(new Rect(30, 70, 350, 25), player.Reloading ? "HERLADEN…" : $"PISTOOL   {player.Ammo:00} / {player.magazineSize}       R  herladen");
        if (Mouse.current == null) return;
        Vector2 p = Mouse.current.position.ReadValue();
        p.y = Screen.height - p.y;
        GUI.color = new Color(0.4f, 1, 0.85f);
        GUI.DrawTexture(new Rect(p.x - 7, p.y, 15, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(p.x, p.y - 7, 1, 15), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
