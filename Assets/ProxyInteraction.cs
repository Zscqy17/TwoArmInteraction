using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.DistanceReticles;

public enum InteractionMode
{
    Auto,
    Button,
    Gesture,
    Proxy
}

public class ProxyInteraction : MonoBehaviour
{
    [Header("Interaction Mode")]
    [SerializeField] private InteractionMode mode = InteractionMode.Proxy;
    public InteractionMode Mode => mode;

    [Header("Experiment")]
    [SerializeField] private Experiment1 experiment;

    [Header("UI Threshold")]
    [SerializeField] private Slider waterSlider;
    [SerializeField] private Slider saltSlider;
    [SerializeField] private GameObject waterNotifyCanvas;
    [SerializeField] private GameObject saltNotifyCanvas;

    [Header("Items – Proxy")]
    [SerializeField] private GameObject waterCup;
    private Grabbable waterCupGrab;
    private MeshFilter waterCupMeshFilter;
    [SerializeField] private GameObject saltContainer;
    private Grabbable saltContainerGrab;
    private MeshFilter saltContainerMeshFilter;
    [SerializeField] private GameObject pan;

    [Header("Reticles")]
    [SerializeField] private ReticleMeshDrawer leftProxyInHand;
    [SerializeField] private ReticleMeshDrawer rightProxyInHand;

    [Header("Items – Gesture")]
    [SerializeField] private GameObject waterGesture;
    [SerializeField] private GameObject saltGesture;

    [Header("Hands")]
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private float handProximityThreshold = 0.3f;

    [Header("Gaze")]
    [SerializeField] private ConicalFrustum gazeFrustum;

    [Header("Areas")]
    [SerializeField] private Collider panArea;
    [SerializeField] private Collider waterDenyArea;
    [SerializeField] private Collider saltDenyArea;
    [SerializeField] private Collider floorDenyArea;

    private Collider waterCupCollider;
    private Collider saltContainerCollider;

    private Pose waterCupStartPose;
    private Pose saltContainerStartPose;

    // Per-item prompt control (3 threshold stages + full-meter trigger)
    private static readonly float[][] ThresholdRanges = new float[][]
    {
        new float[] { 0.08f, 0.18f },
        new float[] { 0.25f, 0.50f },
        new float[] { 0.60f, 0.82f },
    };

    private int waterThresholdIndex = 0;
    private float waterTriggerThreshold;
    private bool waterPromptActive = false;

    private int saltThresholdIndex = 0;
    private float saltTriggerThreshold;
    private bool saltPromptActive = false;

    private bool waterFullTriggered = false;
    private bool saltFullTriggered = false;

    // Tracks whether a prompt was shown because a meter hit full.
    // When true, accepting one does NOT auto-deny the other.
    private bool waterFromFull = false;
    private bool saltFromFull = false;

    private bool waterInPan;
    private bool saltInPan;
    private bool waterInDeny;
    private bool saltInDeny;
    private bool waterToReset;
    private bool saltToReset;

    // Auto mode
    private int waterAutoPromptCount = 0;
    private int saltAutoPromptCount = 0;
    private float waterAutoTimer = -1f;
    private float saltAutoTimer = -1f;
    [Header("Auto Mode")]
    [SerializeField] private float autoResponseDelay = 0.1f;

    private void Awake()
    {
        if (waterCup != null)
        {
            waterCupCollider = waterCup.GetComponentInChildren<Collider>();
            waterCupStartPose = new Pose(waterCup.transform.position, waterCup.transform.rotation);
            waterCupGrab = waterCup.GetComponent<Grabbable>();
            waterCupMeshFilter = waterCup.GetComponentInChildren<MeshFilter>();
        }
        if (saltContainer != null)
        {
            saltContainerCollider = saltContainer.GetComponentInChildren<Collider>();
            saltContainerStartPose = new Pose(saltContainer.transform.position, saltContainer.transform.rotation);
            saltContainerGrab = saltContainer.GetComponent<Grabbable>();
            saltContainerMeshFilter = saltContainer.GetComponentInChildren<MeshFilter>();
        }

        // Disable grabbables at start — they get enabled when a prompt is shown
        SetGrabbableEnabled(waterCupGrab, false);
        SetGrabbableEnabled(saltContainerGrab, false);

        waterTriggerThreshold = PickThreshold(waterThresholdIndex);
        saltTriggerThreshold = PickThreshold(saltThresholdIndex);

        // Hide prompts and items at start (keep GameObjects active)
        SetItemVisible(waterNotifyCanvas, false);
        SetItemVisible(saltNotifyCanvas, false);
        SetItemVisible(waterCup, false);
        SetItemVisible(saltContainer, false);
        SetItemVisible(waterGesture, false);
        SetItemVisible(saltGesture, false);
        SetItemVisible(pan, false);
    }

