using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Needs a trigger to be functional
/// Please configure the unity collision layer table to make sure it only triggers the player(or other layers)
/// 
/// Used to put points of interests on the map which will always appear to the camera on automatic mode
/// Can also modify the camera's properties by changing its "camera attributes" or "height attributes"
/// </summary>
public class                    CameraModifier : MonoBehaviour
{
    private PlatformerCamera    cam;

    [SerializeField]
    private Transform           pointOfInterest;
    [SerializeField]
    private float               timeToChangePointOfInterest = 5;
    [SerializeField]
    private AnimationCurve      changingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField]
    private CameraAttributes    cameraAttrs;
    [SerializeField]
    private HeightAttributes    heightAttrs;

    private float               triggeredTime;


    private Vector3             position;
    private Vector3             velocity = Vector3.zero;
    private bool                hadAlreadyAPointOfInterest;

    void                        Start()
    {
        this.cam = Camera.main.GetComponent<PlatformerCamera>();
    }
	
    void                        OnTriggerEnter(Collider other)
    {
        if (this.pointOfInterest)
        { 
            Vector3                 currentPointOfInterestPosition = this.cam.getCurrentPointOfInterest();

            this.triggeredTime = Time.time;
            this.position = new Vector3(this.pointOfInterest.position.x, this.pointOfInterest.position.y, this.pointOfInterest.position.z);
            this.pointOfInterest.position = Vector3.Distance(this.cam.getCurrentPointOfInterest(), this.position) < Vector3.Distance(this.cam.getNormalPointOfInterest(), this.position) ?
                                                            this.cam.getCurrentPointOfInterest() : this.cam.getNormalPointOfInterest();
            this.hadAlreadyAPointOfInterest = (this.cam.getPointOfInterest() != null);
            this.cam.replacePointOfInterest(this.pointOfInterest);
        }
        if (this.cameraAttrs != null)
            this.cam.setCameraAttributes(this.cameraAttrs);
        if (this.heightAttrs != null)
            this.cam.setHeightAttributes(this.heightAttrs);
    }

    void                        OnTriggerStay(Collider other)
    {
        if (!this.pointOfInterest) return;
        if (this.hadAlreadyAPointOfInterest)
            this.pointOfInterest.position = Vector3.SmoothDamp(this.pointOfInterest.position, this.position, ref this.velocity, this.timeToChangePointOfInterest);
        else
            this.pointOfInterest.position = Vector3.Lerp(this.cam.getNormalPointOfInterest(), this.position, this.changingCurve.Evaluate((Time.time - this.triggeredTime) / this.timeToChangePointOfInterest));
    }

    void                        OnTriggerExit(Collider other)
    {
        if (this.pointOfInterest)
        {
            if (this.cam.getPointOfInterest() == this.pointOfInterest)
                this.cam.replacePointOfInterest(null);
            this.pointOfInterest.position = this.position;
            this.velocity = Vector3.zero;
        }
        if (this.cameraAttrs != null)
            this.cam.resetCameraAttributes();
        if (this.heightAttrs != null)
            this.cam.resetHeightAttributes();
    }
}
