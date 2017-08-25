using UnityEngine;
using System.Collections;

[System.Serializable]
public class                    HeightModifier
{
    public float                BumpHeight = 0;
    public float                BumpDistance = 0;
    private float               bumpHeightSmooth = 0;
    private float               bumpDistanceSmooth = 0;
    public float                smoothDampTime = 0.4f;              // time it takes to change camera location
    public RaycastHit           raycastHit;                         // public for debug purposes

    
    [Range(-1, 1)]
    public float                cliffHillRatio = 0;                 // -1 = cliff | 0 = flat | 1 = hill

    public void                 Calculate(HeightAttributes attrs, Vector3 origin, Vector3 castHillCliffDirection, Vector3 cameraDirection)
    {
        Vector3                 cliffCastPosition;
        Vector3                 hillCastPosition;
        float                   minDistance;
        float                   maxDistance;

        // we offset the y value of the raycast position by the length of the raycast
        hillCastPosition = origin + cameraDirection * attrs.cliffHillCastAwayDistance.y + (castHillCliffDirection * -1) * attrs.maxCliffHillCastDistance.y;
        minDistance = Vector3.Distance(origin, hillCastPosition + castHillCliffDirection * attrs.maxCliffHillCastDistance.y);
        maxDistance = Vector3.Distance(origin, hillCastPosition);
        if (Physics.Raycast(hillCastPosition, castHillCliffDirection, out this.raycastHit, attrs.maxCliffHillCastDistance.y, attrs.cliffHillMaskDetection))
        {
            // there's a ground
            this.cliffHillRatio = (Vector3.Distance(origin, this.raycastHit.point) - minDistance) / maxDistance;
            this.BumpHeight = Mathf.SmoothDamp(this.BumpHeight, attrs.cliffHillHeight.y, ref this.bumpHeightSmooth, this.smoothDampTime);
            this.BumpDistance = Mathf.SmoothDamp(this.BumpDistance, attrs.cliffHillDistance.y, ref this.bumpDistanceSmooth, this.smoothDampTime);
        }
        else
        {
            // there's a cliff
            cliffCastPosition = origin + cameraDirection * attrs.cliffHillCastAwayDistance.x;
            minDistance = Vector3.Distance(origin, cliffCastPosition);
            maxDistance = Vector3.Distance(origin, cliffCastPosition + castHillCliffDirection * attrs.maxCliffHillCastDistance.x);
            this.cliffHillRatio = 1;
            if (Physics.Raycast(cliffCastPosition, castHillCliffDirection, out this.raycastHit, attrs.maxCliffHillCastDistance.x, attrs.cliffHillMaskDetection))
                this.cliffHillRatio = 1 * (Vector3.Distance(origin, this.raycastHit.point) - minDistance) / maxDistance;
            this.BumpHeight = Mathf.SmoothDamp(this.BumpHeight, attrs.cliffHillHeight.x, ref this.bumpHeightSmooth, this.smoothDampTime);
            this.BumpDistance = Mathf.SmoothDamp(this.BumpDistance, attrs.cliffHillDistance.x, ref this.bumpDistanceSmooth, this.smoothDampTime);
        }
    }
}

public class                    PlatformerCamera : MonoBehaviour
{
    private float               modeRatio = 1;
    private float               lastTimeManual = 0;

    private Transform           pointOfInterest = null; // the camera will attempt to show it if it is in hybrid or automatic mode

    private float               manualDistanceUp;
    private float               manualDistance;

    [SerializeField, Range(-1,1)]
    private float               pitchSlider = 0;
    [SerializeField, Range(0, 1)]
    private float               yawSlider = 0;

    [SerializeField, Tooltip("Pushes the camera away from colliding geometry")]
    private float               cameraCollisionDistanceMultiplier = 0.35f; // 0.35 seems to be a good value to avoid ~90% of clipping geometry, depends on the radius of the character thought

    [SerializeField]
    private float               minJoystickThreshold = 0.1f;
    private Vector3             manualForward;
    private Vector3             manualUp;
    private Quaternion          oldRotation;

    [SerializeField]
    private CameraAttributes    defaultAttrs;
    private CameraAttributes    attrs;

    [SerializeField]
    private HeightAttributes    defaultHeightAttributes;
    private HeightAttributes    currentHeightAttributes;

    [SerializeField]
    private HeightModifier      heightModifier;

    [SerializeField]
    private Transform           follow;
    [SerializeField, Tooltip("head position used in first person mode")]
    private Transform           head;
    private Vector3             lookAt;

    private int                 layerMask = 251;            // hit all layers but the manual ones and the IgnoreRaycast

