using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterController))]
public class                        Movement : MonoBehaviour
{
    [SerializeField]
    private Camera                  _camera;
    private PlatformerCamera        _cameraCtrl;
    private CharacterController     _controller;
    private Rigidbody               _rigidbody;

    private Vector3                 _cameraDirection = Vector3.zero;
    private Vector2                 _padxyDirection = Vector2.zero;
    private Vector3                 _xyDirection = Vector3.zero;

    [SerializeField]
    private float                   _standardLagCoeff = 0.2f;       // added to the input so it slows artificially the input

    private float                   _xAxis;
    private float                   _yAxis;
    private float                   _speed;
    private float                   _vSpeed = 0;                    // vertical speed (gravity & jumps)

    [SerializeField, Tooltip("gameobject tracked by the camera, when player's velocity is at its peak")]
    private Transform               _motionIndicator;
    [SerializeField, Tooltip("motion indicator's distance relative to the velocity input")]
    private AnimationCurve          _motionIndicatorDistance;
    private Vector3                 _maxMIVector;
    private Vector3                 _velocityMotionIndicator = Vector3.zero;

    [SerializeField]
    private float                   _speedMultiplier = 12;
    [SerializeField]
    private float                   _gravity = 9.8f;

    private Vector3                 _moveDirectionBeforejump;
    //hugo hack 22/04
    //permet de stopper le mouvement du perso au contact d;une sticky platform
    private int mouvDirNullifier = 1;

    [SerializeField, Tooltip("decides how much momentum is keeped when jumping")]
    private float                   _airInertia = 1;
    [SerializeField, Tooltip("decreases inertia inertia *= aeroDynamic")]
    private float                   _aerodynamic = 0.99f;
    [SerializeField, Tooltip("decreases inertia on double jump inertia *= airInertiaDecreaser")]
    private float                   _airInertiaDecreaser = 0.95f;
    [SerializeField]
    private float                   _airControl = 1;
    [SerializeField]
    private float                   _airControlDoubleJump = 1;
    [SerializeField]
    private float                   _groundJumpForce = 5;
    [SerializeField]
    private float                   _airJumpForce = 3;
    [SerializeField]
    private AnimationCurve          _jumpExpandTimeMultiplier;
    [SerializeField, Tooltip("window in wich the player can expand his jump while in the air")]
    private float                   _jumpMaxExpandTime = (1 / 60) * 20;     // in seconds, default to 30frames?
    [SerializeField]
    private AnimationCurve          _jumpForceMultiplier;
    [SerializeField, Tooltip("time max before leaving the ground")]
    private float                   _jumpMaxTime = (1 / 60) * 10;           // in seconds, default to 10frames?
    private float                   _jumpTimeTap;                           // time at which the jump button was pressed
    private bool                    _didJump = false;
    private bool                    _didDoubleJump = false;

    [SerializeField, Tooltip("delay in seconds for the player to actually be considered 'in the air' when leaving the ground")]
    private float                   _groundedDelay = 0.1f;
    private float                   _notGroundedTime;                       // when did the player quit the ground
    private bool                    _oldGrounded;
    public bool                     isGrounded
    {
        get {
            if (this._didJump) return false;
            return !(!this._controller.isGrounded && (Time.time - this._notGroundedTime) > this._groundedDelay);
        }
    }

    public delegate void            FreezeDelegate();
    public event FreezeDelegate     FreezeEvent;

    void                            Start()
    {
        this._controller = GetComponent<CharacterController>();
        this._cameraCtrl = this._camera.GetComponent<PlatformerCamera>();
        this._rigidbody = GetComponent<Rigidbody>();
        this._maxMIVector = this.transform.position - this._motionIndicator.position;
        this._maxMIVector.y = this._motionIndicator.localPosition.y;
        this._oldGrounded = this._controller.isGrounded;
    }

    void                            controllerInput()
    {
        this._padxyDirection.x = Input.GetAxis("Horizontal");
        this._padxyDirection.y = Input.GetAxis("Vertical");

        this._xAxis = this._xAxis * (1 - this._standardLagCoeff) + this._padxyDirection.x * this._standardLagCoeff;
        this._yAxis = this._yAxis * (1 - this._standardLagCoeff) + this._padxyDirection.y * this._standardLagCoeff;
        this._xAxis = Mathf.Clamp(this._xAxis, -1f, 1f);
        this._yAxis = Mathf.Clamp(this._yAxis, -1f, 1f);

        this._xyDirection = new Vector3(this._xAxis, 0, this._yAxis);
        this._cameraDirection = this._cameraCtrl.getCameraDirection();
        this._speed = Mathf.Min(this._xyDirection.sqrMagnitude, 1.0f);
        this._speed = (this._speed < 0.1f) ? 0 : this._speed;
    }

