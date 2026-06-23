using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One menu entry: an id, a name tag, the layer it belongs to, and the (external) Transform that
/// represents it. The menu positions <see cref="visual"/> into its fan slot; it does not create the
/// visual itself, so existing 3D grabbables can be reused as proxy items.
/// </summary>
[Serializable]
public class ProxyMenuItem
{
    public string id;
    public string nameTag;
    public int layer = 1;
    public Transform visual;
}

/// <summary>
/// A generated second-layer action marker (e.g. Apply / Discard). The owner of the menu polls
/// <see cref="collider"/> against the held item to decide when an action is committed.
/// </summary>
public class ProxyMenuActionMarker
{
    public string actionId;
    public string label;
    public Transform transform;
    public Collider collider;
    public ProxyMenuItemView view;
}

/// <summary>
/// Drives a referenced Transform toward a target pose. The Transform may be an external object
/// (a reused grabbable) or this component's own GameObject (a generated action marker).
/// While driven, a Rigidbody on the visual is held kinematic so it parks in the fan instead of
/// falling; a real grab can take over by calling <see cref="SetTransformDriven"/>(false).
/// </summary>
public class ProxyMenuItemView : MonoBehaviour
{
    [SerializeField] private float poseLerpSpeed = 9f;

    private ProxyMenu owner;
    private Transform visual;
    private Rigidbody body;
    private bool originalKinematic;

    private bool driveTransform;
    private bool controlScale;
    private float baseScale = 1f;

    private Vector3 targetPosition;
    private Quaternion targetRotation = Quaternion.identity;
    private float targetScaleMultiplier = 1f;

    public bool IsMarker { get; private set; }
    public string ActionId { get; private set; }

    /// <summary>Initialize as a first-layer item that positions an external visual transform.</summary>
    public void InitializeItem(ProxyMenu menu, Transform externalVisual)
    {
        owner = menu;
        visual = externalVisual;
        IsMarker = false;
        controlScale = false;
        body = visual != null ? visual.GetComponent<Rigidbody>() : null;
        if (body != null) originalKinematic = body.isKinematic;
    }

    /// <summary>Initialize as a generated action marker that positions its own GameObject.</summary>
    public void InitializeMarker(ProxyMenu menu, string actionId, float worldScale)
    {
        owner = menu;
        visual = transform;
        IsMarker = true;
        ActionId = actionId;
        controlScale = true;
        baseScale = worldScale;
    }

    public void SetTarget(Vector3 position, Quaternion rotation, float scaleMultiplier)
    {
        targetPosition = position;
        targetRotation = rotation;
        targetScaleMultiplier = scaleMultiplier;
    }

    public void Snap(Vector3 position)
    {
        if (visual != null) visual.position = position;
    }

    /// <summary>When false, the menu stops moving the visual (a real grab is controlling it).</summary>
    public void SetTransformDriven(bool driven)
    {
        driveTransform = driven;
        if (body != null)
        {
            body.isKinematic = driven ? true : originalKinematic;
        }
    }

    private void Update()
    {
        if (!driveTransform || visual == null) return;

        float t = Time.deltaTime * poseLerpSpeed;
        visual.position = Vector3.Lerp(visual.position, targetPosition, t);
        visual.rotation = Quaternion.Slerp(visual.rotation, targetRotation, t);
        if (controlScale)
        {
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * baseScale * targetScaleMultiplier, t);
        }
    }
}

/// <summary>
/// Hand-anchored fan menu of proxy items.
///
/// - Items are registered via <see cref="RegisterItem"/> and toggled into the visible first layer via
///   <see cref="SetItemActive"/> (so the active set can change mid-trial).
/// - The active first layer is arranged in a 120 deg fan above the hand; the panel lags behind the
///   hand so it stays in reach without snapping around.
/// - When a first-layer item is grabbed (<see cref="NotifyGrabbed"/>), the rest spread to the fan
///   edges and that item's action markers appear in a wider 200 deg fan above it. The owner polls
///   <see cref="CurrentActions"/> to detect which action the held item is brought to.
/// </summary>
public class ProxyMenu : MonoBehaviour
{
    [Header("Anchors")]
    [Tooltip("Hand the menu follows. Falls back to this transform if unset.")]
    [SerializeField] private Transform hand;
    [Tooltip("Used so items face the user. Falls back to Camera.main.")]
    [SerializeField] private Transform head;

