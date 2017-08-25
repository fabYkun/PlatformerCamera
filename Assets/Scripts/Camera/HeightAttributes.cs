using UnityEngine;

/// <summary>
/// Used to modify the view automaticly when having a hill or a cliff in front of the camera
/// </summary>
[CreateAssetMenu(menuName = "Camera/HeightAttributes")]
public class                    HeightAttributes : ScriptableObject
{
    [Tooltip("which layers collide with the raycast")]
    public LayerMask            cliffHillMaskDetection = 251;       // hit all layers but the manual ones and the IgnoreRaycast
    [Tooltip("x is for cliffs and y for hills")]
    public Vector2              cliffHillDistance = new Vector2(4, 170);
    [Tooltip("x is for cliffs and y for hills")]
    public Vector2              cliffHillHeight = new Vector2(12, 10);
    [Tooltip("How far we cast the ray to ensure that this is a big cliff (x) or a hill (y)")]
    public Vector2              maxCliffHillCastDistance = new Vector2(20, 100);
    [Tooltip("How far away is the cliff (x) or the hill (y) ray cast from the player")]
    public Vector2              cliffHillCastAwayDistance = new Vector2(5, 8);
}