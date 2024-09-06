using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cape : MonoBehaviour
{
    public Transform Player;    // Reference to the player's transform
    public float RotationDamping = 2f;  // Damping factor for smoother rotation

    private Vector3 LastPlayerPosition; // Tracks the player's position from the previous frame
    private Vector3 PlayerVelocity;     // Stores the player's movement direction and speed

    private float PitchAngle = 45;     // How much the cape tilts up/down based on movement (in degrees)
    private float YawAngle = 35;       // How much the cape rotates left/right based on movement (in degrees)

    void Start()
    {
        // Initialize the player's last position to the current position
        LastPlayerPosition = Player.position;
    }

    void Update()
    {
        CalculatePlayerVelocity();
        ApplyCapeRotation();
    }

    void CalculatePlayerVelocity()
    {
        // Calculate how much the player has moved since the last frame
        PlayerVelocity = (Player.position - LastPlayerPosition) / Time.deltaTime;

        // Update the last position to the current position
        LastPlayerPosition = Player.position;
    }

    void ApplyCapeRotation()
    {
        // Calculate angles based on player movement direction and speed
        float forwardMovement = Vector3.Dot(PlayerVelocity, Player.forward) + Vector3.Dot(PlayerVelocity, -Player.up);
        float sidewaysMovement = Vector3.Dot(PlayerVelocity, Player.right);

        // Determine the pitch (up/down tilt) based on forward/backward movement
        float pitch = -Mathf.Clamp(forwardMovement * PitchAngle, 0, PitchAngle);

        // Determine the yaw (left/right swing) based on sideways movement
        float yaw = Mathf.Clamp(sidewaysMovement * YawAngle, -YawAngle, YawAngle);

        // Create the target rotation based on the calculated pitch and yaw
        Quaternion targetRotation = Quaternion.Euler(yaw, 0, -pitch);

        // Smoothly rotate the cape towards the target rotation
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation * Quaternion.Euler(0, 90 + 180, 0), Time.deltaTime * RotationDamping);
    }
}