    [Header("Panel Placement")]
    [SerializeField] private float aboveHandDistance = 0.15f;
    [Tooltip("Seconds-scale lag of the panel behind the hand.")]
    [SerializeField] private float followSmoothTime = 1.2f;

    [Header("Fan Geometry")]
    [SerializeField] private float firstLayerFanDegrees = 120f;
    [SerializeField] private float firstLayerRadius = 0.18f;
    [SerializeField] private float secondLayerFanDegrees = 200f;
    [SerializeField] private float secondLayerRadius = 0.16f;
    [SerializeField] private float secondLayerAboveOffset = 0.06f;
    [Tooltip("Angular step used to stack non-grabbed items at the fan edges.")]
    [SerializeField] private float edgeStackStepDegrees = 9f;

    [Header("Action Markers")]
    [SerializeField] private float actionMarkerScale = 0.06f;

    private class MenuEntry
    {
        public ProxyMenuItem item;
        public ProxyMenuItemView view;
        public bool active;
        public readonly List<ProxyMenuActionMarker> actions = new List<ProxyMenuActionMarker>();
    }

    private readonly List<MenuEntry> entries = new List<MenuEntry>();
    private readonly Dictionary<string, MenuEntry> entriesById = new Dictionary<string, MenuEntry>();

    private MenuEntry grabbedEntry;
    private Vector3 followedHandPosition;
    private Vector3 followVelocity;
    private bool followInitialized;

    private static readonly ProxyMenuActionMarker[] NoActions = Array.Empty<ProxyMenuActionMarker>();

    // ── Public API ───────────────────────────────────────────────────────────

    public string GrabbedItemId => grabbedEntry != null ? grabbedEntry.item.id : null;

    public IReadOnlyList<ProxyMenuActionMarker> CurrentActions =>
        grabbedEntry != null ? grabbedEntry.actions : (IReadOnlyList<ProxyMenuActionMarker>)NoActions;

    public void SetHand(Transform handTransform) { hand = handTransform; }

    public void SetHead(Transform headTransform) { head = headTransform; }

    public ProxyMenuItemView RegisterItem(ProxyMenuItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return null;
        if (entriesById.TryGetValue(item.id, out MenuEntry existing)) return existing.view;

        GameObject controller = new GameObject("ProxyMenuItemCtrl_" + item.id);
        controller.transform.SetParent(transform, false);

        ProxyMenuItemView view = controller.AddComponent<ProxyMenuItemView>();
        view.InitializeItem(this, item.visual);

        MenuEntry entry = new MenuEntry { item = item, view = view, active = false };
        entries.Add(entry);
        entriesById[item.id] = entry;
        return view;
    }

    public void AddAction(string parentId, string actionId, string label, Color color)
    {
        if (!entriesById.TryGetValue(parentId, out MenuEntry entry)) return;

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        markerObject.name = "ProxyMenuAction_" + parentId + "_" + actionId;
        markerObject.transform.SetParent(transform, false);

        MeshCollider meshCollider = markerObject.GetComponent<MeshCollider>();
        if (meshCollider != null) Destroy(meshCollider);

        BoxCollider boxCollider = markerObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector3(1f, 1f, 0.2f);

        MeshRenderer markerRenderer = markerObject.GetComponent<MeshRenderer>();
        Material material = new Material(Shader.Find("Sprites/Default")) { color = color };
        markerRenderer.sharedMaterial = material;

        markerObject.transform.localScale = Vector3.one * actionMarkerScale;

        ProxyMenuItemView view = markerObject.AddComponent<ProxyMenuItemView>();
        view.InitializeMarker(this, actionId, actionMarkerScale);

        AddLabel(markerObject.transform, label);
        markerObject.SetActive(false);

        entry.actions.Add(new ProxyMenuActionMarker
        {
            actionId = actionId,
            label = label,
            transform = markerObject.transform,
            collider = boxCollider,
            view = view
        });
    }

