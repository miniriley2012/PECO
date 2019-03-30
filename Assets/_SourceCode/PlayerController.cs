using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public enum Form { Test, Human, Bear };
    private Form currentForm = Form.Test;

    //playercontroller functionality 
    private GameObject controlledPawn;
    private Rigidbody pawnRigidbody;
    private CharacterController pawnController;
    public Camera pawnCamera;
    private RaycastHit hitObj;
    public LayerMask mask;
    private bool jump = false;

    //input variables 
    private const float baseSpeed = 12.0f;
    public float speed = baseSpeed;
    public float turnSpeed = 2f;
    public float jumpStrength = 5.2f;
    Vector3 moveDirection;
    Vector3 moveRotation;

    /**
     * Speed modifiers for now, this will be
     * generalized later on
     */
    private Dictionary<string, SpeedEffect> _effects = new Dictionary<string, SpeedEffect>();

    //sets the position of the camera behind each pawn once possessed
    private Vector3 CameraPosition = new Vector3(0.8f, 2.5f, -4.2f);

    void Start()
    {
        controlledPawn = GameObject.FindGameObjectsWithTag("PlayerControllable")[0];
        pawnController = controlledPawn.GetComponent<CharacterController>();

        //Sets the camera to be a child of  the controlled pawn
        pawnCamera.transform.SetParent(controlledPawn.transform);

        //force set the cameras position 
        pawnCamera.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        pawnCamera.transform.localPosition = CameraPosition;

        if (controlledPawn.GetComponent<Rigidbody>())
        {
            pawnRigidbody = controlledPawn.GetComponent<Rigidbody>();
        }
    }

    private void Update() {
        if(Input.GetButtonDown("Jump") && IsGrounded()) {
            jump = true;
        }
    }

    void FixedUpdate()
    {
        //grounded = Physics.OverlapBox(transform.position, GetComponent<CapsuleCollider>().radius / 2, )
        if (controlledPawn == null)
            return;
        if (pawnRigidbody != null) {
            RBMovement();
        } else {
            Movement();
        }

        RayTrace();
    }

    private void Movement() {
        // Retrieve inputs
        //moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        moveDirection.x = Input.GetAxis("Horizontal") * speed;
        moveDirection.z = Input.GetAxis("Vertical") * speed;
        moveRotation = new Vector3(0.0f, Input.GetAxis("CameraX"), 0.0f);
        moveDirection = controlledPawn.transform.TransformVector(moveDirection);
        
        // Apply gravity and jump
        if(jump) {
            moveDirection.y = 10f;
            jump = false;
        } else if(!IsGrounded()) {
            moveDirection.y += -1f;
        } else {
            moveDirection.y = 0;
        }
        moveDirection.y = Mathf.Clamp(moveDirection.y, -20f, 10f);

        // Apply movement
        pawnController.Move(moveDirection * Time.fixedDeltaTime);
        pawnController.transform.Rotate((moveRotation * turnSpeed), Space.Self);
    }

    private void MovementOld()
    {
        moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        moveRotation = new Vector3(0.0f, Input.GetAxis("CameraX"), 0.0f);

        //converts move direction to world space
        moveDirection = controlledPawn.transform.TransformVector(moveDirection);
        moveDirection = moveDirection * speed;

        pawnController.Move(moveDirection * Time.deltaTime);
        pawnController.transform.Rotate((moveRotation * turnSpeed), Space.Self);

        IsGrounded();

        Utils.WithKeyHold(KeyCode.LeftShift, OnSneakBegin, OnSneakEnd);
        ApplyEffects();
    }

    private void RBMovement()
    {
        moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        moveRotation = new Vector3(0.0f, Input.GetAxis("CameraX"), 0.0f);

        //converts move direction to world space
        moveDirection = controlledPawn.transform.TransformVector(moveDirection);
        moveDirection = moveDirection * speed;

        pawnRigidbody.MovePosition(controlledPawn.transform.position + moveDirection * Time.deltaTime);
        pawnRigidbody.MoveRotation(Quaternion.Euler(moveRotation * turnSpeed) * controlledPawn.transform.rotation);

        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            pawnRigidbody.AddRelativeForce(controlledPawn.transform.up * jumpStrength, ForceMode.Impulse);
        }

        Utils.WithKeyHold(KeyCode.LeftShift, OnSneakBegin, OnSneakEnd);
        ApplyEffects();
    }

    private bool IsGrounded() {
        //check if the player is more than 0.1f above the ground, if so, they cannot jump
        //return Physics.Raycast(controlledPawn.transform.position, -controlledPawn.transform.up, controlledPawn.GetComponent<Collider>().bounds.extents.y + 0.1f);
        //Debug.DrawRay(controlledPawn.transform.position - new Vector3(0, 0.8f), Vector3.down, Color.black);
        return jump ? false : Physics.BoxCast(controlledPawn.transform.position - new Vector3(0, 0.8f), new Vector3(0.25f, 0.02f, 0.25f), Vector3.down, Quaternion.identity, 0.01f, mask);
    }

    private void ChangeAlpha(float alpha)
    {
        var mat = controlledPawn.GetComponent<Renderer>().material;
        var oldColor = mat.color;
        var newColor = new Color(oldColor.r, oldColor.g, oldColor.b, alpha);
        mat.SetColor("_Color", newColor);
    }

    private void RayTrace()
    {
        var position = controlledPawn.transform.position;
        var forward = controlledPawn.transform.forward;

        Debug.DrawRay(position, forward * 10f, Color.red);

        //on LMB 
        if (!Input.GetMouseButtonDown(0)) return;
        //if hit object is tagged as controllable, possess it 
        if (!Physics.Raycast(position, forward, out hitObj, 100, 1)) return;

        if (hitObj.transform.gameObject.CompareTag("PlayerControllable"))
        {
            ChangeControlledPawn(hitObj.transform.gameObject);
        }
    }

    private void OnSneakBegin()
    {
        var sneak = new Sneak();
        _effects.Add("Sneak", sneak);
        ChangeAlpha(0.5f);
    }

    private void OnSneakEnd()
    {
        _effects.Remove("Sneak");
        ChangeAlpha(1f);
    }

    /**
     * Currently only works for speed
     */
    private void ApplyEffects()
    {
        var effects = _effects.Values;
        var newFlat = effects.Aggregate(baseSpeed, (accum, effect) => accum + effect.flat);
        speed = effects.Aggregate(newFlat, (accum, effect) => accum * effect.multiplier);
    }

    //sets controlled pawn to new pawn, resets camera on new pawn
    private void ChangeControlledPawn(GameObject newPawn)
    {
        var temp = controlledPawn;
        controlledPawn = newPawn;

        if (newPawn.GetComponent<Rigidbody>())
        {
            pawnRigidbody = newPawn.GetComponent<Rigidbody>();
        }
        else
        {
            pawnRigidbody = null;
            pawnController = controlledPawn.GetComponent<CharacterController>();
        }
        pawnCamera.transform.SetParent(controlledPawn.transform);
        pawnCamera.transform.localPosition = CameraPosition;
        pawnCamera.transform.rotation = controlledPawn.transform.rotation * Quaternion.Euler(15.0f, 0f, 0f);

        // retain current rotation
        controlledPawn.transform.rotation = temp.transform.rotation;


    }

}