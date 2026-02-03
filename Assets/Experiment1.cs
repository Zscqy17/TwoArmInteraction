using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

enum Exp1Type
{
    Manual,
    Request,
    Auto
}

public class Experiment1 : MonoBehaviour
{
    // this is the script for all things featured in experiment 1.
    /*
     * The key parts of the experiment goes like this:
     * First there will be 2 different task areas, the left side with a water faucet, and the right side with a stove.
     * 
     * The player can preform the following actions: 
     * 1. hold the water faucet's button to dispense water, trigger: hand collider entering button's collider
     * 2. grab the cup, move to the water faucet, which will fill the cup, trigger: cup's collider entering faucet's collider
     * 3. move the cup to the stove, and pour the water into the pan, trigger: cup's collider entering pan's collider
     * 4. grab the pan and move it around, trigger: pan's position movement
     * 
     * when the faucet button is pressed and when the cup's collider is in the faucet's collider, the cup's water value is increased.
     * when the cup enters the pan's collider, the cup's water value is decreased, and the pan's burning value is decreased.
     * when the pan is moved around, the meal's pregress is increased.
     * the pan's burning value increases with time, which needs the player to pour water to lower it.
     * 
     */
    [Header("VR Systems")]
    [SerializeField] GameObject VRsys; [SerializeField] GameObject leftHand, rightHand;
    //public GameObject robot;

    [Header("Experiment Objects")]
    [SerializeField] GameObject faucetObject;
    [SerializeField] GameObject mugObject, panObject, spatulaObject, saltObject;

    [Header("Experiment Scripts")]
    [SerializeField] ExpFaucet ExpFaucet;
    [SerializeField] ExpMug ExpMug;
    [SerializeField] ExpPan ExpPan;
    [SerializeField] ExpSalt ExpSalt;

    [SerializeField] OVRGrabbable triggerSphere;

    [Header("Experiment UI")]
    [SerializeField] Slider burntLevel; [SerializeField] Slider saltLevel, progressLevel; [SerializeField] GameObject initText, doneText, failedText, tutorial1, tutorial2, tutorial3;

    float burntAmount, saltAmount, progressAmount;
    bool foodDone, ongoing, grabflag, progressHold;

    Color warningColor = new Color(1f, 0.6f, 0.6f);
    float saltCycle = 15f, waterCycle = 20f;

    [Header("Arms System")]
    public DualArm armSys;

    [Header("One-shot Automation")]
    [SerializeField] private AnimationClip waterOneShotClip;
    [SerializeField] private AnimationClip saltOneShotClip;

    // One-shot automation state (start on keypress, take back when done)
    private bool pendingWaterOneShot;
    private bool pendingSaltOneShot;
    private float waterOneShotEndTime;
    private float saltOneShotEndTime;

    // --- Arm control helpers (avoid confusing bool parameters sprinkled around) ---
    private void SetLeftArmAutomation(bool enabled, Transform target = null)
    {
        if (armSys == null) return;
        armSys.switchControl(enabled, left: true);
        if (enabled && target != null)
        {
            armSys.updateTarget(target, isRight: false);
        }
    }

    private void SetRightArmAutomation(bool enabled, Transform target = null)
    {
        if (armSys == null) return;
        armSys.switchControl(enabled, left: false);
        if (enabled && target != null)
        {
            armSys.updateTarget(target, isRight: true);
        }
    }

    [Header("Debug / Controls")]
    [SerializeField] private KeyCode restartSceneKey = KeyCode.R;
    [SerializeField] private bool autoStartOnPlay = false;

    [Header("Logging")]
    [SerializeField] private ExpLogging logger;

    private void Start()
    {
        ongoing = false;
        foodDone = false;
        grabflag = false;
        progressHold = false;
        burntAmount = 0f;
        saltAmount = 0f;
        progressAmount = 0f;
        if (autoStartOnPlay)
        {
            startExperiment();
        }
        //Vector3 compensate = new Vector3(-armSysL.head.position.x, 0, -2.2f - armSysL.head.position.z);
        //VRsys.transform.position += compensate;
    }

