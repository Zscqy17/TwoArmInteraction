using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

public enum InteractionMode
{
    Voice,
    Gesture,
    Proxy
}

public class ProxyInteraction : MonoBehaviour
{
    [Header("Interaction Mode")]
    [SerializeField] private InteractionMode mode = InteractionMode.Proxy;

    [Header("Experiment")]
    [SerializeField] private Experiment1 experiment;

    [Header("UI Threshold")]
    [SerializeField] private Slider waterSlider;
    [SerializeField] private Slider saltSlider;
    [SerializeField] private GameObject waterNotifyCanvas;
    [SerializeField] private GameObject saltNotifyCanvas;

    [Header("Items")]
    [SerializeField] private GameObject waterCup;
    private Grabbable waterCupGrab;
    [SerializeField] private GameObject saltContainer;
    private Grabbable saltContainerGrab;

    [Header("Areas")]
    [SerializeField] private Collider panArea;
    [SerializeField] private Collider waterDenyArea;
    [SerializeField] private Collider saltDenyArea;

    private Collider waterCupCollider;
    private Collider saltContainerCollider;

    private Pose waterCupStartPose;
    private Pose saltContainerStartPose;

    // Per-item prompt control
    private bool waterHighRange = false;
    private float waterTriggerThreshold;
    private bool waterPromptActive = false;

    private bool saltHighRange = false;
    private float saltTriggerThreshold;
    private bool saltPromptActive = false;