    public enum                 CameraState : byte
    {
        Automatic,              // automatic
        Manual,                 // the user is involved
        Hybrid,                 // hybrid between Nintendo and ManualState
        FirstPerson             // when the camera is too close to the subject
    }
    private CameraState         state;
    public bool                 allowedToFirstPerson = true;
    private bool                isFirstPersonned = false;

    private Vector3             targetPosition;
    private Vector3             lookDirection;
    private Vector3             oldFollow;
    private Vector3             oldPosition;
    
    private Vector3             velocityCamSmooth = Vector3.zero;
    private Vector3             manualCamSmooth = Vector3.zero;
    private Vector3             velocityTargetSmooth = Vector3.zero;
    private Vector3             hybridTargetSmooth = Vector3.zero;
    [SerializeField]
    private float               smoothDampTime = 0.4f;          // time it takes to change camera location
    [SerializeField]
    private float               smoothDampTargetTime = 0.2f;    // time it takes to change camera target location
    [SerializeField]
    private float               smoothDampManualTime = 0.1f;
    public bool                 debug = true;

    private float               _xAxis = 0;
    private float               _yAxis = 0;

    void                        OnDrawGizmos()
    {
        if (!this.debug) return;
        Gizmos.color = Color.yellow;
        if (this.heightModifier.raycastHit.collider != null)
            Gizmos.DrawSphere(this.heightModifier.raycastHit.point, 1);
    }

    private IEnumerator         changeSmoothDampTargetTime(float newValue)
    {
        float                   oldValue = this.smoothDampTargetTime;
        float                   time = 0;

        while (time < 1)
        {
            time += Time.deltaTime * 5;
            this.smoothDampTargetTime = Mathf.Lerp(oldValue, newValue, time);
            yield return null;
        }
        this.smoothDampTargetTime = newValue;
    }

    void                        Start()
    {
	    this.state = CameraState.Automatic;
        this.oldFollow = this.follow.position;
        this.oldRotation = this.transform.rotation;
   	    this.transform.position = this.follow.position + Vector3.one; // set position
        this.currentHeightAttributes = this.defaultHeightAttributes;
        this.attrs = this.defaultAttrs;
    }

    bool                        userInput()
    {
        return (Mathf.Abs(Input.GetAxis("ViewHorizontal")) >= this.minJoystickThreshold || Mathf.Abs(Input.GetAxis("ViewVertical")) >= this.minJoystickThreshold);
    }

    void                        ManualStateUpdate()
    {
        float                   xAxis = this._xAxis / 2 + Input.GetAxis("ViewHorizontal") / 2;
        float                   yAxis = this._yAxis / 2 + Input.GetAxis("ViewVertical") / 2;

        if (Mathf.Abs(xAxis) > this.minJoystickThreshold)
            this.yawSlider = (this.yawSlider + xAxis * Mathf.Abs(xAxis) / 90) % 1;
        if (Mathf.Abs(yAxis) > this.minJoystickThreshold)
            this.pitchSlider = Mathf.Clamp((this.pitchSlider + yAxis * Mathf.Abs(yAxis) / 20), -1, 1);
        if (this.yawSlider < 0) this.yawSlider += 1;

        this.manualDistance = this.pitchSlider < 0 ? Mathf.Lerp(this.attrs.distance, this.attrs.manualMaxDistance, this.pitchSlider + 1) : Mathf.Lerp(this.attrs.distance, this.attrs.manualMinDistance, this.pitchSlider);
        this.manualDistanceUp = this.pitchSlider < 0 ? Mathf.Lerp(this.attrs.manualMaxDistanceUp, this.attrs.distanceUp, this.pitchSlider + 1) : Mathf.Lerp(this.attrs.distanceUp, this.attrs.manualMinDistanceUp, this.pitchSlider);
        this._xAxis = xAxis;
        this._yAxis = yAxis;
    }

    private IEnumerator         goToFirstPerson()
    {
        float                   time = 0;
        Vector3                 oldPosition;
        Quaternion              oldRotation;

        while (this.pitchSlider == 1 && this._yAxis > 0.9 && time < 0.2f)
        {
            time += Time.deltaTime;
            yield return null;
        }
        if (this.pitchSlider == 1 && this._yAxis > 0.9)
        {
            this.setState(CameraState.FirstPerson);
            oldPosition = this.transform.position;
            oldRotation = this.transform.rotation;
            time = 0;
            while (this.state == CameraState.FirstPerson && this.transform.position != this.targetPosition && time < 1)
            {
                time += Time.deltaTime * 2;
                this.transform.position = Vector3.Lerp(oldPosition, this.head.position, time * time);
                this.transform.rotation = Quaternion.Slerp(oldRotation, this.head.rotation, time * time);
                yield return null;
            }
            this.pitchSlider = 0;
            this.yawSlider = this.follow.rotation.eulerAngles.y / 360.0f;
            this.isFirstPersonned = true;
        }
    }