    private void Update()
    {
        if (triggerSphere.isGrabbed)
        {
            // we've grabbed the trigger sphere
            grabflag = true;
        }
        else
        {
            if (grabflag)
            {
                // we have grabbed it once and now we let go of the sphere
                // which triggers the experiment to start, and hides the sphere.
                // the trigger sphere can act like a timestamp of some sort
                //triggerSphere.grabbedBy.ForceRelease(triggerSphere);
                triggerSphere.GetComponent<MeshRenderer>().enabled = false;
                triggerSphere.transform.GetChild(0).gameObject.SetActive(false);
                //triggerSphere.gameObject.SetActive(false);
                grabflag = false;
                tutorial1.SetActive(false);
                tutorial2.SetActive(false);
                tutorial3.SetActive(false);
                StartTrial();
            }
        }

        if (ongoing)
        {
            TrackMetrics(Time.deltaTime);
        }

        checkStatus();
        switchAutomation();
        HandleOneShotAutomationCompletion();

        // Quick restart when iterating on the experiment
        if (Input.GetKeyUp(restartSceneKey))
        {
            RestartScene();
        }
    }

    private void TrackMetrics(float dt)
    {
        bool onStove = (ExpPan != null) && ExpPan.IsOnStove;
        bool stirring = (ExpPan != null) && ExpPan.IsStirring;
        bool panGrabbed = (ExpPan != null) && ExpPan.IsPanGrabbed;

        if (logger != null)
        {
            logger.Track(dt, burntAmount, saltAmount, progressHold, onStove, stirring, panGrabbed);
        }
    }

    void checkStatus()
    {
        if (ongoing)
        {
            burntLevel.value = burntAmount;
            progressLevel.value = progressAmount;
            saltLevel.value = saltAmount;

            // also update the slider's color
            UpdateSliderColor(saltLevel, saltAmount);
            UpdateSliderColor(burntLevel, burntAmount);
        }
        else
        {
            if (foodDone)
            {
                // now end the experiment

            }
        }
        if (burntAmount >= 1f || saltAmount >= 1f)
        {
            // When water/salt reaches 1.0, we DO NOT fail the trial.
            // Instead, we pause the main progress until the participant adds water/salt.
            progressHold = true;
            ExpPan.ProgressPause();
        }
        else
        {
            progressHold = false;
            ExpPan.Resume();
        }
        if (progressAmount >= 1f)
        {
            EndTrial(success: true, endReason: "completed");
        }
    }

    // Ends one-shot automation after a fixed duration (Exp* scripts do not auto-clear their Automated() flags).
    private void HandleOneShotAutomationCompletion()
    {
        if (pendingWaterOneShot && Time.time >= waterOneShotEndTime)
        {
            pendingWaterOneShot = false;
            if (ExpMug != null) ExpMug.Takeback();
            if (ExpFaucet != null) ExpFaucet.Takeback();
            SetLeftArmAutomation(false);
        }

        if (pendingSaltOneShot && Time.time >= saltOneShotEndTime)
        {
            pendingSaltOneShot = false;
            if (ExpSalt != null) ExpSalt.Takeback();
            SetRightArmAutomation(false);
        }
    }

    public void TriggerWaterOneShot()
    {
        // One-shot trigger for adding water (LEFT ARM)
        if (pendingWaterOneShot)
        {
            return;
        }

        pendingWaterOneShot = true;
        waterOneShotEndTime = Time.time + Mathf.Max(0.01f, waterOneShotClip.length);

        if (ExpFaucet != null) ExpFaucet.Overtake();
        if (ExpMug != null) ExpMug.Overtake();
        SetLeftArmAutomation(true, ExpMug != null ? ExpMug.transform : null);
    }

