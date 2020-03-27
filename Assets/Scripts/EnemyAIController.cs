using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAIController : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform target;
    private GameObject player;
    private DirectorAIController director;

    private Vector3 playerLastSeen;
    private Vector3 reverseDir;

    public GameObject healthOrb;
    public GameObject staminaOrb;

    public ParticleSystem smokeParticle;
    public ParticleSystem explosionParticle;
    public ParticleSystem trailParticle;

    public GameObject projectilePrefab;
    public GameObject[] projectiles;
    public int projectilePoolSize;

    public enum BotClass { SCOUT, GRUNT }
    public BotClass botClass;

    public float maxHealthScout;
    public float maxHealthGrunt;

    public float maxSpeedScout;
    public float maxSpeedGrunt;

    public float maxVisDistScout;
    public float maxVisDistGrunt;

    public float maxViewAngleScout;
    public float maxViewAngleGrunt;

    public float idleDurationScout;
    public float idleDurationGrunt;

    public float chaseDurationScout;
    public float chaseDurationGrunt;

    public float predictThreatScout;
    public float predictThreatGrunt;

    public float fireDowntimeScout;
    public float fireDowntimeGrunt;

    public float currentMaxHealth;
    public float currentHealth;
    private float currentVisDist;
    private float currentViewAngle;
    private float currentPredictThreat;

    public float selfDestructImpactRange;
    public float selfDestructDamage;
    public float selfDestructTime;

    public float firingAccuracyScout;
    public float firingAccuracyGrunt;

    public Color trailColorScout;
    public Color trailColorGrunt;

    public enum BotState { IDLE, CHASING, SEARCHING, ASSIST, CRASH }
    public BotState currentState;
    private bool isIdle, isChasing, isSearching, isAssisting, isCrashed;
    private WaitForSeconds idleRefreshDur, chaseRefreshDur;
    
    public enum BotStance { OFFENSIVE, DEFENSIVE }
    public BotStance currentStance;

    public float maxTurnRateScout;
    public float maxTurnRateGrunt;

    private float currentTurnRate;
    public float aimDuration;
    private float currentAimTime;
    private float firingAccuracy;

    private bool dodgeLeft;
    private bool dodgingAttack;
    private bool changeFwDir;

    [Header("Combat")]
    public GameObject weapon;
    private LineRenderer weaponLineRenderer;
    private float shootCooldown;
    public float shootRate;
    private float fireDowntime;
    public float weaponRange;
    public float weaponDamage;

    private WaitForSeconds calibrateShotDuration;

    public float offensiveStanceBias;
    public float defensiveStanceBias;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player");
        director = GameObject.FindGameObjectWithTag("Director").GetComponent<DirectorAIController>();

        currentState = BotState.IDLE;

        playerLastSeen = new Vector3();
        reverseDir = new Vector3();
        changeFwDir = false;

        weaponLineRenderer = GetComponent<LineRenderer>();

        projectiles = new GameObject[projectilePoolSize];
        for(int i = 0; i < projectilePoolSize; ++i)
        {
            projectiles[i] = Instantiate(projectilePrefab, transform.position + transform.forward * 2.0f, projectilePrefab.transform.rotation);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        ParticleSystem.MainModule psMain = trailParticle.main;

        switch (botClass)
        {
            case BotClass.SCOUT:
                
                currentHealth = currentMaxHealth = maxHealthScout;
                agent.speed = maxSpeedScout;

                currentViewAngle = maxViewAngleScout;
                currentVisDist = maxVisDistScout;

                currentTurnRate = maxTurnRateScout;
                currentPredictThreat = predictThreatScout;

                idleRefreshDur = new WaitForSeconds(idleDurationScout);
                chaseRefreshDur = new WaitForSeconds(chaseDurationScout);

                fireDowntime = fireDowntimeScout;
                firingAccuracy = firingAccuracyScout;

                psMain.startColor = new Color(trailColorScout.r, trailColorScout.g, trailColorScout.b);

                break;

            case BotClass.GRUNT:
                
                currentHealth = currentMaxHealth = maxHealthGrunt;
                agent.speed = maxSpeedGrunt;

                currentViewAngle = maxViewAngleGrunt;
                currentVisDist = maxVisDistGrunt;
               
                currentTurnRate = maxTurnRateGrunt;
                currentPredictThreat = predictThreatGrunt;

                idleRefreshDur = new WaitForSeconds(idleDurationGrunt);
                chaseRefreshDur = new WaitForSeconds(chaseDurationGrunt);

                fireDowntime = fireDowntimeGrunt;
                firingAccuracy = firingAccuracyGrunt;

                psMain.startColor = new Color(trailColorGrunt.r, trailColorGrunt.g, trailColorGrunt.b);

                break;
        }

        calibrateShotDuration = new WaitForSeconds(fireDowntime);

        //Bias evaluation used to identify initial stance based on archetype-defining attributes 
        //Scout predisposed to a defensive bias & Grunt to offensive bias
        offensiveStanceBias = (currentMaxHealth / currentHealth - currentPredictThreat) * currentHealth;
        defensiveStanceBias = (currentHealth / currentMaxHealth) * (currentHealth * (agent.speed * fireDowntime));

        if (offensiveStanceBias > defensiveStanceBias)
        {
            currentStance = BotStance.OFFENSIVE;
        }
        else
        {
            currentStance = BotStance.DEFENSIVE;
        }

    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case BotState.IDLE:
                if(!isIdle)
                {
                    isIdle = true;
                    StartCoroutine(Idle());
                }
                break;
            case BotState.CHASING:
                if (!isChasing)
                {
                    isChasing = true;
                    StartCoroutine(Chase());
                }
                break;
            case BotState.SEARCHING:
                if (!isSearching)
                {
                    isSearching = true;
                    StartCoroutine(Search());
                }
                break;
            case BotState.ASSIST:
                if (!isAssisting)
                {
                    isAssisting = true;
                    StartCoroutine(Assist());
                }
                break;
            case BotState.CRASH:
                if (!isCrashed)
                {
                    isCrashed = true;
                    StartCoroutine(SelfDestruct());
                }
                break;
        }

        //Dynamic stance adaptation
        offensiveStanceBias = (currentMaxHealth / currentHealth - currentPredictThreat) * currentHealth;
        defensiveStanceBias = (currentHealth / currentMaxHealth) * (currentHealth * (agent.speed * fireDowntime));

        if (offensiveStanceBias > defensiveStanceBias)
        {
            currentStance = BotStance.OFFENSIVE;
        }
        else
        {
            currentStance = BotStance.DEFENSIVE;
        }

        if (target != null)
            agent.SetDestination(target.position);
        if (agent.remainingDistance > agent.stoppingDistance)
        {
            agent.Move(agent.desiredVelocity * Time.deltaTime);
        }
        //Switch dodging direction each time a dodge maneuver is complete
        else if (dodgingAttack)
        {
            AimAtPlayer();
            dodgingAttack = false;
            dodgeLeft = !dodgeLeft;
        }
    }

    bool ScanForPlayer()
    {
        Vector3 dirToPlayer = player.transform.position - transform.position;
        if (Vector3.Angle(transform.forward, dirToPlayer) < currentViewAngle)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + new Vector3(0.0f, 0.5f, 0.0f), dirToPlayer, out hit, currentVisDist))
            {
                if (hit.collider.tag.Equals("Player"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void AimAtPlayer()
    {
        Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
        if (dirToPlayer != transform.forward)
        {
            currentAimTime += currentTurnRate * Time.deltaTime;
            Quaternion playerLook = Quaternion.LookRotation(dirToPlayer, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, playerLook, currentAimTime / aimDuration);
        }
        else
        {
            currentAimTime = 0.0f;
        }
    }

    bool InPlayerAim()
    {
        Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;

        if (Vector3.Dot(player.transform.forward, dirToPlayer) < currentPredictThreat)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    void DodgeAttack()
    {
        if(!dodgingAttack)
        {
            dodgingAttack = true;
            if(dodgeLeft)
            {
                target.position = player.transform.position - player.transform.forward * agent.stoppingDistance - player.transform.right * agent.stoppingDistance;
            }
            else
            {
                target.position = player.transform.position - player.transform.forward * agent.stoppingDistance + player.transform.right * agent.stoppingDistance;
            }
        }
    }

    void FireAtTarget()
    {
        if(Time.time > shootCooldown)
        {
            shootCooldown = Time.time + shootRate;

            RaycastHit hit;
            weaponLineRenderer.SetPosition(0, weapon.transform.position);
            weaponLineRenderer.SetPosition(1, -weapon.transform.up * weaponRange);
            if (Physics.Raycast(
                weapon.transform.position,
                -weapon.transform.up,
                out hit,
                weaponRange))
            {
                weaponLineRenderer.SetPosition(1, hit.point);
            }
            StartCoroutine(ShotDisplay(-weapon.transform.up * weaponRange));
        }
    }

    void LookAround()
    {
        if (!changeFwDir)
        {
            changeFwDir = true;
            reverseDir = -transform.forward;
        }      
        else if (transform.forward == reverseDir)
        {
            changeFwDir = false;
            currentAimTime = 0.0f;
        }

        currentAimTime += currentTurnRate * Time.deltaTime;

        Quaternion reverseLook = Quaternion.LookRotation(reverseDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, reverseLook, currentAimTime / aimDuration);
    }

    bool LocateNearestBot()
    {
        GameObject nearestBot = director.GetNearestBot(transform.position);
        if (!nearestBot)
        {
            return false;
        }
        target.position = director.GetNearestBot(transform.position).transform.position;
        return true;
    }

    private IEnumerator SelfDestruct()
    {
        while(isCrashed)
        {
            selfDestructTime -= Time.deltaTime;

            if (selfDestructTime < 0.5f)
            {
                explosionParticle.Play();
            }

            if (selfDestructTime < 0.0f)
            {
                float distToPlayer = (player.transform.position - transform.position).magnitude;
                if (distToPlayer < selfDestructImpactRange)
                {
                    player.GetComponent<PlayerController>().InflictDamage(selfDestructDamage);
                }
                float orbDropChance = Random.Range(0.0f, 10.0f);
                if (orbDropChance < 5.0f)
                {
                    Instantiate(healthOrb, transform.position, new Quaternion());
                }
                else
                {
                    Instantiate(staminaOrb, transform.position, new Quaternion());
                }

                director.spawnedBots.Remove(gameObject);

                Collider[] nearbyBotsHit = Physics.OverlapSphere(transform.position, selfDestructImpactRange);
                if(nearbyBotsHit.Length > 0)
                {
                    foreach(Collider botCol in nearbyBotsHit)
                    {
                        if (botCol.gameObject.tag == gameObject.tag)
                        {
                            botCol.GetComponent<EnemyAIController>().InflictDamage(selfDestructDamage);
                        }
                    }
                }

                Destroy(gameObject);
            }

            yield return null;
        }
    }

    public void InflictDamage(float damage)
    {
        currentHealth -= damage;

        explosionParticle.Play();

        if (currentHealth <= 0.0f)
        {
            smokeParticle.Play();
            currentState = BotState.CRASH;
        }
    }

    private IEnumerator ShotDisplay(Vector3 targetDir)
    {
        weaponLineRenderer.enabled = true;
        yield return calibrateShotDuration;
        for (int i = 0; i < projectilePoolSize; ++i)
        {
            if (!projectiles[i].activeInHierarchy)
            {
                projectiles[i].transform.position = transform.position + targetDir.normalized * 2.0f;
                projectiles[i].transform.rotation = Quaternion.LookRotation(transform.up, targetDir);
                projectiles[i].SetActive(true);
                break;
            }
        }
        weaponLineRenderer.enabled = false;
    }

    private IEnumerator Idle()
    {
        while (isIdle)
        {
            //If you see the player
            if (ScanForPlayer())
            {
                playerLastSeen = player.transform.position;
                AimAtPlayer();
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, dirToPlayer) > firingAccuracy)
                {
                    FireAtTarget();
                }
                //If the player is aimming at you
                if (InPlayerAim())
                {
                    DodgeAttack();                  
                }

                if (currentStance == BotStance.OFFENSIVE)
                {
                    currentState = BotState.CHASING;
                    isIdle = false;
                }
                else if (LocateNearestBot())
                {
                    currentState = BotState.ASSIST;
                    isIdle = false;
                }
            }
            else
            {
                LookAround();
            }
            yield return null;
        }
    }

    private IEnumerator Chase()
    {
        while(isChasing)
        {
            //if you see the player
            if (ScanForPlayer())
            {
                playerLastSeen = player.transform.position;
                AimAtPlayer();
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, dirToPlayer) > firingAccuracy)
                {
                    FireAtTarget();
                }
                if(!dodgingAttack)
                {
                    target.position = playerLastSeen;
                }
                //If the player is aimming at you
                if (InPlayerAim())
                {
                    DodgeAttack();
                }
            }
            else
            {
                float reconNearestSpawnVolumeChance = Random.Range(0.0f, 10.0f);
                if(reconNearestSpawnVolumeChance > 5.0f)
                {
                    target.position = director.GetNearestSpawnVolume(playerLastSeen, agent.stoppingDistance);
                }
                currentState = BotState.SEARCHING;
                isChasing = false;
                //yield return chaseRefreshDur;
            }

            yield return null;
        }
    }

    private IEnumerator Search()
    {
        while(isSearching)
        {
            if (ScanForPlayer())
            {
                playerLastSeen = target.transform.position = player.transform.position;
                isSearching = false;
                if(currentStance == BotStance.OFFENSIVE)
                {
                    currentState = BotState.CHASING;
                }
                else if (LocateNearestBot())
                {
                    currentState = BotState.ASSIST;
                }
            }
            else if (agent.velocity.sqrMagnitude > 0.01f)
            {
                //Debug.DrawRay(transform.position, agent.desiredVelocity, new Color(1,0,1), 0.01f);
                transform.rotation = Quaternion.LookRotation(agent.velocity.normalized, transform.up);
            }

            if ((target.position - transform.position).magnitude <= agent.stoppingDistance)
            {
                isSearching = false;
                currentState = BotState.IDLE;
            }

            yield return null;
        }
    }

    private IEnumerator Assist()
    {
        while(isAssisting)
        {
            if (ScanForPlayer())
            {
                playerLastSeen = player.transform.position;
                AimAtPlayer();
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, dirToPlayer) > firingAccuracy)
                {
                    FireAtTarget();
                }
                //If the player is aimming at you
                if (InPlayerAim())
                {
                    DodgeAttack();
                }
            }
            else if (agent.velocity.sqrMagnitude > 0.01f)
            {
                //Debug.DrawRay(transform.position, agent.velocity);
                transform.rotation = Quaternion.LookRotation(agent.velocity.normalized, transform.up);
            }

            if ((target.position - transform.position).magnitude <= agent.stoppingDistance)
            {
                GameObject assistedBot = director.GetBotAtPos(target.position);
                if(assistedBot)
                {
                    assistedBot.GetComponent<EnemyAIController>().SearchForTargetAtPos(playerLastSeen);
                }
                target.transform.position = playerLastSeen;
                isAssisting = false;
                currentState = BotState.SEARCHING;
            }

            yield return null;
        }
    }

    public void SearchForTargetAtPos(Vector3 pos)
    {
        isIdle = false;
        target.position = pos;
        currentState = BotState.SEARCHING;
    }

    //Goodbye cruel world :(
    public void InitiateSelfDestruction()
    {
        target.position = playerLastSeen;
        dodgingAttack = false;
        selfDestructTime *= 0.5f;
        agent.speed *= 1.5f;
        InflictDamage(currentHealth);
    }
}
