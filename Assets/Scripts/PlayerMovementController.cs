using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character.Assets.Scripts;
using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{

    // TODO: read about execute into animator parameters
    public enum AnimationReference
    {
        speedForward,
        rotateSpeed,
        isWalking,
        isRunning,
        isRolling,
        isJumping,
        isIdle,
        horizontalInput,
        verticalInput,
        rotateInput,
        velocityY
    }

    // TODO: Why Unity hasn't done for us? 😁😁😁 
    protected Dictionary<AnimationReference, int> hashOfAnimationDictionary = new Dictionary<AnimationReference, int>();


    // TODO: List<KeyCode> or new input system of unity, after send. Also need add a triggers, which could be events from keycodes
    [Header("Keybinds")]
    [SerializeField]
    protected KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField]
    protected KeyCode jumpKey = KeyCode.Space;
    [SerializeField]
    protected KeyCode rollKey = KeyCode.LeftAlt;
    [SerializeField]
    protected KeyCode rotateQ = KeyCode.Q;
    [SerializeField]
    protected KeyCode rotateE = KeyCode.E;

    [Header("Rotation")]
    float rotateInput = 0;

    [SerializeField]
    protected float rotationSpeed = 100f;

    [Header("Ground Check")]
    [SerializeField]
    protected LayerMask WhatIsGround;
    [SerializeField]
    protected bool grounded;

    [SerializeField]
    protected bool rolled;

    [SerializeField]
    protected Transform orientation;

    float horizontalInput;
    float verticalInput;
    Vector3 moveDirection;
    Rigidbody rb;
    Vector3 m_EulerAngleVelocity;
    CapsuleCollider capsuleCollider;
    Animator animator;

    // TODO: add states queue or something redux fraemwork for C#;  
    [SerializeField]
    protected MovementState state;

    [SerializeField]
    protected bool exitingSlope = false;
    [SerializeField]
    protected float startYScale = 0f;
    [SerializeField]
    protected RaycastHit slopeHit;
    [SerializeField]
    protected float maxSlopeAngle;


    [Header("Movement")]
    private float moveSpeed;

    [SerializeField]
    protected float walkSpeed;
    [SerializeField]
    protected float sprintSpeed;

    [SerializeField]
    protected float groundDrag;

    [Header("Jump")]
    [SerializeField]
    protected ActionModel jumpActionModel = new ActionModel();

    [Header("Roll")]
    [SerializeField]
    protected ActionModel rollActionModel = new ActionModel(1, 50, 1.2f, ForceMode.Force, true);

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        // rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        grounded = Physics.CheckCapsule(capsuleCollider.bounds.center, new Vector3(capsuleCollider.bounds.center.x, capsuleCollider.bounds.min.y, capsuleCollider.bounds.center.z), 0.18f, WhatIsGround);

        m_EulerAngleVelocity = new Vector3(0, rotationSpeed, 0);

        animator = GetComponent<Animator>();

        foreach (AnimationReference animationReference in Enum.GetValues(typeof(AnimationReference)))
        {
            hashOfAnimationDictionary.Add(animationReference, Animator.StringToHash(animationReference.ToString()));
        }

        startYScale = transform.localScale.y;
    }

    // Update is called once per frame
    private void Update()
    {
        MyInput();
        StateHandler();
    }

    private void FixedUpdate()
    {
        // Reflect from ground. Add check only when state changes from Jumping to Grounding
        var reflect = (rb.velocity.y < 0) ? rb.velocity.y / 10 : 0;
        // - rb.velocity.y / 20 - prechecker depend on velocity Y
        grounded = Physics.CheckCapsule(capsuleCollider.bounds.center, new Vector3(capsuleCollider.bounds.center.x, capsuleCollider.bounds.min.y, capsuleCollider.bounds.center.z), 0.18f, WhatIsGround);

        SpeedControl();
        MovePlayer();
        Rotation();

        if (
            jumpActionModel.readyToAction == false
            && jumpActionModel.input == true
            && state != MovementState.rolling
            && grounded
        )
        {
            jumpActionModel.input = false;
            Jump();
        }

        if (
            rollActionModel.readyToAction == false
            && rollActionModel.input == true
            && state != MovementState.jumping
            && state == MovementState.sprinting
            && grounded
        )
        {
            rollActionModel.input = false;
            Roll();
        }

        //handle drag
        if (grounded)
        {
            rb.drag = groundDrag;
        }
        else
        {
            rb.drag = 0;
        }
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (Input.GetKey(rotateQ))
        {
            rotateInput = -1;
        }
        else if (Input.GetKey(rotateE))
        {
            rotateInput = 1;
        }
        else
        {
            rotateInput = 0;
        }

        // when to jump
        if (Input.GetKeyDown(jumpKey) && grounded && jumpActionModel.readyToAction)
        {
            jumpActionModel.readyToAction = false;
            jumpActionModel.input = true;
        }

        // when to roll
        if (Input.GetKeyDown(rollKey) && Input.GetKey(sprintKey) && grounded && rollActionModel.readyToAction)
        {
            rollActionModel.readyToAction = false;
            rollActionModel.input = true;
        }

    }

    /**
     TODO: 
     build or read about prototype of stateManager there based on transition states, for example:
     if state landing change to ground, than use effect. It's will be help with separate logic from animation state and an update
     currently flow: state -> condition check -> side effects -> animation 
                                                             \-> next state 
     also check how to transition from animator could be intergrated in that stateManager 
     also think about extend base class for reflexing animation class into animator
     magic seen like that states: BaseClass or BaseStruct provide to Animator variables which will create code of state controller from flow   
    */
    private void StateHandler()
    {
        bool wasdInput = verticalInput != 0 || horizontalInput != 0;
        bool standBy = grounded && !wasdInput;

        animator.SetFloat(hashOfAnimationDictionary[AnimationReference.velocityY], rb.velocity.y);


        // Rolled
        if (!rollActionModel.readyToAction && verticalInput > 0)
        {
            state = MovementState.rolling;
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isRolling], true);

            // prevent from frame call
            if (!IsInvoking(nameof(ResetRoll)))
            {
                Invoke(nameof(ResetRoll), rollActionModel.actionCooldown);
            }

            // Prevent method from state update by another condition 
            return;
        }

        // Grounded after flying
        if (state == MovementState.wasGrounded)
        {
            state = MovementState.landing;
            if (!IsInvoking(nameof(ResetJump)))
            {
                Invoke(nameof(ResetJump), jumpActionModel.actionCooldown);
            }
        }

        // Flying or jumping to moment when grounded
        if (state == MovementState.jumping && grounded && jumpActionModel.readyToAction == false)
        {
            state = MovementState.wasGrounded;

            // Prevent method from state update by another condition 
            return;
        }

        // Idle after grounding
        if (state == MovementState.landing)
        {
            state = MovementState.idle;
        }

        // Grounded
        if (grounded)
        {
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isJumping], false);
        }

        // Flying idle
        else if (!grounded)
        {
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isJumping], true);
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.velocityY], rb.velocity.y);
            state = MovementState.jumping;
        }

        // Rotate
        if ((rotateInput != 0) && standBy)
        {
            state = MovementState.rotate;
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isIdle], false);
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.rotateInput], rotateInput, .1f, Time.deltaTime);
        }
        // If rotate wasn't input than idle state 
        else if (rotateInput == 0 && standBy)
        {
            state = MovementState.idle;
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.rotateInput], 0, .1f, Time.deltaTime);
        }

        // Movement x,z
        if (grounded && wasdInput)
        {
            if (Input.GetKey(sprintKey) && verticalInput >= 0)
            {
                state = MovementState.sprinting;
                animator.SetBool(hashOfAnimationDictionary[AnimationReference.isWalking], false);
                animator.SetBool(hashOfAnimationDictionary[AnimationReference.isRunning], true);
                moveSpeed = sprintSpeed;
            }
            else
            {
                state = MovementState.walking;
                animator.SetBool(hashOfAnimationDictionary[AnimationReference.isWalking], true);
                animator.SetBool(hashOfAnimationDictionary[AnimationReference.isRunning], false);
                moveSpeed = walkSpeed;
            }

            if (verticalInput >= 0)
            {
                animator.SetFloat(hashOfAnimationDictionary[AnimationReference.speedForward], moveSpeed);
            }
            else
            {
                animator.SetFloat(hashOfAnimationDictionary[AnimationReference.speedForward], -moveSpeed);
            }
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.horizontalInput], horizontalInput * (int)state, .1f, Time.deltaTime);
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.verticalInput], verticalInput * (int)state, .1f, Time.deltaTime);
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isIdle], false);
        }

        // Idle
        else if (standBy)
        {
            // 🤔🤔🤔🤔
            // reset all states into idle
            state = MovementState.idle;
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isIdle], true);
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isWalking], false);
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isRunning], false);
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isRolling], false);
            animator.SetBool(hashOfAnimationDictionary[AnimationReference.isJumping], false);
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.horizontalInput], 0);
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.verticalInput], 0);
            animator.SetFloat(hashOfAnimationDictionary[AnimationReference.speedForward], 0);
        }
    }

    private void Rotation()
    {
        if (rotateInput == -1)
        {
            // TODO: check solution using AddTorque for true mechanics engine;
            // haven't newPoint, it could be vector of rotation; 
            // Vector3 x = Vector3.Cross(moveDirection.normalized, newPoint.normalized);
            // float theta = Mathf.Asin(x.magnitude);
            // Vector3 w = x.normalized * theta / Time.fixedDeltaTime;

            // Quaternion q = transform.rotation * GetComponent<Rigidbody>().inertiaTensorRotation;
            // var T = q * Vector3.Scale(rb.inertiaTensor, (Quaternion.Inverse(q) * w));
            // rb.AddTorque(-Vector3.up, ForceMode.Impulse);
            Quaternion deltaRotation = Quaternion.Euler(-m_EulerAngleVelocity * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
        else if (rotateInput == 1)
        {
            Quaternion deltaRotation = Quaternion.Euler(m_EulerAngleVelocity * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        //on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        //on ground
        if (grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }

        //in air
        else if (!grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * jumpActionModel.actionMultiplier, ForceMode.Force);
        }

        //turn gravity off while on slope
        rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {

        //limit speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }

        //limiting speed on ground or in air
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            //limit velocity if needed
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }


    private void ResetJump()
    {
        jumpActionModel.ResetAction();
    }

    private void Jump()
    {
        // reset y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpActionModel.actionForce, jumpActionModel.actionForceMode);
    }

    private void Roll()
    {
        rb.AddForce(transform.forward * rollActionModel.actionForce, rollActionModel.actionForceMode);
    }

    private void ResetRoll()
    {
        rollActionModel.readyToAction = true;
        animator.SetBool(hashOfAnimationDictionary[AnimationReference.isRolling], false);
    }

    private bool OnSlope()
    {
        // playerHeight * 0.5f + 0.3f - это так и должно быть?
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, 0.1f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }
}