    public void TriggerSaltOneShot()
    {
        // One-shot trigger for salting (RIGHT ARM)
        if (pendingSaltOneShot)
        {
            return;
        }

        pendingSaltOneShot = true;
        saltOneShotEndTime = Time.time + Mathf.Max(0.01f, saltOneShotClip.length);

        if (ExpSalt != null) ExpSalt.Overtake();
        if (ExpSalt != null) SetRightArmAutomation(true, ExpSalt.transform);
    }

    void switchAutomation()
    {
        if (Input.GetKeyUp(KeyCode.Alpha1))
        {
            TriggerWaterOneShot();
        }
        if (Input.GetKeyUp(KeyCode.Alpha2))
        {
            TriggerSaltOneShot();
        }
        if (Input.GetKeyUp(KeyCode.Alpha3))
        {
            // place holder
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            Vector3 compensate = new Vector3(VRsys.transform.position.x - armSys.head.position.x, 0, VRsys.transform.position.z - armSys.head.position.z);
            VRsys.transform.position += compensate;
        }
    }

    private void RestartScene()
    {
        // If we're mid-trial, log it as an aborted/restarted trial before reloading.
        if (ongoing)
        {
            EndTrial(success: false, endReason: "restart");
        }

        // Prefer the arm system helper if present; otherwise restart directly.
        if (armSys != null)
        {
            armSys.Resart();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void startExperiment()
    {
        // Backwards-compatible entry point (older scripts / inspector hookups)
        StartTrial();
    }

    public void endExperiment(bool success)
    {
        // Keeping signature for compatibility, but in the current design "failure" is not used
        // (burn/salt reaching 1.0 only pauses progress; it does not end the trial).
        EndTrial(success, endReason: success ? "completed" : "ended");
    }

    private void StartTrial()
    {
        // Reset trial state
        foodDone = false;
        ongoing = true;
        pendingWaterOneShot = false;
        pendingSaltOneShot = false;
        waterOneShotEndTime = 0f;
        saltOneShotEndTime = 0f;
        if (logger != null)
        {
            logger.StartTrial((ExpPan != null) && ExpPan.IsPanGrabbed);
        }
    }

    private void EndTrial(bool success, string endReason)
    {
        if (!ongoing) return;

        foodDone = true;
        ongoing = false;

        // Ensure no one-shot automations stay latched on after a trial ends
        pendingWaterOneShot = false;
        pendingSaltOneShot = false;
        waterOneShotEndTime = 0f;
        saltOneShotEndTime = 0f;

        // UI / objects: keep existing behavior for "end of session"
        faucetObject.SetActive(false);
        mugObject.SetActive(false);
        panObject.SetActive(false);
        spatulaObject.SetActive(false);
        initText.SetActive(false);
        if (success)
        {
            doneText.SetActive(true);
        }

        if (logger != null)
        {
            logger.EndTrial(success, endReason, burntAmount, saltAmount, progressAmount);
        }
    }

    /// <summary>
    /// Called by ExpPan whenever pouring water causes an actual burn reduction this frame.
    /// Used to count distinct water interventions ("episodes").
    /// </summary>
    public void NotifyWaterEffect()
    {
        if (logger != null) logger.NotifyWaterEffect();
    }

    /// <summary>
    /// Called by ExpPan whenever adding salt causes an actual salt reduction this frame.
    /// Used to count distinct salt interventions ("episodes").
    /// </summary>
    public void NotifySaltEffect()
    {
        if (logger != null) logger.NotifySaltEffect();
    }

    public void updateVal(float burnt, float salt, float progress)
    {
        burntAmount = burnt;
        saltAmount = salt;
        progressAmount = progress;
        Debug.LogWarning("Updated values: " + burntAmount + ", " + saltAmount + ", " + progressAmount);
    }

    void UpdateSliderColor(Slider slider, float value)
    {
        // Access the Fill Rect's Image component
        Image fillImage = slider.fillRect.GetComponent<Image>();

        // Change color based on value
        if (value >= 0.99f)
        {
            fillImage.color = warningColor;
        }
        else
        {
            fillImage.color = Color.white;
        }
    }

    public bool experimentStarted()
    {
        return ongoing;
    }

}