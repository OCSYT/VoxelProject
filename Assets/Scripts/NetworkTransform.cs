using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class NetworkTransform : NetworkBehaviour
{
    NetworkVariable<Vector3> NetworkPosition = 
        new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    NetworkVariable<Quaternion> NetworkRotation =
    new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private Rigidbody rb;
    private float interpolationSpeed = 5;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (IsOwner)
        {

        }
        else
        {
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, NetworkPosition.Value, 5 * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, NetworkRotation.Value, 5 * Time.deltaTime);
        }
        else
        {
            NetworkPosition.Value = transform.localPosition;
            NetworkRotation.Value = transform.localRotation;
        }
    }
    private void LateUpdate()
    {
        if (!IsOwner)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }
}
