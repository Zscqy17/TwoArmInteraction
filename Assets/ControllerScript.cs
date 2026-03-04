using UnityEngine;

public class ControllerScript : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 0.1f;

    void Update()
    {
        // Left joystick: X axis (left/right) and Z axis (forward/back)
        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // Right joystick: Y axis (up/down)
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        Vector3 movement = new Vector3(leftStick.x, rightStick.y, leftStick.y);
        transform.position += movement * moveSpeed * Time.deltaTime;
    }
}