    void                        Update()
    {
        CameraState             oldState = this.state;

        if (Input.GetButtonDown("CameraReset")) this.setState(CameraState.Automatic);

        if (this.allowedToFirstPerson && this.pitchSlider >= (1.0f - float.Epsilon) && this._yAxis > 0.9)
            StartCoroutine(goToFirstPerson());
        else if ((userInput() && this.state != CameraState.FirstPerson) || (!this.allowedToFirstPerson && this.state == CameraState.FirstPerson))
        {
            this.isFirstPersonned = false;
            this.setState(CameraState.Manual);
            if (oldState != CameraState.Manual && oldState != CameraState.Hybrid)
            {
                this.manualForward = new Vector3(this.transform.forward.x, 0, this.transform.forward.z).normalized;
                this.manualUp = this.follow.up;
            }
            this.lastTimeManual = Time.time;
        }

        if (this.state == CameraState.Manual || this.state == CameraState.FirstPerson || this.state == CameraState.Hybrid)
            this.ManualStateUpdate();
        else if (this.state != CameraState.Hybrid)
        {
            this.yawSlider = 0;
            this._xAxis = this._yAxis = 0;
        }

        this.modeRatio = Mathf.Clamp01((Time.time - (this.lastTimeManual + this.attrs.hybridDelayTime)) / this.attrs.autoSwitchTime);
    }

    Vector3                     getAutomaticTargetPosition()
    {
        if (this.pointOfInterest) this.lookDirection = (this.pointOfInterest.position - this.transform.position).normalized;
        return (this.follow.position + this.follow.up * (Mathf.Lerp(this.attrs.distanceUp, this.heightModifier.BumpHeight, this.heightModifier.cliffHillRatio)) - this.lookDirection * (Mathf.Lerp(this.attrs.distance, this.heightModifier.BumpDistance, this.heightModifier.cliffHillRatio)));
    }

    Vector3                     getManualTargetPosition()
    {
        return (this.follow.position + this.manualUp * this.manualDistanceUp - this.manualForward * this.manualDistance);
    }

    Vector3                     getLookDirection()
    {
        Vector3                 lookdirection = this.follow.position - this.transform.position;

        lookdirection.y = 0;
        return (lookdirection.normalized);
    }

    void                        LateUpdateAutomaticState()
    {
        this.lookDirection = getLookDirection();
        this.targetPosition = getAutomaticTargetPosition();
        this.cameraCollision();
        this.smoothFollow();
    }

    void                        LateUpdateManualState()
    {
        this.targetPosition = getManualTargetPosition();
        this.smoothFollow();
        if (this.lastTimeManual + this.attrs.hybridDelayTime < Time.time)
            this.state = CameraState.Hybrid;
    }

    void                        LateUpdateHybridState()
    {
        float                   normalDistance = this.attrs.distance;
        float                   normalDistanceUp = this.attrs.distanceUp;

        this.lookDirection = getLookDirection();
        this.attrs.distance = Mathf.Lerp(this.manualDistance, normalDistance, this.attrs.cameraModeInterpolation.Evaluate(this.modeRatio));
        this.attrs.distanceUp = Mathf.Lerp(this.manualDistanceUp, normalDistanceUp, this.attrs.cameraModeInterpolation.Evaluate(this.modeRatio));
        this.targetPosition = Vector3.Lerp(this.getManualTargetPosition(), this.getAutomaticTargetPosition(), this.attrs.cameraModeInterpolation.Evaluate(this.modeRatio));
        
        this.cameraCollision();
        this.smoothFollow();

        this.attrs.distance = normalDistance;
        this.attrs.distanceUp = normalDistanceUp;
        if (this.modeRatio >= 1) this.state = CameraState.Automatic;
    }
    
	void                        LateUpdate()
    {
        this.lookAt = this.follow.position;
        switch (this.state)
        {
            case CameraState.Automatic:
                this.LateUpdateAutomaticState();
                break;
            case CameraState.Manual:
                this.LateUpdateManualState();
                break;
            case CameraState.FirstPerson:
                this.targetPosition = this.follow.position;
                if (this.isFirstPersonned) this.firstPerson();
                break;
            case CameraState.Hybrid:
                this.LateUpdateHybridState();
                break;
            default:
                break;
        }
        this.heightModifier.Calculate(this.currentHeightAttributes, this.follow.position, -this.follow.up, this.getCameraDirection());
    }

