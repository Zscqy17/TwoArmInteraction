using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LaserPlacer : MonoBehaviour
{

    public Transform rayPos;
    public LineRenderer laser;
    public float laserLen;
    public GameObject highlight;
    Vector3 objSpawnPos;
    public GameObject[] objs;
    public Transform head;
    public Transform light;
    Vector3 lastobj;


    int index;
    Transform currentObj;
    GameObject selected;
    Button selection;

    // Start is called before the first frame update
    void Start()
    {
        index = 0;
        //highlight.SetActive(false);
        selected = null;
        selection = null;
        currentObj = null;
        //light.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        objSpawnPos = rayPos.position + rayPos.forward * laserLen;
        handleInputs();

        // show laser
        laser.startColor = Color.green;
        laser.endColor = Color.white;
        laser.SetPositions(new Vector3[] { rayPos.position, objSpawnPos });
        //debugCube.position = rayPos.position + rayPos.forward * laserLen;
        if (currentObj != null)
        {
            currentObj.gameObject.SetActive(true);
            currentObj.position = objSpawnPos;

        }
        

        if (OVRInput.GetDown(OVRInput.RawButton.A))
        {
            // spawn an object
            if (currentObj != null)
            {
                GameObject ob = Instantiate(currentObj.gameObject, currentObj.position, currentObj.rotation);
                ob.layer = 8;
                lastobj = ob.transform.position;
            }
        }
        if (OVRInput.GetDown(OVRInput.RawButton.B))
        {
            // Change occlusion
            //if (selected != null)
            //{
            //    selected.GetComponent<OcclusionController>().changeOcclude();
            //}

            // change light position
            light.gameObject.SetActive(true);
            light.position = rayPos.position;
            light.LookAt(lastobj);
        }
        Ray ray = new Ray(rayPos.position, objSpawnPos - rayPos.position);
        RaycastHit hit;
        //if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        //{
        //    if (hit.collider.gameObject.layer == 8)
        //    {
        //        highlight.SetActive(true);
        //        highlight.transform.position = hit.collider.transform.position;
        //        highlight.transform.LookAt(head);
        //        selected = hit.collider.gameObject;
        //    }
        //    else
        //    {
        //        highlight.SetActive(false);
        //        selected = null;
        //    }

        //}
        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            if (hit.collider.name.Contains("col"))
            {
                highlight.SetActive(true);
                highlight.transform.position = hit.point;
                selection = hit.collider.transform.parent.GetComponent<Button>();
            }
            else
            {
                highlight.SetActive(false);
                selection = null;
            }
        }
        else
        {
            highlight.SetActive(false);
            selection = null;
        }
        if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
        {
            if (selection != null)
            {
                selection.onClick.Invoke();
            }
        }
    }

    void handleInputs()
    {
        // change laser length based on input
        if (OVRInput.Get(OVRInput.RawButton.RThumbstickUp))
        {
            // increase distance
            laserLen += Time.deltaTime;
        }
        if (OVRInput.Get(OVRInput.RawButton.RThumbstickDown))
        {
            // decrease distance
            laserLen -= Time.deltaTime;
        }
        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickLeft))
        {
            switchObject(-1);
        }
        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickRight))
        {
            switchObject(1);
        }
        if (laserLen < 0.1f)
        {
            laserLen = 0.1f;
        }
    }

    void switchObject(int change)
    {
        // first hide current object
        int realIndex = Mathf.Abs(index % objs.Length);
        if (objs[realIndex] != null)
        {
            objs[realIndex].SetActive(false);
        }

        // change index
        index += change;

        // then show the next object
        realIndex = Mathf.Abs(index % objs.Length);
        if (objs[realIndex] != null)
        {
            objs[realIndex].SetActive(true);
            currentObj = objs[realIndex].transform;
        }
        else
        {
            currentObj = null;
        }
    }
}
