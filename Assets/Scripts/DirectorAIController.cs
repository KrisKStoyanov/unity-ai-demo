using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DirectorAIController : MonoBehaviour
{
    public float startingResources;

    public float currentResources;

    public float regenResourceRate;

    public float spawnResourceCost;
    public float instructResourceCost;

    public GameObject player;

    public GameObject enemyBot;
    private BoxCollider[] spawnVolumes;
    List<int> eligibleVolumeIds;
    public List<GameObject> spawnedBots;
    List<int> usedSpawnIds;

    public int maxBotSpawns;

    public float spawnBias;
    public float instructBias;
    public float spawnBiasOffset;
    public float instructBiasOffset;

    public float updateBiasCooldown;
    private float updateBiasDefaultCooldown;
    private float updateBiasReducer;

    //Used to measure pacing of bot spawns in relation to player visibility and progression
    public float playerFlowRadius = 20.0f;

    public Image VictoryPanel;
    public Image DefeatPanel;

    private void Awake()
    {
        currentResources = startingResources;

        updateBiasDefaultCooldown = updateBiasCooldown;
        updateBiasReducer = 1.0f;

        GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag("Spawn");
        eligibleVolumeIds = new List<int>();
        spawnVolumes = new BoxCollider[spawnObjects.Length];
        for(int i = 0; i < spawnVolumes.Length; ++i)
        {
            spawnVolumes[i] = spawnObjects[i].GetComponent<BoxCollider>();
        }

        spawnedBots = new List<GameObject>();
        usedSpawnIds = new List<int>();

        while (currentResources >= spawnResourceCost)
        {
            int spawnId = Random.Range(0, spawnVolumes.Length);
            float distToSpawnVolume = (player.transform.position - spawnVolumes[spawnId].transform.position).magnitude;
            if (!usedSpawnIds.Contains(spawnId) && distToSpawnVolume > playerFlowRadius)
            {
                currentResources -= spawnResourceCost;
                float xSpawnCoord = Random.Range(spawnVolumes[spawnId].bounds.min.x, spawnVolumes[spawnId].bounds.max.x);
                float zSpawnCoord = Random.Range(spawnVolumes[spawnId].bounds.min.z, spawnVolumes[spawnId].bounds.max.z);
                Vector3 spawnPos = new Vector3(xSpawnCoord, 0.0f, zSpawnCoord);
                //SpawnBot(enemySpawns[spawnId].transform.position);
                SpawnBot(spawnPos);
                usedSpawnIds.Add(spawnId);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        updateBiasCooldown -= updateBiasReducer * Time.deltaTime;
        if(updateBiasCooldown < 0.0f)
        {
            //Reset cooldown
            updateBiasCooldown = updateBiasDefaultCooldown;

            //Compute spawn bias
            spawnBias = maxBotSpawns / spawnedBots.Count  + currentResources / spawnResourceCost;

            //Compute instruct bias
            float avgDistToPlayer = 0.0f;
            foreach(GameObject bot in spawnedBots)
            {
                avgDistToPlayer += (bot.transform.position - player.transform.position).magnitude;
            }
            instructBias = spawnedBots.Count / avgDistToPlayer + currentResources / instructResourceCost;

            //Debug.Log("Instruct Offset: " + ((instructBias * spawnedBots.Count) + currentResources / instructResourceCost));
            //Debug.Log("Spawn Offset: " + (spawnBias * (maxBotSpawns / spawnedBots.Count) + currentResources / spawnResourceCost));

            //Fuzzy logic implementation - introduce offset chance based on spawned bots vs max bots ratio
            instructBiasOffset = Random.Range(0.0f, instructBias * ((float)spawnedBots.Count / maxBotSpawns));
            spawnBiasOffset = Random.Range(0.0f, (spawnBias * (maxBotSpawns / spawnedBots.Count)));

            instructBias += instructBiasOffset;
            spawnBias += spawnBiasOffset;
        }

        //Decision tree implementation influenced by Fuzzy logic, bias prediction & resource management
        if (spawnBias > instructBias)
        {
            float inaccurateBiasPredictionCost = instructResourceCost + (spawnBias - instructBias) * spawnResourceCost;
            //Choose to spawn
            if (currentResources >= spawnResourceCost)
            {
                SpawnBehavior(spawnResourceCost);
            }
            //Incur cost penalty for inefficient bias prediction
            else if (currentResources >= inaccurateBiasPredictionCost)
            {
                InstructBehavior(inaccurateBiasPredictionCost);
                instructResourceCost = inaccurateBiasPredictionCost;
            }
        }
        else
        {
            float inaccurateBiasPredictionCost = spawnResourceCost + (instructBias - spawnBias) * instructResourceCost;
            //Choose to instruct based on flocking behavior
            if (currentResources >= instructResourceCost)
            {
                InstructBehavior(instructResourceCost);
            }
            //Incur cost penalty for inefficient bias prediction
            else if (currentResources >= inaccurateBiasPredictionCost)
            {
                SpawnBehavior(inaccurateBiasPredictionCost);
                spawnResourceCost = inaccurateBiasPredictionCost;
            }
        }

        currentResources += regenResourceRate * Time.deltaTime;
    }

    void SpawnBehavior(float resourceCost)
    {
        for (int i = 0; i < spawnVolumes.Length; ++i)
        {
            float distToSpawnVolume = (player.transform.position - spawnVolumes[i].transform.position).magnitude;
            if(distToSpawnVolume > playerFlowRadius)
            {
                eligibleVolumeIds.Add(i);
            }
        }
        int svId = Random.Range(0, eligibleVolumeIds.Count);
        eligibleVolumeIds.Clear();

        currentResources -= resourceCost;
        float xSpawnCoord = Random.Range(spawnVolumes[svId].bounds.min.x, spawnVolumes[svId].bounds.max.x);
        float zSpawnCoord = Random.Range(spawnVolumes[svId].bounds.min.z, spawnVolumes[svId].bounds.max.z);
        Vector3 spawnPos = new Vector3(xSpawnCoord, 0.0f, zSpawnCoord);
        SpawnBot(spawnPos);
    }

    void InstructBehavior(float resourceCost)
    {
        //Decision tree implementation influenced by FSM state, active NPC count and select NPC class
        if (spawnedBots.Count > 0)
        {
            currentResources -= resourceCost;
            int selectedBot = Random.Range(0, spawnedBots.Count);
            EnemyAIController botCtrl = spawnedBots[selectedBot].GetComponent<EnemyAIController>();
            //If already chasing receive assistance from closest bot if max spawnned bot count has not been reached otherwise self destruct
            if (botCtrl.currentState == EnemyAIController.BotState.CHASING)
            {
                //If there aren't more active bots than the maximum count instruct the selected bot otherwise set it to self-destruct
                if (!(spawnedBots.Count > maxBotSpawns))
                {
                    GetNearestBot(spawnedBots[selectedBot].transform.position).GetComponent<EnemyAIController>().SearchForTargetAtPos(spawnedBots[selectedBot].GetComponent<EnemyAIController>().target.position);
                }
                else
                {
                    botCtrl.InitiateSelfDestruction();
                }
            }
            //If not engaged in combat, pick a random spawn location to visit if Offense based or move towards the closest bot if Defence based
            else if (botCtrl.currentState == EnemyAIController.BotState.IDLE)
            {
                //If there selected bot is a scout type, instruct bot for recon tactics, otherwise instruct for flocking tactics
                if (botCtrl.botClass == EnemyAIController.BotClass.SCOUT)
                {
                    int spawnId = Random.Range(0, spawnVolumes.Length);
                    float xSpawnCoord = Random.Range(spawnVolumes[spawnId].bounds.min.x, spawnVolumes[spawnId].bounds.max.x);
                    float zSpawnCoord = Random.Range(spawnVolumes[spawnId].bounds.min.z, spawnVolumes[spawnId].bounds.max.z);
                    Vector3 reconPos = new Vector3(xSpawnCoord, 0.0f, zSpawnCoord);
                    if ((reconPos - botCtrl.transform.position).magnitude > botCtrl.agent.stoppingDistance)
                    {
                        botCtrl.SearchForTargetAtPos(reconPos);
                    }
                    else
                    {
                        botCtrl.SearchForTargetAtPos(GetNearestBot(spawnVolumes[spawnId].center).transform.position);
                    }
                }
                else
                {
                    botCtrl.SearchForTargetAtPos(GetNearestBot(spawnedBots[selectedBot].transform.position).transform.position);
                }

            }
        }
    }

    void SpawnBot(Vector3 spawnPos)
    {
        GameObject spawnedBot = Instantiate(enemyBot, spawnPos, enemyBot.transform.rotation);
        int classChance = Random.Range(0, 10);
        if (classChance < 5)
        {
            spawnedBot.GetComponent<EnemyAIController>().botClass = EnemyAIController.BotClass.SCOUT;
        }
        else
        {
            spawnedBot.GetComponent<EnemyAIController>().botClass = EnemyAIController.BotClass.GRUNT;
        }
        GameObject spawnedBotTarget = new GameObject("Enemy Bot Target");
        spawnedBotTarget.transform.position = spawnedBot.transform.position;
        spawnedBotTarget.transform.rotation = spawnedBot.transform.rotation;
        spawnedBot.GetComponent<EnemyAIController>().target = spawnedBotTarget.transform;
        spawnedBots.Add(spawnedBot);
    }

    public GameObject GetNearestBot(Vector3 queryPos)
    {
        if(spawnedBots.Count < 2)
        {
            return null;
        }
        float shortestDist = (spawnedBots[0].transform.position - queryPos).magnitude;
        int shortestDistBotId = 0;
        for (int i = 1; i < spawnedBots.Count; ++i)
        {
            float dist = (spawnedBots[i].transform.position - queryPos).magnitude;
            if (dist < shortestDist && dist != 0.0f)
            {
                shortestDist = dist;
                shortestDistBotId = i;
            }
        }

        return spawnedBots[shortestDistBotId];
    }

    public Vector3 GetNearestSpawnVolume(Vector3 queryPos, float minDist)
    {
        float shortestDist = (spawnVolumes[0].transform.position - queryPos).magnitude;
        int shortestDistSpawnVolumeId = 0;
        for (int i = 1; i < spawnVolumes.Length; ++i)
        {
            float dist = (spawnVolumes[i].transform.position - queryPos).magnitude;
            if (dist < shortestDist && dist > minDist)
            {
                shortestDist = dist;
                shortestDistSpawnVolumeId = i;
            }
        }

        return spawnVolumes[shortestDistSpawnVolumeId].center;
    }

    public GameObject GetBotAtPos(Vector3 pos)
    {
        if(spawnedBots.Count > 0)
        {
            for (int i = 0; i < spawnedBots.Count; ++i)
            {
                if (spawnedBots[i].transform.position == pos)
                {
                    return spawnedBots[i];
                }
            }
        }
        return null;
    }

    public void DeclarePlayerVictory()
    {
        foreach(GameObject bot in spawnedBots)
        {
            bot.gameObject.SetActive(false);
        }
        VictoryPanel.gameObject.SetActive(true);
        player.SetActive(false);
    }

    public void DeclarePlayerDefeat()
    {
        foreach (GameObject bot in spawnedBots)
        {
            bot.gameObject.SetActive(false);
        }
        DefeatPanel.gameObject.SetActive(true);
        player.SetActive(false);
    }
}
