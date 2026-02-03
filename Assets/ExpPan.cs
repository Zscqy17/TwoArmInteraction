using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExpPan : MonoBehaviour
{
    [SerializeField] ExpMug Mug;
    [SerializeField] ExpSalt Salt;
    [SerializeField] GameObject Stove;
    float burntLevel, saltLevel, progressLevel;
    bool stir, burn;
    bool automation_flag;
    bool stop_flag;
    [SerializeField] OVRGrabbable handle;
    [SerializeField] Experiment1 manager;

    [Header("Tuning (seconds to fill from 0 → 1)")]
    // ~4 water interventions over a ~100s session => ~20s to fill burn meter
    [SerializeField] private float burnFillSeconds; //debug only, should be 20
    // ~4–5 salt interventions over a ~100s session => ~14s to fill salt meter
    [SerializeField] private float saltFillSeconds; //debug only, should be 14
    // Total session duration target (when stirring on the stove and not paused)
    [SerializeField] private float progressFillSeconds; // should be 90
    // Start is called before the first frame update
    void Start()
    {
        burntLevel = 0.0f;
        saltLevel = 0.0f;
        progressLevel = 0.0f;
        stir = false;
        burn = false;
        automation_flag = false;
        stop_flag = false;
    }

    // Exposed state for logging/analytics (read-only)
    public bool IsOnStove => burn;
    public bool IsStirring => stir;
    public bool IsPanGrabbed => handle != null && handle.isGrabbed;

    // Update is called once per frame
    void Update()
    {
        Debug.LogError(manager.experimentStarted());
        if (manager.experimentStarted())
        {
            Debug.LogError("Pan Update: burn=" + burn + ", stir=" + stir + ", grabbed=" + IsPanGrabbed + ", stop=" + stop_flag);
            if (burn && stir && IsPanGrabbed && !stop_flag)
            {
                // 1. when the pan is kept over the stove
                // 2. when the user is stiring the pan
                // 3. when the progress is not hindered by anything
                burntLevel = Mathf.Clamp01(burntLevel + Time.deltaTime / Mathf.Max(0.01f, burnFillSeconds));
                saltLevel = Mathf.Clamp01(saltLevel + Time.deltaTime / Mathf.Max(0.01f, saltFillSeconds));
                progressLevel = Mathf.Clamp01(progressLevel + Time.deltaTime / Mathf.Max(0.01f, progressFillSeconds));
            }
            else if (burn && stir && IsPanGrabbed)
            {
                // Progress is paused, but time pressure continues.
                burntLevel = Mathf.Clamp01(burntLevel + Time.deltaTime / Mathf.Max(0.01f, burnFillSeconds));
                saltLevel = Mathf.Clamp01(saltLevel + Time.deltaTime / Mathf.Max(0.01f, saltFillSeconds));
            }
            //Debug.Log(burntLevel + "," + saltLevel + "," + progressLevel);
            manager.updateVal(burntLevel, saltLevel, progressLevel);
        }
        if (Mug.Automated())
        {
            if (burntLevel > 0.0f)
            {
                //burntLevel -= Time.deltaTime / 1.5f;
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // if the watercup is over the pan, add water and reduce the burnt level, if the spatula is over the pan, make meal
        if (other.gameObject.name == Mug.gameObject.name)
        {
            if (Mug.pourWater())
            {
                // if there is water in the cup
                if (burntLevel > 0.0f)
                {
                    burntLevel = Mathf.Clamp01(burntLevel - Time.deltaTime / 0.4f);
                    if (manager != null)
                    {
                        manager.NotifyWaterEffect();
                    }
                }
            }
        }
        if (other.gameObject.name == Salt.gameObject.name)
        {
            if (saltLevel > 0.0f)
            {
                saltLevel = Mathf.Clamp01(saltLevel - Time.deltaTime / 0.4f);
                if (manager != null)
                {
                    manager.NotifySaltEffect();
                }
            }
        }
        if (other.gameObject.name == "Spatula")
        {
            stir = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Spatula")
        {
            stir = true;
        }
        if (other.gameObject.name == "stove")
        {
            burn = true;
            try
            {
                other.GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
            }
            catch { }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "Spatula")
        {
            stir = false;
        }
        if (other.gameObject.name == "stove")
        {
            burn = false;
            try
            {
                other.GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
            }
            catch { }
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

    public void ProgressPause()
    {
        stop_flag = true;
    }

    public void Resume()
    {
        stop_flag = false;
    }


}
