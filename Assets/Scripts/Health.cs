using System;
using System.Reflection.Metadata;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] int maxHealth = 1;
    int currentHealth;
    
    
    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        
    }

    private void Awake()
    {
        //if (currentHealth <= 0)
        //    {
        //    Destroy(gameObject);
        //    }
    }
    void Death()
    {
        currentHealth = 0;
        
    }
}
