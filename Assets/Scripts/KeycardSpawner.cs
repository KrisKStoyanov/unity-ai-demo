using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeycardSpawner : MonoBehaviour
{
    private GameObject[] keycardSpawns;
    public GameObject keycard;

    private void Awake()
    {
        keycardSpawns = GameObject.FindGameObjectsWithTag("KeycardSpawn");
        int spawnId = Random.Range(0, keycardSpawns.Length);
        Instantiate(keycard, keycardSpawns[spawnId].transform.position, keycard.transform.rotation);
    }
}
