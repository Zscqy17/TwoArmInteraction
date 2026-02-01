using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArmComponent : MonoBehaviour
{
    public Transform elbowPos, handPos;
    public Transform handPosOrigin, headPosOrigin;
    Vector3 shoulderPos;
    void Start()
    {
        //shoulderPos = headPosOrigin.position + 0.1f * headPosOrigin.right - 0.1f * headPosOrigin.up - 0.1f * headPosOrigin.forward;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