    /// <summary>Toggle whether an item is part of the visible first layer.</summary>
    public void SetItemActive(string id, bool active)
    {
        if (!entriesById.TryGetValue(id, out MenuEntry entry)) return;
        if (entry.active == active) return;
        entry.active = active;

        if (active)
        {
            if (!followInitialized)
            {
                followedHandPosition = GetHandPosition();
                followVelocity = Vector3.zero;
                followInitialized = true;
            }
            entry.view.SetTransformDriven(true);
            entry.view.Snap(SpawnPoint());
        }
        else
        {
            if (grabbedEntry == entry) ClearGrabbed();
            entry.view.SetTransformDriven(false);
        }
    }

    /// <summary>Called by the owner when an item's visual is grabbed by the user.</summary>
    public void NotifyGrabbed(string id)
    {
        if (!entriesById.TryGetValue(id, out MenuEntry entry) || !entry.active) return;
        if (grabbedEntry == entry) return;

        grabbedEntry = entry;
        entry.view.SetTransformDriven(false); // the real grab now controls the visual

        Vector3 spawn = entry.item.visual != null ? entry.item.visual.position : SpawnPoint();
        foreach (ProxyMenuActionMarker marker in entry.actions)
        {
            marker.view.gameObject.SetActive(true);
            marker.view.SetTransformDriven(true);
            marker.view.Snap(spawn);
        }
    }

    /// <summary>Called by the owner when the grabbed item's visual is released.</summary>
    public void NotifyReleased(string id)
    {
        if (grabbedEntry == null || grabbedEntry.item.id != id) return;

        MenuEntry entry = grabbedEntry;
        HideActions(entry);
        grabbedEntry = null;
        if (entry.active) entry.view.SetTransformDriven(true); // ease back into the fan
    }

