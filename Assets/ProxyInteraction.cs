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
    [SerializeField] private GameObject saltContainer;

    [Header("Areas")]
    [SerializeField] private Collider panArea;
    [SerializeField] private Collider waterDenyArea;
    [SerializeField] private Collider saltDenyArea;

    private Collider waterCupCollider;
    private Collider saltContainerCollider;

    private bool waterDenied;
    private bool saltDenied;
    private bool waterInPan;
    private bool saltInPan;
    private bool waterInDeny;
    private bool saltInDeny;

    private void Awake()
    {
        if (waterCup != null)
        {
            waterCupCollider = waterCup.GetComponentInChildren<Collider>();
        }
        if (saltContainer != null)
        {
            saltContainerCollider = saltContainer.GetComponentInChildren<Collider>();
        }
    }

    private void Update()
    {
        UpdatePromptVisibility();
        CheckOverlapTransitions();
    }

    private void UpdatePromptVisibility()
    {
        bool waterThresholdReached = waterSlider != null && waterSlider.value >= notifyThreshold;
        if (!waterThresholdReached)
        {
            waterDenied = false;
        }
        bool showWater = waterThresholdReached && !waterDenied;
        SetActiveIfNotNull(waterNotifyCanvas, showWater);
        SetActiveIfNotNull(waterCup, showWater);

        bool saltThresholdReached = saltSlider != null && saltSlider.value >= notifyThreshold;
        if (!saltThresholdReached)
        {
            saltDenied = false;
        }
        bool showSalt = saltThresholdReached && !saltDenied;
        SetActiveIfNotNull(saltNotifyCanvas, showSalt);
        SetActiveIfNotNull(saltContainer, showSalt);
    }

    private void CheckOverlapTransitions()
    {
        if (waterCupCollider != null)
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

        if (saltContainerCollider != null)
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
    }

    private void OnWaterAccepted()
    {
        if (experiment != null)
        {
            experiment.TriggerWaterOneShot();
        }
    }

    private void OnSaltAccepted()
    {
        if (experiment != null)
        {
            experiment.TriggerSaltOneShot();
        }
    }

    private void OnWaterDenied()
    {
        waterDenied = true;
        UpdatePromptVisibility();
    }

    private void OnSaltDenied()
    {
        saltDenied = true;
        UpdatePromptVisibility();
    }

    private static bool IsOverlapping(Collider item, Collider area)
    {
        if (item == null || area == null) return false;
        return item.bounds.Intersects(area.bounds);
    }

    private static void SetActiveIfNotNull(GameObject obj, bool active)
    {
        if (obj != null && obj.activeSelf != active)
        {
            obj.SetActive(active);
        }
    }
}
