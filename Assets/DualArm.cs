using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class DualArm : MonoBehaviour
{
    [Header("Controllers and HMD")]
    public Transform rightController;
    public Transform leftController;
    public Transform head;

    [Tooltip("Optional: a transform that represents the player's BODY yaw (e.g. OVRCameraRig/TrackingSpace). If set, arm rest pose follows this yaw instead of the head yaw.")]
    public Transform bodyYawRoot;

    [Header("Arm Components")]
    public Transform leftHand;
    public Transform rightHand;
    public Transform leftElbow;
    public Transform rightElbow;
    public Transform robotSholderLeft;
    public Transform robotSholderRight;

    [Header("Calculated Positions")]
    public Transform supposed, supposedRight;
    private Transform reachingFor, reachingForRight;

    [Header("Resting Positions")]
    public Transform restingLeft;
    public Transform restingRight;

    [Header("Arm Offsets (Yaw-only space)")]
    [Tooltip("Shoulder offset from head in (right, up, forward) using yaw-only basis. X is lateral (mirrored between arms), Y and Z are shared.")]
    public Vector3 shoulderOffset = new Vector3(0.25f, -0.30f, -0.15f);

    [Tooltip("Elbow offset from head in (right, up, forward) using yaw-only basis. X is lateral (mirrored between arms), Y and Z are shared.")]
    public Vector3 elbowOffset = new Vector3(0.45f, -0.55f, 0.10f);

    [Header("Away Mode")]
    [Tooltip("When enabled, the arm base position is shifted laterally away from the user's actual position.")]
    public bool awayModeEnabled = false;
    public float awayLateralOffset = 2f;

    // Each arm has independent automation state
    private bool overtakeLeft, overtakeRight;

    void Start()
    {
        overtakeLeft = false;
        overtakeRight = false;
    }

    void Update()
    {
        UpdateRobotArmPositions();
        UpdateHandPositions();
        UpdateSupposedPositions();
    }

    private Transform GetYawReference()
    {
        // If a body yaw root is provided, use it so arms don't "rotate in place" when the head turns.
        // Otherwise fall back to head (previous behavior).
        return bodyYawRoot != null ? bodyYawRoot : head;
    }

    private void GetYawOnlyBasis(out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        var yawRef = GetYawReference();

        up = Vector3.up;
        forward = Vector3.ProjectOnPlane(yawRef.forward, up);
        if (forward.sqrMagnitude < 1e-6f)
        {
            // If yawRef.forward is nearly vertical, fall back to yawRef.up projected.
            forward = Vector3.ProjectOnPlane(yawRef.up, up);
        }
        forward.Normalize();
        right = Vector3.Cross(up, forward).normalized;
    }

    private Vector3 YawSpaceOffsetToWorld(Vector3 yawSpaceOffset, Vector3 right, Vector3 up, Vector3 forward)
    {
        // yawSpaceOffset is (right, up, forward)
        return yawSpaceOffset.x * right + yawSpaceOffset.y * up + yawSpaceOffset.z * forward;
    }

    private Quaternion GetYawOnlyRotation()
    {
        var yawRef = GetYawReference();
        Vector3 fwd = Vector3.ProjectOnPlane(yawRef.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f)
        {
            fwd = Vector3.ProjectOnPlane(yawRef.up, Vector3.up);
        }
        fwd.Normalize();
        return Quaternion.LookRotation(fwd, Vector3.up);
    }

    private void UpdateRobotArmPositions()
    {
        GetYawOnlyBasis(out var fwd, out var right, out var up);
        Quaternion yawRot = GetYawOnlyRotation();
        Vector3 basePosition = head.position;
        if (awayModeEnabled)
            basePosition += right * awayLateralOffset;

        // Shoulders: mirror only the lateral component (X)
        var shoulderWorld = YawSpaceOffsetToWorld(shoulderOffset, right, up, fwd);
        var shoulderWorldL = YawSpaceOffsetToWorld(new Vector3(-shoulderOffset.x, shoulderOffset.y, shoulderOffset.z), right, up, fwd);
        robotSholderRight.position = basePosition + shoulderWorld;
        robotSholderLeft.position = basePosition + shoulderWorldL;

        // Elbows: mirror only the lateral component (X)
        var elbowWorld = YawSpaceOffsetToWorld(elbowOffset, right, up, fwd);
        var elbowWorldL = YawSpaceOffsetToWorld(new Vector3(-elbowOffset.x, elbowOffset.y, elbowOffset.z), right, up, fwd);
        rightElbow.position = basePosition + elbowWorld;
        leftElbow.position = basePosition + elbowWorldL;

        // Set yaw-only rotation for all arm targets
        robotSholderRight.rotation = yawRot;
        robotSholderLeft.rotation = yawRot;
        rightElbow.rotation = yawRot;
        leftElbow.rotation = yawRot;
    }

    private void UpdateHandPositions()
    {
        leftHand.position = leftController.position;
        rightHand.position = rightController.position;
    }

    private void UpdateSupposedPositions()
    {
        // LEFT arm
        if (!overtakeLeft)
        {
            if (restingLeft != null)
            {
                supposed.position = restingLeft.position;
                supposed.rotation = restingLeft.rotation;
            }
        }
        else
        {
            if (reachingFor != null)
            {
                supposed.position = reachingFor.position;
                supposed.rotation = reachingFor.rotation;
            }
        }

        // RIGHT arm
        if (!overtakeRight)
        {
            if (restingRight != null)
            {
                supposedRight.position = restingRight.position;
                supposedRight.rotation = restingRight.rotation;
            }
        }
        else
        {
            if (reachingForRight != null)
            {
                supposedRight.position = reachingForRight.position;
                supposedRight.rotation = reachingForRight.rotation;
            }
        }
    }

    public void updateTarget(Transform target)
    {
        reachingFor = target;
    }

    public void updateTarget(Transform target, bool isRight)
    {
        if (isRight)
        {
            reachingForRight = target;
        }
        else
        {
            reachingFor = target;
        }
    }

    // `left` param kept for compatibility with Experiment1
    public void switchControl(bool enabled, bool left)
    {
        if (left)
        {
            overtakeLeft = enabled;
            if (!enabled)
            {
                reachingFor = null;
            }
        }
        else
        {
            overtakeRight = enabled;
            if (!enabled)
            {
                reachingForRight = null;
            }
        }
    }

    public bool IsLeftOvertaking => overtakeLeft;
    public bool IsRightOvertaking => overtakeRight;

    public void SetAwayMode(bool enabled)
    {
        awayModeEnabled = enabled;
    }

    public void Resart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
