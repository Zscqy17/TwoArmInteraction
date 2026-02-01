using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasFollowObject : MonoBehaviour
{
    public GameObject canvas;
    public Transform hand;
    public Transform head;


    // Start is called before the first frame update
    void Start()
    {
        canvas.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        //canvas.transform.position = hand.position + Vector3.up * 0.1f;
        //canvas.transform.LookAt(head);
        if (OVRInput.GetDown(OVRInput.RawButton.X))
        {
            //canvas.SetActive(!canvas.activeSelf);
        }
    }
}
