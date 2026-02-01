using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEditor.Rendering;

public class RayCast : MonoBehaviour
{
    // shoot out rays from the camera
    // draws out rays when hit
    // if hit object, reflect

    // 1. just ray
    // 2. shadow ray: when hit, shoot ray toward the light, darken those that are blocked
    // 3. specular ray: when hit, shoot reflected ray, if reflected ray hits the light, highlight the ray
    // 4. diffuse ray: when hit, shoot ray to light, based on degree with normal, become light or black
    // 5(0). off

    public Transform camPos, lightPos;
    public Camera camera;
    public TextMeshProUGUI prompt;
    Vector3 ro, rd;
    bool isUpdating;
    int usage;
    int index;

    string wallLayerName = "Wall";
    string lightLayerName = "Light";
    int objectLayer = 8;

    //line renderer
    public Transform lines;
    public LineRenderer line;
    int totalRayCount;
    Color nullcolor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    // resolution
    int resolution = 5;

    private void Start()
    {
        isUpdating = false;
        totalRayCount = 0;
        usage = 0;
    }
    private void Update()
    {
        index = 0;
        // change live update status
        if (OVRInput.GetDown(OVRInput.RawButton.Y))
        {
            isUpdating = !isUpdating;
        }

        // change what the lines are used for
        if (OVRInput.GetDown(OVRInput.RawButton.LThumbstickRight))
        {
            usage = (usage + 1) % 5;
            //cleanray();
        }

        // change what to be shown here

        if (isUpdating)
        {
            for (int i = 0; i < resolution; i++)
            {
                if (usage == 0)
                {
                    // ray is off
                    prompt.text = "Ray is OFF";
                    index = 0;
                }
                else
                {
                    // ray is on
                    for (int j = 0; j < resolution; j++)
                    {
                        // get uv coord
                        ro = camPos.position + camPos.forward * 0.01f;
                        float u = 0.5f * (i / (float)resolution - 0.5f);
                        float v = 0.5f * (j / (float)resolution - 0.5f);
                        rd = camPos.forward + camPos.right * u + camPos.up * v;
                        rd.Normalize();

                        CastRay(ro, rd, 2);
                    }
                }
            }

            // save unused lines
            for (int i = index; i < totalRayCount; i++)
            {
                lines.GetChild(index).gameObject.SetActive(false);
            }
        }

    }

    void cleanray()
    {
        foreach (Transform child in line.transform)
        {
            Destroy(child.gameObject);
        }
    }

    void CastRay(Vector3 ro, Vector3 rd, int maxDepth)
    {
        if (maxDepth <= 0)
        {
            return;
        }
        // shoot ray
        Ray ray = new Ray(ro, rd);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        // check hits
        if (hits.Length > 0)
        {
            // Sort the hits by distance
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Vector3 hitPoint = hits[0].point;

            // check if we have enough rays
            if (index + 1 > totalRayCount)
            {
                GameObject ob = Instantiate(line.gameObject, lines);
                totalRayCount++;
            }

            GameObject lineobj = lines.GetChild(index).gameObject;
            lineobj.SetActive(true);
            LineRenderer thisLine = lineobj.GetComponent<LineRenderer>();

            if (usage == 1)
            {
                // just normal reflection
                prompt.text = "Ray is casting & reflecting";
                // Check the layer of the nearest hit object
                DrawRay(thisLine, ro, hitPoint, Color.red);
                index++;

                // reflect
                Vector3 reflectedDir = Vector3.Reflect(hitPoint - ro, hits[0].normal);
                CastRay(hitPoint, reflectedDir, maxDepth - 1);
            }
            else if (usage == 2)
            {
                // shadow ray
                prompt.text = "Ray is simulating shadow ray";

                DrawRay(thisLine, ro, hitPoint, nullcolor);
                index++;

                // check shadow
                Vector3 lightDir = Vector3.Normalize(lightPos.position - hitPoint);
                CastShadowRay(hitPoint + hits[0].normal * 0.01f, lightDir);

            }
            else if (usage == 3)
            {
                // specular
                prompt.text = "Ray is showing specular";

                DrawRay(thisLine, ro, hitPoint, nullcolor);
                index++;

                // check specular
                Vector3 reflectedDir = Vector3.Reflect(hitPoint - ro, hits[0].normal);
                CastSpecularRefl(hitPoint, reflectedDir);
            }
            else if (usage == 4)
            {
                // diffuse
                prompt.text = "Ray is showing diffuse";

                DrawRay(thisLine, ro, hitPoint, nullcolor);
                index++;

                // check angle with light
                Vector3 lightDir = Vector3.Normalize(lightPos.position - hitPoint);
                float diffcoef = Vector3.Dot(lightDir, hits[0].normal) + 0.1f;
                if (diffcoef < 0.1f)
                {
                    diffcoef = 0.0f;
                }
                if (diffcoef > 1f)
                {
                    diffcoef = 1.0f;
                }
                CastDiffuseCheck(hitPoint, lightDir, new Color(diffcoef, diffcoef, diffcoef, 0.8f));
            }
            
        }
    }

