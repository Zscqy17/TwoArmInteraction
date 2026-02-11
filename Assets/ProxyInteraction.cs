using UnityEngine;
using UnityEngine.UI;

public class ProxyInteraction : MonoBehaviour
{
    [Header("Experiment")]
    [SerializeField] private Experiment1 experiment;

    [Header("UI Threshold")]
    [SerializeField] private Slider waterSlider;
    [SerializeField] private Slider saltSlider;
    [SerializeField] private GameObject waterNotifyCanvas;
    [SerializeField] private GameObject saltNotifyCanvas;
    [SerializeField] private float notifyThreshold = 0.75f;

    [Header("Items")]
    [SerializeField] private GameObject waterCup;
    private OVRGrabbable waterCupGrab;
    [SerializeField] private GameObject saltContainer;
    private OVRGrabbable saltContainerGrab;

    [Header("Areas")]
    [SerializeField] private Collider panArea;
    [SerializeField] private Collider waterDenyArea;
    [SerializeField] private Collider saltDenyArea;

    private Collider waterCupCollider;
    private Collider saltContainerCollider;

    private Pose waterCupStartPose;
    private Pose saltContainerStartPose;

    private bool waterDenied;
    private bool saltDenied;
    private bool waterInPan;
    private bool saltInPan;
    private bool waterInDeny;
    private bool saltInDeny;
    private bool waterToReset;
    private bool saltToReset;

    private void Awake()
    {
        if (waterCup != null)
        {
            waterCupCollider = waterCup.GetComponentInChildren<Collider>();
            waterCupStartPose = new Pose(waterCup.transform.position, waterCup.transform.rotation);
            waterCupGrab = waterCup.GetComponent<OVRGrabbable>();
        }
        if (saltContainer != null)
        {
            saltContainerCollider = saltContainer.GetComponentInChildren<Collider>();
            saltContainerStartPose = new Pose(saltContainer.transform.position, saltContainer.transform.rotation);
            saltContainerGrab = saltContainer.GetComponent<OVRGrabbable>();
        }
    }

    private void Update()
    {
        UpdatePromptVisibility();
        CheckOverlapTransitions();
        checkForResets();
    }

    private void UpdatePromptVisibility()
    {
        bool forceShowBoth = (waterSlider != null && waterSlider.value >= 1f) ||
                             (saltSlider != null && saltSlider.value >= 1f);

        bool waterThresholdReached = waterSlider != null && waterSlider.value >= notifyThreshold;
        if (!waterThresholdReached)
        {
            waterDenied = false;
        }

        bool saltThresholdReached = saltSlider != null && saltSlider.value >= notifyThreshold;
        if (!saltThresholdReached)
        {
            saltDenied = false;
        }

        if (forceShowBoth)
        {
            waterDenied = false;
            saltDenied = false;
        }

        bool showWater = forceShowBoth || (waterThresholdReached && !waterDenied);
        SetActiveIfNotNull(waterNotifyCanvas, showWater);
        SetItemVisible(waterCup, showWater);
        if (showWater) TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);

