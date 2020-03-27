using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaminaOrbPickup : MonoBehaviour
{
    public float staminaAmount;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            other.GetComponentInParent<PlayerController>().PickupStaminaOrb(staminaAmount);
            Destroy(gameObject);
        }
    }
}
