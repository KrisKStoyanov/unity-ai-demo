using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthOrbPickup : MonoBehaviour
{
    public float healthAmount;
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "Player")
        {
            other.GetComponentInParent<PlayerController>().PickupHealthOrb(healthAmount);
            Destroy(gameObject);
        }
    }
}
