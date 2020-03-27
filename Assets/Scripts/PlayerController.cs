using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    private CharacterController PlayerCharacter;

    [Header("Movement")]
    public float PlayerMinMoveSpeed = 1.0f;
    public float PlayerMaxMoveSpeed = 8.0f;
    public float PlayerAccelerationSpeed = 1.0f;
    public float PlayerJumpForce = 10.0f;
    public float PlayerTurnSpeed = 20.0f;
    private float PlayerStandardMaxMS;
    private float PlayerBoostedMaxMS;

    [Header("Gravity")]
    public float PlayerGravitySim = 5.0f;
    private Vector3 PlayerMovement = Vector3.zero;
    
    [Header("Camera")]
    public Camera CharacterCamera;
    private Plane PlayerPlane;

    [Header("Combat")]
    public GameObject PlayerWeapon;
    private LineRenderer WeaponLineRenderer;
    private float ShootCooldown;
    public float ShootRate = 0.25f;
    public WaitForSeconds ShotDuration = new WaitForSeconds(0.25f);
    public float WeaponRange = 20.0f;
    public float WeaponDamage = 40.0f;

    public ParticleSystem DamageParticle;

    public GameObject projectilePrefab;
    public GameObject[] projectiles;
    public int projectilePoolSize;

    [Header("Stats")]
    public float PlayerMaxHealth;
    public float PlayerHealth = 100.0f;
    public float PlayerMaxStamina;
    public float PlayerStamina = 100.0f;
    public WaitForSeconds HealthTick = new WaitForSeconds(0.4f);
    public WaitForSeconds StaminaTick = new WaitForSeconds(0.2f);

    [Header("Interface")]
    public Image PlayerHealthDisplay;
    public Image PlayerStaminaDisplay;

    public bool hasKeycard = false;
    private GameObject Director;

    private void Awake()
    {
        PlayerCharacter = GetComponent<CharacterController>();
        WeaponLineRenderer = GetComponent<LineRenderer>();

        PlayerMaxHealth = PlayerHealth;
        PlayerMaxStamina = PlayerStamina;

        PlayerStandardMaxMS = PlayerMaxMoveSpeed;
        PlayerBoostedMaxMS = PlayerMaxMoveSpeed * 1.5f;

        projectiles = new GameObject[projectilePoolSize];
        for (int i = 0; i < projectilePoolSize; ++i)
        {
            projectiles[i] = Instantiate(projectilePrefab, transform.position + transform.forward * 2.0f, projectilePrefab.transform.rotation);
        }

        Director = GameObject.FindGameObjectWithTag("Director");
    }

    // Update is called once per frame
    void Update()
    {
        PlayerPlane = new Plane(Vector3.up, transform.position);
        Ray ray = CharacterCamera.ScreenPointToRay(Input.mousePosition);

        float hitDist = 0.0f;
        if(PlayerPlane.Raycast(ray, out hitDist))
        {
            Vector3 targetPoint = ray.GetPoint(hitDist);
            Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10.0f * Time.deltaTime);
        }

        PlayerMovement = new Vector3(
            Input.GetAxis("Horizontal") * Mathf.Lerp(PlayerMinMoveSpeed, PlayerMaxMoveSpeed, PlayerAccelerationSpeed),
            PlayerMovement.y,
            Input.GetAxis("Vertical") * Mathf.Lerp(PlayerMinMoveSpeed, PlayerMaxMoveSpeed, PlayerAccelerationSpeed));

        PlayerCharacter.Move(PlayerMovement * Time.deltaTime);

        if (Input.GetButtonDown("Sprint") && PlayerStamina > 0)
        {
            StartCoroutine("Sprint");
            StopCoroutine("RecoverStamina");
        }

        if (Input.GetButtonUp("Sprint") || PlayerStamina <= 0)
        {
            StartCoroutine("RecoverStamina");
            StopCoroutine("Sprint");
        }

        if(PlayerHealth == PlayerMaxHealth)
        {
            StopCoroutine("RecoverHealth");
        }

        PlayerHealthDisplay.fillAmount = PlayerHealth / PlayerMaxHealth;
        PlayerStaminaDisplay.fillAmount = PlayerStamina / PlayerMaxStamina;

        if (Input.GetMouseButtonDown(0) && Time.time > ShootCooldown)
        {
            ShootCooldown = Time.time + ShootRate;
            WeaponLineRenderer.SetPosition(0, PlayerWeapon.transform.position);
            RaycastHit hit;
            Vector3 targetPoint = ray.GetPoint(hitDist);
            WeaponLineRenderer.SetPosition(1, targetPoint);
            if (Physics.Raycast(
                PlayerWeapon.transform.position, 
                -PlayerWeapon.transform.up, 
                out hit,
                (targetPoint - PlayerWeapon.transform.position).magnitude))
            {
                
                WeaponLineRenderer.SetPosition(1, hit.point);
            }
            StartCoroutine(ShotDisplay(targetPoint - PlayerWeapon.transform.position));
        }
    }

    private IEnumerator ShotDisplay(Vector3 targetDir)
    {
        WeaponLineRenderer.enabled = true;
        yield return ShotDuration;
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
        WeaponLineRenderer.enabled = false;
    }

    private IEnumerator Sprint()
    {
        while (PlayerStamina > 0)
        {
            PlayerMaxMoveSpeed = PlayerBoostedMaxMS;
            PlayerStamina -= PlayerMaxStamina * 0.1f;

            if (PlayerStamina < 0)
            {
                PlayerStamina = 0.0f;
            }
            yield return StaminaTick;
        }
    }

    private IEnumerator RecoverStamina()
    {
        while (PlayerStamina < PlayerMaxStamina)
        {
            PlayerMaxMoveSpeed = PlayerStandardMaxMS;
            PlayerStamina += PlayerMaxStamina * 10.0f * Time.deltaTime;

            if (PlayerStamina > PlayerMaxStamina)
            {
                PlayerStamina = PlayerMaxStamina;
            }
            yield return StaminaTick;
        }
    }

    public void InflictDamage(float damage)
    {
        PlayerHealth -= damage;

        if (PlayerHealth <= 0)
        {
            PlayerHealth = 0;
            PlayerHealthDisplay.fillAmount = 0;
            Director.GetComponent<DirectorAIController>().DeclarePlayerDefeat();
        }
        else
        {
            StartCoroutine("RecoverHealth");
        }
    }

    private IEnumerator RecoverHealth()
    {
        DamageParticle.Play();
        while (PlayerHealth < PlayerMaxHealth)
        {
            PlayerHealth += PlayerMaxHealth * Time.deltaTime;

            if (PlayerHealth >= PlayerMaxHealth)
            {
                PlayerHealth = PlayerMaxHealth;
            }
            yield return HealthTick;
        }
    }

    public void PickupHealthOrb(float bonusHealth)
    {
        PlayerHealth += bonusHealth;
        if(PlayerHealth > PlayerMaxHealth)
        {
            PlayerHealth = PlayerMaxHealth;
        }
    }

    public void PickupStaminaOrb(float bonusStamina)
    {
        PlayerStamina += bonusStamina;
        if (PlayerStamina > PlayerMaxStamina)
        {
            PlayerStamina = PlayerMaxStamina;
        }
    }

    public void PickupKeycard()
    {
        hasKeycard = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "Portal" && hasKeycard)
        {
            Director.GetComponent<DirectorAIController>().DeclarePlayerVictory();
        }
    }
}
