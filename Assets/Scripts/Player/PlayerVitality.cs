using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerVitality : MonoBehaviour
{
    public int maxHealth = 5;
    public int Health { get; private set; }
    public bool IsDead => Health <= 0;
    Vector2 spawn;
    ActionPlayer player;
    float invulnerableUntil;
    void Awake() { player = GetComponent<ActionPlayer>(); spawn = transform.position; Health = maxHealth; }
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0 || Time.time < invulnerableUntil) return;
        Health = Mathf.Max(0, Health - amount); invulnerableUntil = Time.time + 0.2f;
        player.Feedback?.Hurt();
        if (IsDead) { player.enabled = false; GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; }
    }
    public void Respawn()
    {
        Health = maxHealth; GetComponent<Rigidbody2D>().position = spawn; transform.position = spawn;
        player.enabled = true; invulnerableUntil = Time.time + 1;
        player.Feedback?.ResetEncounter();
        foreach (var room in FindObjectsByType<NpcRoom>(FindObjectsSortMode.None)) room.ResetEncounter();
    }
    void Update() { if (IsDead && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) Respawn(); }
    void OnGUI()
    {
        GUI.Label(new Rect(30, 132, 350, 24), $"LEVEN {Health}/{maxHealth}    F2 NPC-status    F6 NPC-reset");
        if (IsDead) GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height / 2 - 35, 340, 70), "UITGESCHAKELD\nEnter om opnieuw te proberen");
    }
}
