using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandRetargeting : MonoBehaviour
{
    [Header("Retargeting")]
    [SerializeField] private Transform hand;
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 deviation;
    [SerializeField] private float startDeviateDistance = 0.2f;

    private Vector3 handStartLocalPosition;

    // Start is called before the first frame update
    void Start()
    {
        if (hand != null)
        {
            handStartLocalPosition = hand.localPosition;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (hand == null || target == null || startDeviateDistance <= 0f) return;

        float dist = Vector3.Distance(hand.position, target.position);
        float t = Mathf.Clamp01(1f - (dist / startDeviateDistance));
        hand.localPosition = handStartLocalPosition + (deviation * t);
    }
}
