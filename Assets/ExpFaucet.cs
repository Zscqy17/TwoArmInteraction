using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExpFaucet : MonoBehaviour
{
    public GameObject progressBar;
    [SerializeField] GameObject pressButton;
    [SerializeField] GameObject leftHand, rightHand;
    [SerializeField] ExpMug Mug;
    bool isDispensing;
    bool automation_flag;
    Vector3 buttonUp, buttonDown;

    private void Start()
    {
        isDispensing = false;
        automation_flag = false;
        progressBar.SetActive(false);
        buttonUp = pressButton.transform.localPosition;
        buttonDown = pressButton.transform.localPosition + Vector3.forward * 0.05f;
    }

    private void Update()
    {

        if (Vector3.Distance(leftHand.transform.position, pressButton.transform.position) < 0.2f
            || Vector3.Distance(rightHand.transform.position, pressButton.transform.position) < 0.2f || automation_flag)
        {
            // checks if the button is being pressed, or if the faucet's automation is on
            progressBar.SetActive(true);
            isDispensing = true;
            pressButton.transform.localPosition = buttonDown;
        }
        else
        {
            progressBar.SetActive(false);
            isDispensing = false;
            pressButton.transform.localPosition = buttonUp;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // checks if the cup is in the area where water comes out
        if (other.gameObject.name == Mug.gameObject.name && isDispensing)
        {
            Mug.addWater();
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
