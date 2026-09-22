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
        GUI.Box(new Rect(18, 18, 465, 112), "TRAINING");
        GUI.Label(new Rect(30, 43, 440, 25), "WASD  bewegen     Muis  richten     Linksklik  aanvallen");
        GUI.Label(new Rect(30, 66, 440, 25), "E / Rechtsklik  oppakken     Q  neerleggen     R  herladen");
        string status = player.Weapon.IsMelee ? player.Weapon.Name : $"{player.Weapon.Name}   {player.Ammo:00} / {player.magazineSize}";
        GUI.Label(new Rect(30, 91, 440, 25), player.Reloading ? status + "    HERLADEN…" : status);
        if (player.NearbyWeapon != null)
            GUI.Box(new Rect(Screen.width / 2 - 165, Screen.height - 70, 330, 32), "E  OPPAKKEN  /  " + CharacterWeaponSettings.For(player.NearbyWeapon.kind).Name);
        var camera = GetComponent<Camera>();
        foreach (var pickup in WorldWeapon.Available)
        {
            if (pickup == null || pickup.Collected) continue;
            Vector3 label = camera.WorldToScreenPoint(pickup.transform.position + Vector3.down * 0.55f);
            if (label.z > 0) GUI.Label(new Rect(label.x - 60, Screen.height - label.y, 140, 24), CharacterWeaponSettings.For(pickup.kind).Name);
        }
        if (Mouse.current == null) return;
        Vector2 p = Mouse.current.position.ReadValue();
        p.y = Screen.height - p.y;
        GUI.color = new Color(0.4f, 1, 0.85f);
        GUI.DrawTexture(new Rect(p.x - 7, p.y, 15, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(p.x, p.y - 7, 1, 15), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