    private bool waterNeedsReset = false;
    private bool saltNeedsReset = false;

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
            waterCupGrab = waterCup.GetComponent<Grabbable>();
        }
        if (saltContainer != null)
        {
            saltContainerCollider = saltContainer.GetComponentInChildren<Collider>();
            saltContainerStartPose = new Pose(saltContainer.transform.position, saltContainer.transform.rotation);
            saltContainerGrab = saltContainer.GetComponent<Grabbable>();
        }

        waterTriggerThreshold = PickThreshold(waterHighRange);
        saltTriggerThreshold = PickThreshold(saltHighRange);

        // Hide prompts and items at start (keep GameObjects active)
        SetItemVisible(waterNotifyCanvas, false);
        SetItemVisible(saltNotifyCanvas, false);
        SetItemVisible(waterCup, false);
        SetItemVisible(saltContainer, false);
    }

    private void Update()
    {
        UpdatePromptVisibility();
        CheckOverlapTransitions();
        checkForResets();
        NotifyPromptState();
        HandleVoiceKeys();
    }

    private void HandleVoiceKeys()
    {
        if (mode != InteractionMode.Voice) return;

        if (Input.GetKeyUp(KeyCode.Q) && waterPromptActive)
        {
            OnWaterAccepted();
        }
        if (Input.GetKeyUp(KeyCode.P) && saltPromptActive)
        {
            OnSaltAccepted();
        }
    }

    private void UpdatePromptVisibility()
    {
        // Water
        if (!waterPromptActive && waterSlider != null)
        {
            if (waterNeedsReset)
            {
                if (waterSlider.value <= waterTriggerThreshold - 0.05f)
                    waterNeedsReset = false;
            }
            else
            {
                bool thresholdReached = waterSlider.value >= waterTriggerThreshold;
                bool full = waterSlider.value >= 0.99f;

                if (thresholdReached || full)
                {
                    waterPromptActive = true;
                    waterHighRange = !waterHighRange;
                    SetItemVisible(waterNotifyCanvas, true);
                    if (mode == InteractionMode.Proxy)
                    {
                        SetItemVisible(waterCup, true);
                        TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
                    }
                }
            }
        }

        // Salt
        if (!saltPromptActive && saltSlider != null)
        {
            if (saltNeedsReset)
            {
                if (saltSlider.value <= saltTriggerThreshold - 0.05f)
                    saltNeedsReset = false;
            }
            else
            {
                bool thresholdReached = saltSlider.value >= saltTriggerThreshold;
                bool full = saltSlider.value >= 0.99f;

                if (thresholdReached || full)
                {
                    saltPromptActive = true;
                    saltHighRange = !saltHighRange;
                    SetItemVisible(saltNotifyCanvas, true);
                    if (mode == InteractionMode.Proxy)
                    {
                        SetItemVisible(saltContainer, true);
                        TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
                    }
                }
            }
        }
    }

    private void CheckOverlapTransitions()
    {
        if (mode != InteractionMode.Proxy) return;

        bool waterGrabbed = waterCupGrab != null && waterCupGrab.SelectingPointsCount > 0;
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

        bool saltGrabbed = saltContainerGrab != null && saltContainerGrab.SelectingPointsCount > 0;
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
        if (waterToReset && (waterCupGrab == null || waterCupGrab.SelectingPointsCount == 0))
        {
            ResetWaterItem();
            waterToReset = false;
        }
        if (saltToReset && (saltContainerGrab == null || saltContainerGrab.SelectingPointsCount == 0))
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
        waterPromptActive = false;
        waterTriggerThreshold = PickThreshold(waterHighRange);
        waterNeedsReset = true;
        SetItemVisible(waterNotifyCanvas, false);
        ForceReleaseGrab(waterCupGrab);
        waterToReset = true;
        SetItemVisible(waterCup, false);
    }

    private void OnSaltAccepted()
    {
        if (experiment != null)
        {
            experiment.TriggerSaltOneShot();
        }
        saltPromptActive = false;
        saltTriggerThreshold = PickThreshold(saltHighRange);
        saltNeedsReset = true;
        SetItemVisible(saltNotifyCanvas, false);
        ForceReleaseGrab(saltContainerGrab);
        saltToReset = true;
        SetItemVisible(saltContainer, false);
    }

    private void OnWaterDenied()
    {
        waterPromptActive = false;
        waterTriggerThreshold = PickThreshold(waterHighRange);
        waterNeedsReset = true;
        SetItemVisible(waterNotifyCanvas, false);
        //ForceReleaseGrab(waterCupGrab);
        waterToReset = true;
        SetItemVisible(waterCup, false);
    }

    private void OnSaltDenied()
    {
        saltPromptActive = false;
        saltTriggerThreshold = PickThreshold(saltHighRange);
        saltNeedsReset = true;
        SetItemVisible(saltNotifyCanvas, false);
        //ForceReleaseGrab(saltContainerGrab);
        saltToReset = true;
        SetItemVisible(saltContainer, false);
    }

    private void RevealSaltIfHidden()
    {
        if (!saltPromptActive)
        {
            saltPromptActive = true;
            saltHighRange = !saltHighRange;
            SetItemVisible(saltNotifyCanvas, true);
            if (mode == InteractionMode.Proxy)
            {
                SetItemVisible(saltContainer, true);
                TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
            }
        }
    }

    private void RevealWaterIfHidden()
    {
        if (!waterPromptActive)
        {
            waterPromptActive = true;
            waterHighRange = !waterHighRange;
            SetItemVisible(waterNotifyCanvas, true);
            if (mode == InteractionMode.Proxy)
            {
                SetItemVisible(waterCup, true);
                TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
            }
        }
    }

    private static void SetItemVisible(GameObject obj, bool visible)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }

    private static void ForceReleaseGrab(Grabbable grabbable)
    {
        if (grabbable == null) return;
        while (grabbable.SelectingPointsCount > 0)
        {
            var points = grabbable.SelectingPoints;
            grabbable.ProcessPointerEvent(
                new PointerEvent(0, PointerEventType.Cancel, points[0]));
        }
    }

    private static bool IsItemVisible(GameObject obj)
    {
        if (obj == null) return false;
        var renderer = obj.GetComponentInChildren<Renderer>(true);
        return renderer != null && renderer.enabled;
    }

    private static void TryResetIfFree(GameObject obj, Pose pose, Grabbable grab)
    {
        if (obj == null) return;
        if (grab != null && grab.SelectingPointsCount > 0) return;
        obj.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }

    private void NotifyPromptState()
    {
        if (experiment != null)
        {
            experiment.SetPromptPause(waterPromptActive || saltPromptActive);
        }
    }

    private static bool IsOverlapping(Collider item, Collider area)
    {
        if (item == null || area == null) return false;
        return item.bounds.Intersects(area.bounds);
    }

    private static float PickThreshold(bool highRange)
    {
        return highRange
            ? Random.Range(0.56f, 0.9f)
            : Random.Range(0.23f, 0.45f);
    }

    private void ResetWaterItem()
    {
        TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
    }

    private void ResetSaltItem()
    {
        TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
    }

    public void GestureWater(bool agreed)
    {
        if (mode != InteractionMode.Gesture) return;
        if (!waterPromptActive) return;

        if (agreed)
            OnWaterAccepted();
        else
            OnWaterDenied();
    }

    public void GestureSalt(bool agreed)
    {
        if (mode != InteractionMode.Gesture) return;
        if (!saltPromptActive) return;

        if (agreed)
            OnSaltAccepted();
        else
            OnSaltDenied();
    }
}