    private void Update()
    {
        UpdatePromptVisibility();
        CheckOverlapTransitions();
        checkForResets();
        CheckFallthrough();
        NotifyPromptState();
        HandleButtonKeys();
        HandleAutoMode();
        SyncReticles();
    }

    private void SyncReticles()
    {
        bool anyPrompt = waterPromptActive || saltPromptActive;
        bool waterVisible = IsItemVisible(waterCup);
        bool saltVisible = IsItemVisible(saltContainer);
        bool waterGrabbed = waterCupGrab != null && waterCupGrab.SelectingPointsCount > 0;
        bool saltGrabbed = saltContainerGrab != null && saltContainerGrab.SelectingPointsCount > 0;

        // 1. Force-release grabs on items that are no longer visible
        if (!waterVisible && waterGrabbed)
            ForceReleaseGrab(waterCupGrab);
        if (!saltVisible && saltGrabbed)
            ForceReleaseGrab(saltContainerGrab);

        // 2. Enable/disable Grabbable components based on prompt state
        //    When no prompt is active and nothing is grabbed, disable grabbables.
        //    When a prompt is shown, enable the relevant grabbable.
        if (anyPrompt)
        {
            SetGrabbableEnabled(waterCupGrab, waterPromptActive);
            SetGrabbableEnabled(saltContainerGrab, saltPromptActive);
        }
        else
        {
            // No prompt — disable both (only if not currently grabbed, to avoid mid-grab disable)
            if (!waterGrabbed) SetGrabbableEnabled(waterCupGrab, false);
            if (!saltGrabbed) SetGrabbableEnabled(saltContainerGrab, false);
        }

        // 3. Reticle drawers: only enabled in Proxy mode when a prompt is active
        bool showReticles = mode == InteractionMode.Proxy && anyPrompt;
        if (leftProxyInHand != null)
            leftProxyInHand.enabled = showReticles;
        if (rightProxyInHand != null)
            rightProxyInHand.enabled = showReticles;

        // 4. Validate reticle mesh matches a currently visible proxy item
        ValidateReticleMesh(leftProxyInHand, waterVisible, saltVisible);
        ValidateReticleMesh(rightProxyInHand, waterVisible, saltVisible);

        // 5. Pan proxy: visible only in Proxy mode when water or salt proxy is grabbed
        bool showPan = mode == InteractionMode.Proxy && (waterGrabbed || saltGrabbed) && anyPrompt;
        SetItemVisible(pan, showPan);
    }

    /// <summary>
    /// If the ReticleMeshDrawer is enabled and its MeshFilter has a non-null mesh,
    /// verify that mesh belongs to one of the currently visible proxy items.
    /// If it doesn't match, clear the mesh to prevent stale outlines.
    /// </summary>
    private void ValidateReticleMesh(ReticleMeshDrawer drawer, bool waterVisible, bool saltVisible)
    {
        if (drawer == null || !drawer.enabled) return;

        var reticleFilter = drawer.GetComponent<MeshFilter>();
        if (reticleFilter == null || reticleFilter.sharedMesh == null) return;

        Mesh currentMesh = reticleFilter.sharedMesh;

        // Check if it matches any currently visible proxy item's mesh
        bool meshMatchesVisible = false;
        if (waterVisible && waterCupMeshFilter != null && waterCupMeshFilter.sharedMesh == currentMesh)
            meshMatchesVisible = true;
        if (saltVisible && saltContainerMeshFilter != null && saltContainerMeshFilter.sharedMesh == currentMesh)
            meshMatchesVisible = true;

        if (!meshMatchesVisible)
            reticleFilter.sharedMesh = null;
    }

    private static void SetGrabbableEnabled(Grabbable grab, bool enabled)
    {
        if (grab != null) grab.enabled = enabled;
    }

