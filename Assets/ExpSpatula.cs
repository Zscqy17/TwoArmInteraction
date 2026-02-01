using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExpSpatula : MonoBehaviour
{
    Vector3 InitialPos;
    // Start is called before the first frame update
    void Start()
    {
        InitialPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        //safeguard mechanism, if falls through the floor, returns to its initial position
        if (transform.position.y <= 0)
        {
            transform.position = InitialPos;
        }
    }
}
