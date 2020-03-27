using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    
    public float smoothSpeed = 2.25f;
    public Vector3 offset;

    void LateUpdate()
    {
        Vector3 nextPos = target.position + offset;
        Vector3 smoothPos = Vector3.Lerp(transform.position, nextPos, Time.deltaTime * smoothSpeed);
        transform.position = smoothPos;
    }
}
