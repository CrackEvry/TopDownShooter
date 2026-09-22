using UnityEngine;

public class CharacterTestTarget : MonoBehaviour
{
    float resetAt;
    public void Hit()
    {
        GetComponent<SpriteRenderer>().color = new Color(1, 0.2f, 0.35f);
        GetComponent<Collider2D>().enabled = false;
        resetAt = Time.time + 1.2f;
    }
    void Update()
    {
        if (resetAt <= 0 || Time.time < resetAt) return;
        GetComponent<SpriteRenderer>().color = Color.white;
        GetComponent<Collider2D>().enabled = true;
        resetAt = 0;
    }
}