    private void                firstPerson()
    {
        this.transform.rotation = this.oldRotation;
        this.oldRotation = this.transform.rotation;
        this.transform.RotateAround(this.transform.position, this.follow.up, Mathf.Lerp(0, 360, this.yawSlider));
        this.transform.RotateAround(this.transform.position, this.transform.right, this.pitchSlider < 0 ? Mathf.Lerp(-70, 0, Mathf.Clamp01(this.pitchSlider + 1)) : Mathf.Lerp(0, 70, Mathf.Clamp01(this.pitchSlider)));
    }

    private void                smoothFollow()
    {
        this.oldPosition = this.transform.position;
        this.transform.rotation = this.oldRotation;
        if (this.state == CameraState.Manual) this.manualRotation();
        else if (this.state == CameraState.Hybrid) this.hybridRotation();
        else
            this.transform.position = Vector3.SmoothDamp(this.transform.position, this.targetPosition, ref this.velocityCamSmooth, this.smoothDampTime);
        this.oldFollow = Vector3.SmoothDamp(this.oldFollow, this.lookAt, ref this.velocityTargetSmooth, this.smoothDampTargetTime);
        this.transform.LookAt(this.oldFollow);
    }

    private void                manualRotation()
    {
        this.transform.position = this.targetPosition;
        this.oldRotation = this.transform.rotation;

        this.transform.RotateAround(this.lookAt, Vector3.up, Mathf.Lerp(0, 360, this.yawSlider));
        this.cameraCustomCollision();
        this.transform.position = Vector3.SmoothDamp(this.oldPosition, this.transform.position, ref this.manualCamSmooth, this.smoothDampManualTime);
    }

    private void                hybridRotation()
    {
        Vector3 auto = Vector3.SmoothDamp(this.transform.position, this.targetPosition, ref this.velocityCamSmooth, this.smoothDampTime);

        this.transform.position = this.follow.position + this.manualUp * this.manualDistanceUp - this.manualForward * this.manualDistance;
        this.oldRotation = this.transform.rotation;

        this.transform.RotateAround(this.lookAt, Vector3.up, Mathf.Lerp(0, 360, this.yawSlider));
        this.cameraCustomCollision();
        this.transform.position = Vector3.SmoothDamp(this.oldPosition, this.transform.position, ref this.manualCamSmooth, this.smoothDampManualTime);

        this.transform.position = Vector3.Lerp(this.transform.position, auto, this.attrs.cameraModeInterpolation.Evaluate(this.modeRatio));
    }

    private void                cameraCollision()
    {
        RaycastHit              obstacle = new RaycastHit();

        if (Physics.Linecast(this.oldFollow, this.targetPosition, out obstacle, this.layerMask))
            this.targetPosition = new Vector3(obstacle.point.x, obstacle.point.y, obstacle.point.z) + obstacle.normal.normalized * cameraCollisionDistanceMultiplier;
    }

    private void                cameraCustomCollision()
    {
        RaycastHit              obstacle = new RaycastHit();

        if (Physics.Linecast(this.oldFollow, this.transform.position, out obstacle, this.layerMask))
            this.transform.position = new Vector3(obstacle.point.x, obstacle.point.y, obstacle.point.z) + obstacle.normal.normalized * cameraCollisionDistanceMultiplier;
    }

    public void                 setState(CameraState newState)
    {
        this.state = newState;
    }
    
    public void                 setSmoothDampTargetTime(float newSmoothDampTargetTime)
    {
        StopCoroutine("changeSmoothDampTargetTime");
        StartCoroutine(changeSmoothDampTargetTime(newSmoothDampTargetTime));
    }

    public Vector3              getCameraDirection()
    {
        return (new Vector3(this.transform.forward.x, 0, this.transform.forward.z).normalized);
    }
    
    public void                 replacePointOfInterest(Transform newPointOfInterest)
    {
        this.pointOfInterest = newPointOfInterest;
    }

    public Vector3              getCurrentPointOfInterest()
    {
        return (this.pointOfInterest ? this.pointOfInterest.position : this.getNormalPointOfInterest());
    }

    public Vector3              getNormalPointOfInterest()
    {
        return (this.transform.position + this.getLookDirection());
    }

    public Transform            getPointOfInterest()
    {
        return (this.pointOfInterest);
    }

    public void                 setCameraAttributes(CameraAttributes newCamAttrs)
    {
        this.attrs = newCamAttrs;
    }

    public void                 resetCameraAttributes()
    {
        this.attrs = this.defaultAttrs;
    }

    public void                 setHeightAttributes(HeightAttributes newHeightAttrs)
    {
        this.currentHeightAttributes = newHeightAttrs;
    }

    public void                 resetHeightAttributes()
    {
        this.currentHeightAttributes = this.defaultHeightAttributes;
    }
}