    private void HandleAutoMode()
    {
        if (mode != InteractionMode.Auto) return;

        // Water auto-response
        if (waterPromptActive && waterAutoTimer < 0f)
        {
            waterAutoTimer = autoResponseDelay;
        }
        if (waterAutoTimer >= 0f)
        {
            waterAutoTimer -= Time.deltaTime;
            if (waterAutoTimer < 0f && waterPromptActive)
            {
                waterAutoPromptCount++;
                // Water sequence: 1st deny, 2nd accept, 3rd+ accept
                if (waterAutoPromptCount == 1)
                    OnWaterDenied();
                else
                    OnWaterAccepted();
            }
        }

        // Salt auto-response
        if (saltPromptActive && saltAutoTimer < 0f)
        {
            saltAutoTimer = autoResponseDelay;
        }
        if (saltAutoTimer >= 0f)
        {
            saltAutoTimer -= Time.deltaTime;
            if (saltAutoTimer < 0f && saltPromptActive)
            {
                saltAutoPromptCount++;
                // Salt sequence: 1st accept, 2nd deny, 3rd+ accept
                if (saltAutoPromptCount == 2)
                    OnSaltDenied();
                else
                    OnSaltAccepted();
            }
        }
    }

    private void HandleButtonKeys()
    {
        if (mode != InteractionMode.Button) return;

        if (Input.GetKeyUp(KeyCode.Q) && waterPromptActive)
        {
            OnWaterAccepted();
        }
        if (Input.GetKeyUp(KeyCode.A) && waterPromptActive)
        {
            OnWaterDenied();
        }
        if (Input.GetKeyUp(KeyCode.P) && saltPromptActive)
        {
            OnSaltAccepted();
        }
        if (Input.GetKeyUp(KeyCode.L) && saltPromptActive)
        {
            OnSaltDenied();
        }
    }