    void CastShadowRay(Vector3 ro, Vector3 rd)
    {
        // shoot ray
        Ray ray = new Ray(ro, rd);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        // check hits
        if (hits.Length > 0)
        {
            // Sort the hits by distance
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Vector3 hitPoint = hits[0].point;

            // check if we have enough rays
            if (index + 1 > totalRayCount)
            {
                GameObject ob = Instantiate(line.gameObject, lines);
                totalRayCount++;
            }

            GameObject lineobj = lines.GetChild(index).gameObject;
            lineobj.SetActive(true);
            LineRenderer thisLine = lineobj.GetComponent<LineRenderer>();

            if (hits[0].collider.gameObject.layer != LayerMask.NameToLayer(lightLayerName))
            {
                DrawRay(thisLine, ro, hitPoint, Color.red);
            }
            else
            {
                DrawRay(thisLine, ro, hitPoint, nullcolor);
            }

            index++;
        }
    }

    void CastSpecularRefl(Vector3 ro, Vector3 rd)
    {
        // shoot ray
        Ray ray = new Ray(ro, rd);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        // check hits
        if (hits.Length > 0)
        {
            // Sort the hits by distance
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Vector3 hitPoint = hits[0].point;

            // check if we have enough rays
            if (index + 1 > totalRayCount)
            {
                GameObject ob = Instantiate(line.gameObject, lines);
                totalRayCount++;
            }

            GameObject lineobj = lines.GetChild(index).gameObject;
            lineobj.SetActive(true);
            LineRenderer thisLine = lineobj.GetComponent<LineRenderer>();

            if (hits[0].collider.gameObject.layer == LayerMask.NameToLayer(lightLayerName))
            {
                DrawRay(thisLine, ro, hitPoint, Color.white);
            }
            else
            {
                DrawRay(thisLine, ro, hitPoint, nullcolor);
            }

            index++;
        }
    }

    void CastDiffuseCheck(Vector3 ro, Vector3 rd, Color diff)
    {
        // shoot ray
        Ray ray = new Ray(ro, rd);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        // check hits
        if (hits.Length > 0)
        {
            // Sort the hits by distance
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Vector3 hitPoint = hits[0].point;

            // check if we have enough rays
            if (index + 1 > totalRayCount)
            {
                GameObject ob = Instantiate(line.gameObject, lines);
                totalRayCount++;
            }

            GameObject lineobj = lines.GetChild(index).gameObject;
            lineobj.SetActive(true);
            LineRenderer thisLine = lineobj.GetComponent<LineRenderer>();

            if (hits[0].collider.gameObject.layer == LayerMask.NameToLayer(lightLayerName))
            {
                DrawRay(thisLine, ro, hitPoint, diff);
            }
            else
            {
                DrawRay(thisLine, ro, hitPoint, nullcolor);
            }

            index++;
        }
    }


    void DrawRay(LineRenderer lineRenderer, Vector3 start, Vector3 end, Color color)
    {
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

}


