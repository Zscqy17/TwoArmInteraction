using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class timerecord : MonoBehaviour
{
    public TMPro.TextMeshProUGUI record;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private float lastInvokeTime = -1f;

    public void RecordTime()
    {
        float currentTime = Time.time;
        float timeSinceLastInvoke = 0;

        if (lastInvokeTime != -1f)
        {
            timeSinceLastInvoke = currentTime - lastInvokeTime;
        }

        lastInvokeTime = currentTime;



        Debug.Log("Time since last invoke: " + timeSinceLastInvoke + " seconds");
        record.text = record.text + "\n" + timeSinceLastInvoke.ToString();
    }

    public void RecordSelection(string selected)
    {
        record.text = record.text + "\nSelected " + selected;
    }

}