        bool showSalt = forceShowBoth || (saltThresholdReached && !saltDenied);
        SetActiveIfNotNull(saltNotifyCanvas, showSalt);
        SetItemVisible(saltContainer, showSalt);
        if (showSalt) TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
    }

    private void CheckOverlapTransitions()
    {
        bool waterGrabbed = waterCupGrab != null && waterCupGrab.isGrabbed;
        if (waterCupCollider != null && IsItemVisible(waterCup) && waterGrabbed)
        {
            bool inPan = IsOverlapping(waterCupCollider, panArea);
            if (inPan && !waterInPan)
            {
                waterInPan = true;
                OnWaterAccepted();
            }
            else if (!inPan && waterInPan)
            {
                waterInPan = false;
            }

            bool inDeny = IsOverlapping(waterCupCollider, waterDenyArea);
            if (inDeny && !waterInDeny)
            {
                waterInDeny = true;
                OnWaterDenied();
            }
            else if (!inDeny && waterInDeny)
            {
                waterInDeny = false;
            }
        }
        else
        {
            waterInPan = false;
            waterInDeny = false;
        }

        bool saltGrabbed = saltContainerGrab != null && saltContainerGrab.isGrabbed;
        if (saltContainerCollider != null && IsItemVisible(saltContainer) && saltGrabbed)
        {
            bool inPan = IsOverlapping(saltContainerCollider, panArea);
            if (inPan && !saltInPan)
            {
                saltInPan = true;
                OnSaltAccepted();
            }
            else if (!inPan && saltInPan)
            {
                saltInPan = false;
            }

            bool inDeny = IsOverlapping(saltContainerCollider, saltDenyArea);
            if (inDeny && !saltInDeny)
            {
                saltInDeny = true;
                OnSaltDenied();
            }
            else if (!inDeny && saltInDeny)
            {
                saltInDeny = false;
            }
        }
        else
        {
            saltInPan = false;
            saltInDeny = false;
        }
    }

    private void checkForResets()
    {
        if (waterToReset && (waterCupGrab == null || !waterCupGrab.isGrabbed))
        {
            ResetWaterItem();
            waterToReset = false;
        }
        if (saltToReset && (saltContainerGrab == null || !saltContainerGrab.isGrabbed))
        {
            ResetSaltItem();
            saltToReset = false;
        }
    }

    private void OnWaterAccepted()
    {
        if (experiment != null)
        {
            experiment.TriggerWaterOneShot();
        }
        waterToReset = true;
        SetItemVisible(waterCup, false);
    }

    private void OnSaltAccepted()
    {
        if (experiment != null)
        {
            experiment.TriggerSaltOneShot();
        }
        saltToReset = true;
        SetItemVisible(saltContainer, false);
    }

    private void OnWaterDenied()
    {
        waterDenied = true;
        UpdatePromptVisibility();
        waterToReset = true;
        SetItemVisible(waterCup, false);
        RevealSaltIfHidden();
    }

    private void OnSaltDenied()
    {
        saltDenied = true;
        UpdatePromptVisibility();
        saltToReset = true;
        SetItemVisible(saltContainer, false);
        RevealWaterIfHidden();
    }

    private void RevealSaltIfHidden()
    {
        if (!IsItemVisible(saltContainer))
        {
            saltDenied = false;
            SetActiveIfNotNull(saltNotifyCanvas, true);
            SetItemVisible(saltContainer, true);
            TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
        }
    }

    private void RevealWaterIfHidden()
    {
        if (!IsItemVisible(waterCup))
        {
            waterDenied = false;
            SetActiveIfNotNull(waterNotifyCanvas, true);
            SetItemVisible(waterCup, true);
            TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
        }
    }

    private static void SetItemVisible(GameObject obj, bool visible)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        foreach (var c in obj.GetComponentsInChildren<Collider>(true)) c.enabled = visible;
    }

    private static bool IsItemVisible(GameObject obj)
    {
        if (obj == null) return false;
        var renderer = obj.GetComponentInChildren<Renderer>(true);
        return renderer != null && renderer.enabled;
    }

    private static void TryResetIfFree(GameObject obj, Pose pose, OVRGrabbable grab)
    {
        if (obj == null) return;
        if (grab != null && grab.isGrabbed) return;
        obj.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }

    private static void SetActiveIfNotNull(GameObject obj, bool active)
    {
        if (obj != null && obj.activeSelf != active)
        {
            obj.SetActive(active);
        }
    }

    private static bool IsOverlapping(Collider item, Collider area)
    {
        if (item == null || area == null) return false;
        return item.bounds.Intersects(area.bounds);
    }

    private void ResetWaterItem()
    {
        SetItemVisible(waterCup, true);
        TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
    }

    private void ResetSaltItem()
    {
        SetItemVisible(saltContainer, true);
        TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
    }
}