    private void UpdatePromptVisibility()
    {
        // If either meter hits full, show BOTH prompts — but only once per full event.
        // After both prompts are dismissed, don't re-show them while still at 1.
        if (waterSlider != null && waterSlider.value >= 0.99f && !waterFullTriggered)
        {
            waterFullTriggered = true;
            RevealWaterIfHidden(fromFull: true);
            RevealSaltIfHidden(fromFull: true);
        }
        if (saltSlider != null && saltSlider.value >= 0.99f && !saltFullTriggered)
        {
            saltFullTriggered = true;
            RevealWaterIfHidden(fromFull: true);
            RevealSaltIfHidden(fromFull: true);
        }

        // Reset full-triggered flags once sliders drop back down
        if (waterSlider != null && waterSlider.value < 0.99f)
            waterFullTriggered = false;
        if (saltSlider != null && saltSlider.value < 0.99f)
            saltFullTriggered = false;

        // Water (threshold stages 0–2; stage 3 = full-meter, handled above)
        if (!waterPromptActive && waterSlider != null && waterThresholdIndex < ThresholdRanges.Length)
        {
            bool thresholdReached = waterSlider.value >= waterTriggerThreshold;

            if (thresholdReached)
            {
                    waterPromptActive = true;
                    waterFromFull = false;
                    waterThresholdIndex++;
                    if (waterThresholdIndex < ThresholdRanges.Length)
                        waterTriggerThreshold = PickThreshold(waterThresholdIndex);
                    SetItemVisible(waterNotifyCanvas, true);
                    if (experiment != null) experiment.LogPromptShown("water");
                    if (mode == InteractionMode.Proxy)
                    {
                        SetItemVisible(waterCup, true);
                        TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
                        SetItemVisible(waterGesture, false);
                    }
                    else if (mode == InteractionMode.Gesture)
                    {
                        SetItemVisible(waterGesture, true);
                        SetItemVisible(waterCup, true);
                        TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
                        SetChildrenActive(waterCup, false);
                    }
                    else
                    {
                        SetItemVisible(waterCup, false);
                        SetItemVisible(waterGesture, false);
                    }
            }
        }

        // Salt (threshold stages 0–2; stage 3 = full-meter, handled above)
        if (!saltPromptActive && saltSlider != null && saltThresholdIndex < ThresholdRanges.Length)
        {
            bool thresholdReached = saltSlider.value >= saltTriggerThreshold;

            if (thresholdReached)
            {
                    saltPromptActive = true;
                    saltFromFull = false;
                    saltThresholdIndex++;
                    if (saltThresholdIndex < ThresholdRanges.Length)
                        saltTriggerThreshold = PickThreshold(saltThresholdIndex);
                    SetItemVisible(saltNotifyCanvas, true);
                    if (experiment != null) experiment.LogPromptShown("salt");
                    if (mode == InteractionMode.Proxy)
                    {
                        SetItemVisible(saltContainer, true);
                        TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
                        SetItemVisible(saltGesture, false);
                    }
                    else if (mode == InteractionMode.Gesture)
                    {
                        SetItemVisible(saltGesture, true);
                        SetItemVisible(saltContainer, true);
                        TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
                        SetChildrenActive(saltContainer, false);
                    }
                    else
                    {
                        SetItemVisible(saltContainer, false);
                        SetItemVisible(saltGesture, false);
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
            bool inPan = IsOverlapping(waterCupCollider, panArea) && IsNearHand(waterCup.transform);
            if (inPan && !waterInPan)
            {
                waterInPan = true;
                OnWaterAccepted();
            }
            else if (!inPan && waterInPan)
            {
                waterInPan = false;
            }

            bool inDeny = IsOverlapping(waterCupCollider, waterDenyArea) || IsOverlapping(waterCupCollider, saltDenyArea)
                       || IsOverlapping(waterCupCollider, floorDenyArea);
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
            bool inPan = IsOverlapping(saltContainerCollider, panArea) && IsNearHand(saltContainer.transform);
            if (inPan && !saltInPan)
            {
                saltInPan = true;
                OnSaltAccepted();
            }
            else if (!inPan && saltInPan)
            {
                saltInPan = false;
            }

            bool inDeny = IsOverlapping(saltContainerCollider, saltDenyArea) || IsOverlapping(saltContainerCollider, waterDenyArea)
                       || IsOverlapping(saltContainerCollider, floorDenyArea);
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
            experiment.LogPromptDecision("water", true);
            experiment.TriggerWaterOneShot();
        }
        waterPromptActive = false;
        SetItemVisible(waterNotifyCanvas, false);
        ForceReleaseGrab(waterCupGrab);
        waterToReset = true;
        SetChildrenActive(waterCup, true);
        SetItemVisible(waterCup, false);
        SetItemVisible(waterGesture, false);

        // Auto-deny the other prompt unless it was triggered by full meter
        if (saltPromptActive && !saltFromFull)
            OnSaltDenied();
    }

    private void OnSaltAccepted()
    {
        if (experiment != null)
        {
            experiment.LogPromptDecision("salt", true);
            experiment.TriggerSaltOneShot();
        }
        saltPromptActive = false;
        SetItemVisible(saltNotifyCanvas, false);
        ForceReleaseGrab(saltContainerGrab);
        saltToReset = true;
        SetChildrenActive(saltContainer, true);
        SetItemVisible(saltContainer, false);
        SetItemVisible(saltGesture, false);

        // Auto-deny the other prompt unless it was triggered by full meter
        if (waterPromptActive && !waterFromFull)
            OnWaterDenied();
    }

    private void OnWaterDenied()
    {
        if (experiment != null) experiment.LogPromptDecision("water", false);
        waterPromptActive = false;
        SetItemVisible(waterNotifyCanvas, false);
        ForceReleaseGrab(waterCupGrab);
        waterToReset = true;
        SetChildrenActive(waterCup, true);
        SetItemVisible(waterCup, false);
        SetItemVisible(waterGesture, false);
    }

    private void OnSaltDenied()
    {
        if (experiment != null) experiment.LogPromptDecision("salt", false);
        saltPromptActive = false;
        SetItemVisible(saltNotifyCanvas, false);
        ForceReleaseGrab(saltContainerGrab);
        saltToReset = true;
        SetChildrenActive(saltContainer, true);
        SetItemVisible(saltContainer, false);
        SetItemVisible(saltGesture, false);
    }

    private void RevealSaltIfHidden(bool fromFull = false)
    {
        if (!saltPromptActive)
        {
            saltPromptActive = true;
            saltFromFull = fromFull;
            SetItemVisible(saltNotifyCanvas, true);
            if (experiment != null) experiment.LogPromptShown("salt");
            if (mode == InteractionMode.Proxy)
            {
                SetItemVisible(saltContainer, true);
                TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
                SetItemVisible(saltGesture, false);
            }
            else if (mode == InteractionMode.Gesture)
            {
                SetItemVisible(saltGesture, true);
                SetItemVisible(saltContainer, true);
                TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
                SetChildrenActive(saltContainer, false);
            }
            else
            {
                SetItemVisible(saltContainer, false);
                SetItemVisible(saltGesture, false);
            }
        }
    }

    private void RevealWaterIfHidden(bool fromFull = false)
    {
        if (!waterPromptActive)
        {
            waterPromptActive = true;
            waterFromFull = fromFull;
            SetItemVisible(waterNotifyCanvas, true);
            if (experiment != null) experiment.LogPromptShown("water");
            if (mode == InteractionMode.Proxy)
            {
                SetItemVisible(waterCup, true);
                TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
                SetItemVisible(waterGesture, false);
            }
            else if (mode == InteractionMode.Gesture)
            {
                SetItemVisible(waterGesture, true);
                SetItemVisible(waterCup, true);
                TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
                SetChildrenActive(waterCup, false);
            }
            else
            {
                SetItemVisible(waterCup, false);
                SetItemVisible(waterGesture, false);
            }
        }
    }

    private static void SetItemVisible(GameObject obj, bool visible)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }

    private static void SetChildrenActive(GameObject obj, bool active)
    {
        if (obj == null) return;
        for (int i = 0; i < obj.transform.childCount; i++)
            obj.transform.GetChild(i).gameObject.SetActive(active);
    }

    private static void ForceReleaseGrab(Grabbable grabbable)
    {
        if (grabbable == null) return;
        int safety = 20;
        while (grabbable.SelectingPointsCount > 0 && safety-- > 0)
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

    private void CheckFallthrough()
    {
        if (waterCup != null && waterCup.transform.position.y < 0f)
        {
            waterCup.transform.SetPositionAndRotation(waterCupStartPose.position, waterCupStartPose.rotation);
        }
        if (saltContainer != null && saltContainer.transform.position.y < 0f)
        {
            saltContainer.transform.SetPositionAndRotation(saltContainerStartPose.position, saltContainerStartPose.rotation);
        }

        // Deny items that land on the floor deny area
        if (waterCupCollider != null && IsItemVisible(waterCup) && IsOverlapping(waterCupCollider, floorDenyArea))
        {
            OnWaterDenied();
        }
        if (saltContainerCollider != null && IsItemVisible(saltContainer) && IsOverlapping(saltContainerCollider, floorDenyArea))
        {
            OnSaltDenied();
        }
    }

    private static bool IsOverlapping(Collider item, Collider area)
    {
        if (item == null || area == null) return false;
        return item.bounds.Intersects(area.bounds);
    }

    private bool IsNearHand(Transform item)
    {
        
        if (item == null) return false;
        
        if (leftHand != null)
        {
            float distToLeft = (item.position - leftHand.position).sqrMagnitude;
            //Debug.LogError($"{item.name} Distance to left hand: {distToLeft}, threshold is {handProximityThreshold}");
            if (distToLeft <= handProximityThreshold)
            {
                //Debug.LogError($"{item.name} is near left hand");
                return true;
            }
        }
        
        if (rightHand != null)
        {
            float distToRight = (item.position - rightHand.position).sqrMagnitude;
            //Debug.LogError($"{item.name} Distance to right hand: {distToRight}, threshold is {handProximityThreshold}");
            if (distToRight <= handProximityThreshold) {
                //Debug.LogError($"{item.name} is near right hand");
                return true;
            }
                
        }
        
        return false;
    }

    private static float PickThreshold(int index)
    {
        if (index < 0 || index >= ThresholdRanges.Length) return float.MaxValue;
        return Random.Range(ThresholdRanges[index][0], ThresholdRanges[index][1]);
    }

    private void ResetWaterItem()
    {
        TryResetIfFree(waterCup, waterCupStartPose, waterCupGrab);
    }

    private void ResetSaltItem()
    {
        TryResetIfFree(saltContainer, saltContainerStartPose, saltContainerGrab);
    }

    public void GestureRespond(bool agreed)
    {
        if (mode != InteractionMode.Gesture) return;
        if (!waterPromptActive && !saltPromptActive) return;

        // Determine which active prompt the user is most looking at
        bool gazeWater = false;
        bool gazeSalt = false;

        if (waterPromptActive && saltPromptActive)
        {
            float waterAngle = GazeAngleTo(waterNotifyCanvas);
            float saltAngle = GazeAngleTo(saltNotifyCanvas);
            if (waterAngle <= saltAngle)
                gazeWater = true;
            else
                gazeSalt = true;
        }
        else if (waterPromptActive)
        {
            gazeWater = true;
        }
        else
        {
            gazeSalt = true;
        }

        if (gazeWater)
        {
            if (agreed) OnWaterAccepted(); else OnWaterDenied();
        }
        else if (gazeSalt)
        {
            if (agreed) OnSaltAccepted(); else OnSaltDenied();
        }
    }

    /// <summary>
    /// Returns the angle (degrees) between the gaze frustum's forward direction
    /// and the direction toward the given object. Lower = more directly looked at.
    /// </summary>
    private float GazeAngleTo(GameObject target)
    {
        if (gazeFrustum == null || target == null) return float.MaxValue;
        Vector3 dir = (target.transform.position - gazeFrustum.Pose.position).normalized;
        return Vector3.Angle(gazeFrustum.Direction, dir);
    }
}
