using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ProjectileTravel : MonoBehaviour
{
    public float speed;
    public float lifetime;
    public float playerDamage;
    public float enemyDamage;
    // Update is called once per frame
    void Update()
    {
        Invoke("DeactivateCountdown", lifetime);
        transform.Translate(0.0f, speed * Time.deltaTime, 0.0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.gameObject.tag == "Enemy")
        {
            other.transform.GetComponent<EnemyAIController>().InflictDamage(playerDamage);
        }
        else if (other.transform.gameObject.tag == "Player")
        {
            other.transform.GetComponentInParent<PlayerController>().InflictDamage(enemyDamage);
        }
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    void DeactivateCountdown()
    {
        gameObject.SetActive(false);
    }
}
