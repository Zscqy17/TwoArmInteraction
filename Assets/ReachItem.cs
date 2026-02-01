using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class ReachItem : MonoBehaviour
{
    public GameObject[] items;
    public Transform rightHand;
    public GameObject rightHandModel;

    int currentIndex = 0;
    Vector3 originalHandPosition;
    Vector3 lastHandPosition;
    Vector3 moveVec;

    void Start()
    {
        UpdateItemVisibility();
        lastHandPosition = rightHand.position;
        rightHandModel.transform.position = rightHand.position;
        moveVec = Vector3.zero;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Joystick1Button3))
        {
            // switches between the test items
            // this is a manual way of switches between items, while it should have been automatic in the end product.
            // I mean, "target switches" was also one question we need to solve
            // test update
            SwitchItem();
        }

        if (items.Length > 0)
        {
            Vector3 targetPosition = items[currentIndex].transform.position;

            if (IsUserMovingTowards(targetPosition))
            {
                if (items[currentIndex].GetComponent<Renderer>().material.color != Color.red)
                {
                    items[currentIndex].GetComponent<Renderer>().material.color = Color.red;
                }
                //Vector3 direction = (targetPosition - rightHand.position).normalized;
                Debug.Log((targetPosition - rightHand.position).magnitude);
                rightHandModel.transform.position += moveVec * 1.6f * (targetPosition - rightHand.position).magnitude;
            }
            else
            {
                if (items[currentIndex].GetComponent<Renderer>().material.color != Color.white)
                {
                    items[currentIndex].GetComponent<Renderer>().material.color = Color.white;
                }
                rightHandModel.transform.position += moveVec;
            }
        }
    }

    void SwitchItem()
    {
        currentIndex = (currentIndex + 1) % items.Length;
        UpdateItemVisibility();
    }

    void UpdateItemVisibility()
    {
        for (int i = 0; i < items.Length; i++)
        {
            items[i].SetActive(i == currentIndex);
        }
    }

    bool IsUserMovingTowards(Vector3 targetPosition)
    {
        Vector3 currentHandPosition = rightHand.position;

        moveVec = currentHandPosition - lastHandPosition;
        Vector3 toTarget = (targetPosition - rightHandModel.transform.position).normalized;

        float dotProduct = Vector3.Dot(toTarget, moveVec.normalized);
        float mag = Vector3.Magnitude(moveVec);

        //Debug.Log(mag + ", " + dotProduct);

        lastHandPosition = currentHandPosition;
        if (mag > 0.005f && dotProduct > 0f)
        {
            return true;
        }

        return false;
    }
}