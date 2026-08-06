using UnityEngine;

public class CollisionDetection : MonoBehaviour
{
    private AIChase aichase;

    private void Awake()
    {
        aichase = GetComponentInParent<AIChase>();

        //if (aichase == null)
        //{
        //    Debug.LogError("Geen AIChase script gevonden op de Enemy parent.");
        //}
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            aichase.enabled = true;
            //Debug.Log("Player gedetecteerd, AIChase staat aan.");
        }
    }
    //    Enemy.GetComponent<AIChase>().enabled = false;
    //    Debug.Log("Detectie aangeraakt");
}