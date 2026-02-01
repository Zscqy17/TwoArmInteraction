using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExpMug : MonoBehaviour
{
    [SerializeField] GameObject waterLine;
    float waterLevel;
    bool automation_flag;
    public Animator animation;
    Rigidbody rb;
    OVRGrabbable og;
    Vector3 InitialPos;

    // Start is called before the first frame update
    void Start()
    {
        waterLevel = 0.0f;
        automation_flag = false;
        rb = GetComponent<Rigidbody>();
        og = GetComponent<OVRGrabbable>();
        InitialPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        waterLine.transform.localPosition += Vector3.up * (waterLevel * 0.09f - 0.047f - waterLine.transform.localPosition.y);
        if (automation_flag)
        {
            // is currently automated
            animation.enabled = true;

            // disable the grabbing
            rb.isKinematic = true;
            //og.grabbedBy.ForceRelease(og);
            //og.enabled = false;
            //animation.Play();
        }
        if (!automation_flag)
        {
            //rb.isKinematic = false;
            // is not automated, disable animation
            //og.enabled = true;
            animation.enabled = false;
        }

        //safeguard mechanism, if falls through the floor, returns to its initial position
        if (transform.position.y <= 0)
        {
            transform.position = InitialPos;
        }
    }

    public void addWater()
    {
        if (waterLevel < 1.0f)
        {
            waterLevel += Time.deltaTime / 7.5f;
        }
    }

    public bool pourWater()
    {
        if (waterLevel > 0.0f)
        {
            waterLevel -= Time.deltaTime / 2.0f;
            return true;
        }
        return false;
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
