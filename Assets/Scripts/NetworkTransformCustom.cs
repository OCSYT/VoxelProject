using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkTransformCustom : NetworkBehaviour
{
    NetworkVariable<Vector3> NetworkPosition =
        new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    NetworkVariable<Quaternion> NetworkRotation =
        new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    NetworkVariable<Vector3> NetworkScale =
        new NetworkVariable<Vector3>(Vector3.one, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Rigidbody rb;

    // Parameters for customization
    public float InterpolationSpeed = 5f;
    public float MaxSnapDistance = 5f;
    public bool UseGlobalRotation = false;  // Toggle between local/global rotation

    // Toggles for syncing position, rotation, and scale
    public bool SyncPosition = true;
    public bool SyncRotation = true;
    public bool SyncScale = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (IsOwner)
        {
            if (rb != null)
            {
                rb.isKinematic = false; // Allow movement on the owner side
            }
        }
        else
        {
            if (rb != null)
            {
                rb.isKinematic = true; // Prevent physics simulation on non-owner clients
            }
        }
    }

    void Update()
    {
        if (!IsOwner)
        {
            // Handle non-owner clients
            if (SyncPosition)
            {
                float distance = Vector3.Distance(transform.localPosition, NetworkPosition.Value);
                if (distance > MaxSnapDistance)
                {
                    transform.localPosition = NetworkPosition.Value; // Snap to position
                }
                else
                {
                    transform.localPosition = Vector3.Lerp(transform.localPosition, NetworkPosition.Value, InterpolationSpeed * Time.deltaTime);
                }
            }

            if (SyncRotation)
            {
                if (UseGlobalRotation)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, NetworkRotation.Value, InterpolationSpeed * Time.deltaTime);
                }
                else
                {
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, NetworkRotation.Value, InterpolationSpeed * Time.deltaTime);
                }
            }

            if (SyncScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, NetworkScale.Value, InterpolationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Owner's side: update networked variables with local values
            if (SyncPosition) NetworkPosition.Value = transform.localPosition;
            if (SyncRotation)
            {
                if (UseGlobalRotation)
                {
                    NetworkRotation.Value = transform.rotation;
                }
                else
                {
                    NetworkRotation.Value = transform.localRotation;
                }
            }
            if (SyncScale) NetworkScale.Value = transform.localScale;
        }
    }

    void LateUpdate()
    {
        if (!IsOwner && rb != null)
        {
            rb.isKinematic = true; // Ensure the rigidbody is kinematic for non-owners
        }
    }
}