    /// <summary>Hook for a hand-pose detector: re-centers the lagging panel on the hand.</summary>
    public void OnHandPoseDetected()
    {
        followedHandPosition = GetHandPosition();
        followInitialized = true;
    }

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!AnyActive()) return;

        Vector3 target = GetHandPosition();
        if (!followInitialized)
        {
            followedHandPosition = target;
            followInitialized = true;
        }
        followedHandPosition = Vector3.SmoothDamp(followedHandPosition, target, ref followVelocity, Mathf.Max(0.0001f, followSmoothTime));
    }

    private void LateUpdate()
    {
        if (!AnyActive()) return;

        GetBasis(out Vector3 pivot, out Vector3 right, out Vector3 up, out Vector3 headPos);
        List<MenuEntry> activeEntries = GetActiveEntries();

        if (grabbedEntry != null && grabbedEntry.active)
        {
            LayoutFanToEdges(activeEntries, grabbedEntry, pivot, right, up, headPos);

            Vector3 grabbedPos = grabbedEntry.item.visual != null ? grabbedEntry.item.visual.position : pivot;
            Vector3 secondPivot = grabbedPos + up * secondLayerAboveOffset;
            LayoutActions(grabbedEntry.actions, secondPivot, right, up, headPos);
        }
        else
        {
            LayoutFanCentered(activeEntries, pivot, right, up, headPos);
        }
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void LayoutFanCentered(List<MenuEntry> activeEntries, Vector3 pivot, Vector3 right, Vector3 up, Vector3 headPos)
    {
        int count = activeEntries.Count;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angleDeg = Mathf.Lerp(-firstLayerFanDegrees * 0.5f, firstLayerFanDegrees * 0.5f, t);
            Vector3 position = FanPosition(pivot, angleDeg, firstLayerRadius, right, up);
            activeEntries[i].view.SetTarget(position, FaceUser(position, headPos), 1f);
        }
    }

    private void LayoutFanToEdges(List<MenuEntry> activeEntries, MenuEntry grabbed, Vector3 pivot, Vector3 right, Vector3 up, Vector3 headPos)
    {
        int grabbedIndex = activeEntries.IndexOf(grabbed);
        int leftStack = 0;
        int rightStack = 0;

        for (int i = 0; i < activeEntries.Count; i++)
        {
            MenuEntry entry = activeEntries[i];
            if (entry == grabbed) continue; // grabbed visual is controlled by the real grab

            bool left = i < grabbedIndex;
            float edgeAngle = left ? -firstLayerFanDegrees * 0.5f : firstLayerFanDegrees * 0.5f;
            int stackIndex = left ? leftStack++ : rightStack++;
            float angleDeg = edgeAngle + (left ? 1f : -1f) * stackIndex * edgeStackStepDegrees;

            Vector3 position = FanPosition(pivot, angleDeg, firstLayerRadius, right, up);
            entry.view.SetTarget(position, FaceUser(position, headPos), 1f);
        }
    }

    private void LayoutActions(List<ProxyMenuActionMarker> actions, Vector3 pivot, Vector3 right, Vector3 up, Vector3 headPos)
    {
        int count = actions.Count;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angleDeg = Mathf.Lerp(-secondLayerFanDegrees * 0.5f, secondLayerFanDegrees * 0.5f, t);
            Vector3 position = FanPosition(pivot, angleDeg, secondLayerRadius, right, up);
            actions[i].view.SetTarget(position, FaceUser(position, headPos), 1f);
        }
    }

    private static Vector3 FanPosition(Vector3 pivot, float angleDegrees, float radius, Vector3 right, Vector3 up)
    {
        float angleRad = angleDegrees * Mathf.Deg2Rad;
        Vector3 direction = Mathf.Sin(angleRad) * right + Mathf.Cos(angleRad) * up;
        return pivot + direction * radius;
    }

    private static Quaternion FaceUser(Vector3 position, Vector3 headPos)
    {
        Vector3 toItem = position - headPos;
        if (toItem.sqrMagnitude < 1e-6f) return Quaternion.identity;
        return Quaternion.LookRotation(toItem.normalized, Vector3.up);
    }

    private void GetBasis(out Vector3 pivot, out Vector3 right, out Vector3 up, out Vector3 headPos)
    {
        pivot = SpawnPoint();
        up = Vector3.up;

        headPos = head != null
            ? head.position
            : Camera.main != null ? Camera.main.transform.position : pivot + Vector3.back;

        Vector3 forward = pivot - headPos;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        right = Vector3.Cross(up, forward).normalized;
    }

    private Vector3 SpawnPoint()
    {
        Vector3 basePos = followInitialized ? followedHandPosition : GetHandPosition();
        return basePos + Vector3.up * aboveHandDistance;
    }

    private Vector3 GetHandPosition()
    {
        return hand != null ? hand.position : transform.position;
    }

    // ── Entry helpers ──────────────────────────────────────────────────────────

    private bool AnyActive()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].active) return true;
        }
        return false;
    }

    private List<MenuEntry> GetActiveEntries()
    {
        List<MenuEntry> result = new List<MenuEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].active) result.Add(entries[i]);
        }
        return result;
    }

    private void ClearGrabbed()
    {
        if (grabbedEntry == null) return;
        HideActions(grabbedEntry);
        grabbedEntry = null;
    }

    private static void HideActions(MenuEntry entry)
    {
        foreach (ProxyMenuActionMarker marker in entry.actions)
        {
            marker.view.SetTransformDriven(false);
            marker.view.gameObject.SetActive(false);
        }
    }

    private static void AddLabel(Transform parent, string label)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, -0.7f, -0.02f);

        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = 0.02f;
        text.color = Color.white;
    }
}
