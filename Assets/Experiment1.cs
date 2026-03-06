using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Oculus.Interaction;

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

    [SerializeField] Grabbable triggerSphere;

    [Header("Experiment UI")]
    [SerializeField] Slider burntLevel; [SerializeField] Slider saltLevel, progressLevel; [SerializeField] GameObject initText, doneText, failedText, tutorial1, tutorial2, tutorial3;

    float burntAmount, saltAmount, progressAmount;
    bool foodDone, ongoing, grabflag, progressHold;
    private bool promptPause;
    private bool tutorialMode = true;
    public bool IsTutorialMode => tutorialMode;
    public float WaterOneShotDuration => waterOneShotClip != null ? waterOneShotClip.length : 0f;
    public float SaltOneShotDuration => saltOneShotClip != null ? saltOneShotClip.length : 0f;

    Color warningColor = new Color(1f, 0.6f, 0.6f);

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

    // Post-prompt hold: progress stays paused after a prompt is accepted
    // until the actual item enters the pan OR the one-shot animation ends.
    private bool waterEffectHold;
    private bool saltEffectHold;

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

    [Header("Logging")]
    [SerializeField] private ExpLogging logger;
    [SerializeField] private ProxyInteraction proxyInteraction;

    private void Start()
    {
        ongoing = false;
        foodDone = false;
        grabflag = false;
        progressHold = false;
        burntAmount = 0f;
        saltAmount = 0f;
        progressAmount = 0f;
        //Vector3 compensate = new Vector3(-armSysL.head.position.x, 0, -2.2f - armSysL.head.position.z);
        //VRsys.transform.position += compensate;
    }

    private void Update()
    {
        // Start experiment with Enter key
        if (!ongoing && Input.GetKeyUp(KeyCode.Return))
        {
            triggerSphere.GetComponent<MeshRenderer>().enabled = false;
            triggerSphere.transform.GetChild(0).gameObject.SetActive(false);
            //tutorial1.SetActive(false);
            //tutorial2.SetActive(false);
            //tutorial3.SetActive(false);
            StartTrial();
        }

        if (triggerSphere.SelectingPointsCount > 0)
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
        if (logger != null)
        {
            string modeName = proxyInteraction != null ? proxyInteraction.Mode.ToString() : "Unknown";
            logger.CheckProgressStart(progressAmount, modeName);
            logger.TrackHinderedTime(dt, progressHold);
        }
    }

    void checkStatus()
    {
        if (ongoing)
        {
            burntLevel.value = burntAmount;
            progressLevel.value = progressAmount;
            saltLevel.value = saltAmount;
        }
        else
        {
            if (foodDone)
            {
                // now end the experiment

            }
        }
        if (promptPause || waterEffectHold || saltEffectHold)
        {
            // Pause progress while a prompt is shown, OR while waiting
            // for the actual item to hit the pan / animation to finish.
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
            waterEffectHold = false;
            if (ExpMug != null) ExpMug.Takeback();
            if (ExpFaucet != null) ExpFaucet.Takeback();
            SetLeftArmAutomation(false);
        }

        if (pendingSaltOneShot && Time.time >= saltOneShotEndTime)
        {
            pendingSaltOneShot = false;
            saltEffectHold = false;
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
        waterEffectHold = true;
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
        saltEffectHold = true;
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
        // Discard the log — restart means the trial data is not useful.
        if (logger != null) logger.DiscardLog();
        ongoing = false;

        // Prefer the arm system helper if present; otherwise restart directly.
        if (armSys != null)
        {
            armSys.Resart();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnApplicationQuit()
    {
        // Discard log on quit so incomplete trials are not saved.
        if (logger != null) logger.DiscardLog();
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
        tutorialMode = false;

        // Clean up any running one-shot animations from tutorial
        if (pendingWaterOneShot)
        {
            if (ExpMug != null) ExpMug.Takeback();
            if (ExpFaucet != null) ExpFaucet.Takeback();
            SetLeftArmAutomation(false);
        }
        if (pendingSaltOneShot)
        {
            if (ExpSalt != null) ExpSalt.Takeback();
            SetRightArmAutomation(false);
        }

        // Reset trial state
        foodDone = false;
        ongoing = true;
        promptPause = false;
        pendingWaterOneShot = false;
        pendingSaltOneShot = false;
        waterEffectHold = false;
        saltEffectHold = false;
        waterOneShotEndTime = 0f;
        saltOneShotEndTime = 0f;
        if (logger != null)
        {
            logger.ResetForNewTrial();
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
        waterEffectHold = false;
        saltEffectHold = false;
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
            logger.EndLogging();
        }
    }

    /// <summary>
    /// Called by ExpPan whenever pouring water causes an actual burn reduction this frame.
    /// Used to count distinct water interventions ("episodes").
    /// </summary>
    public void NotifyWaterEffect()
    {
        waterEffectHold = false;
    }

    /// <summary>
    /// Called by ExpPan whenever adding salt causes an actual salt reduction this frame.
    /// Used to count distinct salt interventions ("episodes").
    /// </summary>
    public void NotifySaltEffect()
    {
        saltEffectHold = false;
    }

    public void updateVal(float burnt, float salt, float progress)
    {
        burntAmount = burnt;
        saltAmount = salt;
        progressAmount = progress;
    }

    public bool experimentStarted()
    {
        return ongoing;
    }

    public void SetPromptPause(bool paused)
    {
        promptPause = paused;
    }

    // ── Logging forwarding (called by ProxyInteraction) ──────────────

    public void LogPromptShown(string type)
    {
        if (logger != null) logger.OnPromptShown(type);
    }

    public void LogPromptDecision(string type, bool accepted)
    {
        if (logger != null) logger.OnPromptDecision(type, accepted);
    }
}