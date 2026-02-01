using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExpSalt : MonoBehaviour
{
    Vector3 InitialPos;
    float waterLevel;
    bool automation_flag;
    public Animator animation;
    Rigidbody rb;
    OVRGrabbable og;

    // Start is called before the first frame update
    void Start()
    {
        InitialPos = transform.position;
        automation_flag = false;
        rb = GetComponent<Rigidbody>();
        og = GetComponent<OVRGrabbable>();
    }

    // Update is called once per frame
    void Update()
    {
        //safeguard mechanism, if falls through the floor, returns to its initial position
        if (transform.position.y <= 0)
        {
            transform.position = InitialPos;
        }
        if (automation_flag)
        {
            // is currently automated
            animation.enabled = true;

            // disable the grabbing
            rb.isKinematic = true;
        }
        if (!automation_flag)
        {
            animation.enabled = false;
        }
    }

    public void Overtake()
    {
        automation_flag = true;
    }

    public void Takeback()
    {
        automation_flag = false;
    }

    public bool Automated()
    {
        return automation_flag;
    }
}
