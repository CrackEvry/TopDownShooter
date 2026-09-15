using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 2;
    private int currentHealth;
    
    //sprite veranderingen OEHHHHH
    
    //[SerializeField] private SpriteRenderer spriteRenderer;
    //[SerializeField] private Sprite healthySprite;
    //[SerializeField] private Sprite damagedSprite;
    //[SerializeField] private Sprite deadSprite;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color damagedColor = Color.yellow;
    [SerializeField] private Color deadColor = Color.red;
    
    
    [SerializeField] private float deathDelay = 0.5f;

    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateColor(); //(notitie) voor sprites zet het dan terug naar UpdateSprite 😝
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        UpdateColor(); //ook hier zet het dan terug naar UpdateSprite();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    //private void UpdateSprite()
    //{
    //    if (currentHealth >= 2)
    //    {
    //        spriteRenderer.sprite = healthySprite;
    //    }
    //    else if (currentHealth == 1)
    //    {
    //        spriteRenderer.sprite = damagedSprite;
    //    }
    //    else
    //    {
    //        spriteRenderer.sprite = deadSprite;
    //    }
    
    
    private void UpdateColor()
    {
        if (currentHealth >= 2)
        {
            spriteRenderer.color = healthyColor;
        }
        else if (currentHealth == 1)
        {
            spriteRenderer.color = damagedColor;
        }
        else
        {
            spriteRenderer.color = deadColor;
        }
    }

    private void Die()
    {
        isDead = true;


        Destroy(gameObject, deathDelay);
    }
}