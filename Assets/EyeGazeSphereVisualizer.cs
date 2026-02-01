using System.Collections.Generic;
using UnityEngine;

public class EyeGazeSphereVisualizer : MonoBehaviour
{
    public GameObject spherePrefab;
    public float defaultDistance = 3f;
    public LayerMask raycastLayers = ~0;

    private GameObject sphereInstance;

    public GameObject leftEye, rightEye, head, leftEyeActual;

    private List<Vector3> spherePositions = new List<Vector3>();
    private float positionRecordTime = 0.5f; // seconds

    void Start()
    {
        sphereInstance = spherePrefab;
    }

    void Update()
    {
        Vector3 origin = head.transform.position;
        Vector3 direction = leftEye.transform.forward;
        Debug.DrawRay(origin, direction * 10f, Color.yellow);

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, 10f, raycastLayers))
        {
            sphereInstance.transform.position = hit.point;
        }
        else
        {
            sphereInstance.transform.position = origin + direction * defaultDistance;
        }

        // Record the current position with timestamp
        spherePositions.Add(sphereInstance.transform.position);
        // Remove positions older than 1 second
        while (spherePositions.Count > 1 && spherePositions.Count > (1f / Time.deltaTime))
        {
            spherePositions.RemoveAt(0);
        }

        // Draw lines between consecutive positions
        for (int i = 1; i < spherePositions.Count; i++)
        {
            Debug.DrawLine(spherePositions[i - 1], spherePositions[i], Color.cyan, 0f, false);
        }
    }
}
