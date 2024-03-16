using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class PlayerController : MonoBehaviour, IDamageable
{
    public enum FacingDir
    {
        Right,
        Left
    }

    [Header("Player State Machine")]
    protected StateMachine playerSM;
    public StateMachine fsm => playerSM; // Properties for the states
    public PlayerData Data => data;
    public Rigidbody2D Rb2d => rb2d;
    public Animator Animator => animator;
    public Vector2 MoveDir => moveDir;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool IsSwapping => isSwapping;
    public void ToggleIsAttacking(bool b)
    {
        isAttacking = b;
    }

    [Header("Player Stats")]
    [SerializeField] PlayerData data;
    private int hp;
    public int HP { get { return hp; } private set { hp = value; OnHPChanged?.Invoke(value); } }
    public UnityAction<int> OnHPChanged;

    [Header("Player Move")]
    [SerializeField] float moveSpeed;
    Vector2 moveDir;
    public FacingDir facingDir;

    [Header("Player Jump")]
    [SerializeField] float jumpPower;
    [SerializeField] int jumpCount;
    [SerializeField] float fallMultiplier = 2.5f;
    [SerializeField] float coyoteTime;
    [SerializeField] LayerMask groundLayer;
    int remainingJumps;
    Vector2 jumpVec;
    float coyoteTimeCounter;
    bool isGrounded;

    [Header("Player Dash")]
    [SerializeField] int dashCount;
    [SerializeField] float dashPower;
    [SerializeField] float dashDuration;
    [SerializeField] float dashCooldown;
    bool isDashing;

    [Header("Player Attack")]
    [SerializeField] float attackRange;
    [SerializeField] int attackDamage;
    [SerializeField] LayerMask hittableMask;
    public bool isAttacking;

    [Header("Player Skills")]
    
    protected float cooldownTimer = 0f;
    public float CooldownRatio => cooldownTimer / data.skill1Cooldown; // TODO: refactor this
    protected bool isSwapping;
 

    [Header("Player Interact")]
    [SerializeField] float interactRange;
    [SerializeField] LayerMask interactableMask;

    [Header("Effects")]
    SmokeSpawner smokeSpawner;
    PooledObject playerHitEffectPrefab;

    [Header("Player Mask")]
    [SerializeField] private LayerMask playerMask;
    public LayerMask Mask => playerMask;

    [Header("Misc")]
    protected Rigidbody2D rb2d;
    protected Animator animator;
    Collider2D[] colliders = new Collider2D[15];
    Collider2D playerCollider;
    Collider2D platformCollider;


    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        smokeSpawner = GetComponentInChildren<SmokeSpawner>();
        playerCollider = GetComponent<BoxCollider2D>();
        playerHitEffectPrefab = Manager.Resource.Load<PooledObject>("Prefabs/PlayerHitEffect");
        dashesLeft = dashCount;

        // Cache jump vector once to prevent repetitive math operations
        jumpVec = Vector2.up * Physics2D.gravity.y * fallMultiplier * Time.fixedDeltaTime;

        playerSM = new StateMachine(this);
    }

    private void Start()
    {
        playerSM.Initialize(playerSM.idleState);
    }

    private void Update()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
        playerSM.Update();
    }

    private void FixedUpdate()
    {
        if (isDashing)
            return;
        
        Move();
        JumpFall();
    }

    #region Collision

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (groundLayer.Contains(collision.gameObject.layer))
        {
            isGrounded = true;
            remainingJumps = jumpCount;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (groundLayer.Contains(collision.gameObject.layer))
        {
            isGrounded = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Platform down jump
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            platformCollider = collision.collider;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Platform"))
        {
            platformCollider = null;
        }
    }

    #endregion

    #region Movement
    private void Move()
    {
        Vector2 newVel = rb2d.velocity;
        newVel.x = moveDir.x * moveSpeed;

        if ((isAttacking && isGrounded) || isSwapping) // prevent movement while attacking
        {
            newVel.x = 0;
        }

        rb2d.velocity = newVel;
    }
    private void Flip()
    {
        if (isSwapping)
            return;

        Vector3 newScale = new Vector3(1, 1, 1);
        if (moveDir.x < 0)
        {
            newScale.x = -1;
            transform.localScale = newScale;
            facingDir = FacingDir.Left;
        }
        else if (moveDir.x > 0)
        {
            transform.localScale = newScale;
            facingDir = FacingDir.Right;
        }
    }
    #endregion

    #region Jump

    // BASIC JUMP
    private void Jump()
    {
        if (moveDir.y == -1 && platformCollider != null)
        {
            StartCoroutine(TemporaryIgnoreCollision());
            return;
        }

        if (coyoteTimeCounter <= 0 && remainingJumps == 0)
            return;
        if (remainingJumps == 1) // double jump smoke effect
            smokeSpawner.SpawnSmoke(SmokeSpawner.SmokeType.Jump);

        remainingJumps--;
        coyoteTimeCounter = 0;
        rb2d.velocity = new Vector2(rb2d.velocity.x, jumpPower);
    }

    // TODO: STUDY THIS https://www.youtube.com/watch?v=7KiK0Aqtmzc&t=518s
    void JumpFall()
    {
        if (rb2d.velocity.y < 0)
        {
            rb2d.velocity += jumpVec;
        }
    }

    // Platform down jump
    IEnumerator TemporaryIgnoreCollision()
    {
        Collider2D coll = platformCollider;
        Physics2D.IgnoreCollision(playerCollider, coll, true);
        yield return new WaitForSeconds(0.5f);
        Physics2D.IgnoreCollision(playerCollider, coll, false);
    }

    #endregion

    #region Dash
    // TODO: Implement double dash
    int dashesLeft;
    private IEnumerator Dash()
    {
        if (isDashing || dashesLeft == 0)
            yield break;

        // dash start
        if(dashCooldownRoutine != null)
            StopCoroutine(dashCooldownRoutine); // reset cooldown routine
        dashCooldownRoutine = StartCoroutine(DashCooldownRoutine());
        smokeSpawner.SpawnSmoke(SmokeSpawner.SmokeType.Dash);
        playerSM.Trigger(TriggerType.DashTrigger);
        isDashing = true;

        // dash physics
        float originalGravity = rb2d.gravityScale;
        rb2d.gravityScale = 0;
        rb2d.velocity = ((facingDir == FacingDir.Left) ? Vector2.left : Vector2.right) * dashPower;
        yield return new WaitForSeconds(dashDuration);
        rb2d.velocity = new Vector3(0, rb2d.velocity.y, 0); // prevent sliding
        rb2d.gravityScale = originalGravity;

        // dash end
        isDashing = false;
        dashesLeft--;
    }

    Coroutine dashCooldownRoutine;
    private IEnumerator DashCooldownRoutine()
    {
        yield return new WaitForSeconds(dashCooldown);
        dashesLeft = dashCount;
    }

    #endregion

    #region Attack
    private void HandleAttackInput()
    {
        playerSM.Trigger(TriggerType.AttackTrigger);
    }
    public void Attack()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, attackRange, colliders, hittableMask);
        for(int i = 0; i < count; i++)
        {
            IDamageable[] damageables = colliders[i].GetComponents<IDamageable>();
            foreach(IDamageable damageable in damageables)
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }
    public void TakeDamage(int damage)
    {
        Manager.Pool.GetPool(playerHitEffectPrefab, transform.position, Quaternion.identity);
        HP -= damage;
        if (HP <= 0)
        {
            //Die
            return;
        }
    }
    #endregion

    #region Skills

    protected virtual void UseSkill1() { }
    protected virtual void UseSkill2() { }
    protected virtual void Swap() { }

    #endregion

    #region Interact
    private void Interact()
    {
        Collider2D collider = Physics2D.OverlapCircle(transform.position, interactRange, interactableMask);
        if (collider == null || collider.GetComponent<IInteractable>() == null)
            return;

        collider.GetComponent<IInteractable>().Interact();
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    #endregion

    #region Inputs
    private void OnMove(InputValue value)
    {
        moveDir = value.Get<Vector2>();
        Flip();
    }
    private void OnJump()
    {
        if (isSwapping)
            return;
        Jump();
    }
    private void OnDash()
    {
        StartCoroutine(Dash());
    }
    private void OnSwap()
    {
        if (isSwapping)
            return;
        Swap();
    }
    private void OnAttack()
    {
        HandleAttackInput();
    }
    private void OnSkill1()
    {
        UseSkill1();
    }
    private void OnSkill2()
    {
        UseSkill2();
    }
    private void OnInteract()
    {
        Interact();
    }

    #endregion
}
