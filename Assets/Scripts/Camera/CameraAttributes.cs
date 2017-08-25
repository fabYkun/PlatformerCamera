using UnityEngine;

[CreateAssetMenu(menuName = "Camera/CameraAttributes")]
public class                    CameraAttributes : ScriptableObject
{
    public float                distance = 5;
    public float                distanceUp = 2.25f;
    public float                manualMinDistance = 4;
    public float                manualMinDistanceUp = 0;
    public float                manualMaxDistance = 12;
    public float                manualMaxDistanceUp = 10;

    [SerializeField, Tooltip("interpolation between automatic and manual mode, 0 is manual state and 1 is automatic state")]
    public AnimationCurve cameraModeInterpolation = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField, Tooltip("time it takes to regain an automatic camera")]
    public float                autoSwitchTime = 5;
    [SerializeField, Tooltip("delay in which the camera stays in manual mode")]
    public float                hybridDelayTime = 2;
}