    void                            Move()
    {
        Quaternion                  relativeToCamera = Quaternion.FromToRotation(Vector3.forward, this._cameraDirection);
        Vector3                     moveDirection = relativeToCamera * this._xyDirection;

        moveDirection.Normalize();
        moveDirection *= this._speed * this._speedMultiplier;
        this._cameraCtrl.allowedToFirstPerson = true;
        if (this._xyDirection.sqrMagnitude > 0.1f)
        {
            this.transform.LookAt(this.transform.position + (moveDirection * Time.deltaTime));
            this._cameraCtrl.allowedToFirstPerson = false;
        }
        if (this.isGrounded)
        {
            this._vSpeed = 0;
            this._didDoubleJump = this._didJump = false;
            this._moveDirectionBeforejump = moveDirection;
            if (!this._didJump && (Input.GetButtonUp("Fire1") || (Input.GetButton("Fire1") && Time.time - this._jumpTimeTap > this._jumpMaxTime)))
            {
                this._vSpeed = this._groundJumpForce * this._jumpForceMultiplier.Evaluate(Mathf.Clamp01((Time.time - this._jumpTimeTap) / this._jumpMaxTime));
                this._didJump = true;
            }
        }
        else
        {
            this._cameraCtrl.allowedToFirstPerson = false; // we don't want to be in 1st person view while in the air
            this._moveDirectionBeforejump *= this._aerodynamic;
            if (!this._didDoubleJump)
            {
                moveDirection *= this._airControl;
                if (Input.GetButtonDown("Fire1"))
                {
                    this._vSpeed = this._airJumpForce;
                    this._didDoubleJump = true;
                }
                else if (Input.GetButton("Fire1") && Time.time - (this._jumpTimeTap + this._jumpMaxTime) < this._jumpMaxExpandTime)
                {
                    this._vSpeed += _jumpExpandTimeMultiplier.Evaluate(Mathf.Clamp01((Time.time - this._jumpTimeTap + this._jumpMaxTime) / this._jumpMaxExpandTime)) * this._gravity * Time.deltaTime;
                }
            }
            else
            {
                this._moveDirectionBeforejump *= this._airInertiaDecreaser;
                moveDirection *= this._airControlDoubleJump;
            }
        }

        this._vSpeed -= this._gravity * Time.deltaTime;
        moveDirection.y = this._vSpeed;
        if (!this.isGrounded) moveDirection += this._moveDirectionBeforejump * this._airInertia;
           // injected mouvDirNullifier there, god, it is dirty
        this._controller.Move(moveDirection * mouvDirNullifier * Time.deltaTime);
        //  we only want mouvDirNullifier to equal 0 once to stop the mouv
        //print(moveDirection.y);
        mouvDirNullifier = 1;
    }

    void                            Freeze()
    {
        if (this.FreezeEvent != null) this.FreezeEvent();
        this._rigidbody.isKinematic = !this._rigidbody.isKinematic;
        this._controller.enabled = !this._controller.enabled;
    }

    private void                    MotionIndicator()
    {
        
        if (Physics.Linecast(this.transform.position, this._motionIndicator.position, 251)) // hit all layers but the custom ones and the IgnoreRaycast
            this._motionIndicator.localPosition = Vector3.SmoothDamp(this._motionIndicator.localPosition, Vector3.zero, ref this._velocityMotionIndicator, 0.2f);
        else
            this._motionIndicator.localPosition = Vector3.SmoothDamp(this._motionIndicator.localPosition, this._maxMIVector * this._speed, ref this._velocityMotionIndicator, 4f);
    }
	
	void                            Update ()
    {
        if (Input.GetButtonDown("Fire1")) this._jumpTimeTap = Time.time;
        this.controllerInput();
        this.Move();
        this.MotionIndicator();
        if (Input.GetButtonDown("Fire2")) this.Freeze();
    }

    void                        LateUpdate()
    {
        if (this._controller.isGrounded && this._didJump) this._didJump = false;
    }

    void                        FixedUpdate()
    {
        if (!this._controller.isGrounded && this._oldGrounded)
            this._notGroundedTime = Time.time;
        this._oldGrounded = this._controller.isGrounded;
    }

    //hugo 22/04/17 
    //setters
    public float Gravity { get { return _gravity; } set { _gravity = value; }}
    public bool DidJump { set { _didJump = value; }}
    public bool DidDoubleJump { set { _didDoubleJump = value; }}
    public int MouvDirNullifier { set { mouvDirNullifier = value; }  }
    public Vector3 MoveDirectionBeforejump { set { _moveDirectionBeforejump = value; } }
    public float VSpeed { set { _vSpeed = value; } }


}
