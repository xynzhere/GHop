using BepInEx;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.XR;

namespace ghop.Classes
{
    public class MovementSystem : MonoBehaviour
    {
        [SerializeField] public float accel = 150f;
        [SerializeField] public float airAccel = 50f;
        [SerializeField] public float maxSpeed = 2f;
        [SerializeField] public float maxAirSpeed = 0.2f;
        [SerializeField] public float friction = 1.5f;
        [SerializeField] public float jumpForce = 3.2f;
        [SerializeField] public GameObject camObj;

        private float lastJumpPress = -1f;
        private float jumpPressDuration = 0.1f;
        private float lastJumpTime;
        private float speedRamp = 0f;
        private bool onGround = false;

        private void Update()
        {
            if (UnityInput.Current.GetKey((KeyCode)32) || ControllerInputPoller.instance.rightControllerPrimaryButton)
                lastJumpPress = Time.time;
        }

        private void FixedUpdate()
        {
            Vector2 input = new Vector2(
                (UnityInput.Current.GetKey((KeyCode)97) ? -1f : 0f) + (UnityInput.Current.GetKey((KeyCode)100) ? 1f : 0f),
                (UnityInput.Current.GetKey((KeyCode)115) ? -1f : 0f) + (UnityInput.Current.GetKey((KeyCode)119) ? 1f : 0f)
            );

            Vector2 joystick = GetLeftJoystickAxis();
            Rigidbody rb = GetComponent<Rigidbody>();

            if (joystick.magnitude > 0.05f)
                input += joystick.normalized;

            Vector3 velocity = rb.linearVelocity;
            velocity = CalculateFriction(velocity);
            velocity += CalculateMovement(input, velocity);
            rb.linearVelocity = velocity;
        }

        private Vector3 CalculateFriction(Vector3 currentVelocity)
        {
            onGround = CheckGround();

            Vector3 flatVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            float magnitude = flatVel.magnitude;

            if (!onGround || UnityInput.Current.GetKey((KeyCode)32) ||
                ControllerInputPoller.instance.rightControllerPrimaryButton ||
                magnitude == 0f)
                return currentVelocity;

            float stopSpeed = maxSpeed * 0.3f;
            float control = magnitude < stopSpeed ? stopSpeed : magnitude;
            float drop = control * friction * Time.deltaTime;

            float newSpeed = Mathf.Max(magnitude - drop, 0f) / magnitude;

            return new Vector3(currentVelocity.x * newSpeed, currentVelocity.y, currentVelocity.z * newSpeed);
        }

        private Vector3 CalculateMovement(Vector2 input, Vector3 velocity)
        {
            onGround = CheckGround();
            float currentAccel = onGround ? accel : airAccel;
            float currentMaxSpeed = onGround ? maxSpeed : maxAirSpeed;

            if (onGround && UnityInput.Current.GetKey(KeyCode.LeftShift))
            {
                currentAccel *= 2f;
                currentMaxSpeed *= 2f;
            }

            if (input.magnitude < 0.01f)
            {
                speedRamp = Mathf.Lerp(speedRamp, 0f, Time.deltaTime * 5f);
                return GetJumpVelocity(velocity.y);
            }

            speedRamp = Mathf.Lerp(speedRamp, 1f, Time.deltaTime * 5f);

            Vector3 euler = new Vector3(0f, camObj.transform.rotation.eulerAngles.y, 0f);
            Vector3 wishDir = Quaternion.Euler(euler) * new Vector3(input.x, 0f, input.y);
            wishDir.Normalize();

            float currentSpeed = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDir);
            float addSpeed = currentMaxSpeed - currentSpeed;

            if (addSpeed <= 0f)
                return GetJumpVelocity(velocity.y);

            float accelSpeed = Mathf.Min(currentAccel * Time.deltaTime * speedRamp, addSpeed);

            return new Vector3(wishDir.x * accelSpeed, 0f, wishDir.z * accelSpeed) + GetJumpVelocity(velocity.y);
        }
        private Vector3 GetJumpVelocity(float yVelocity)
        {
            if (Time.time < lastJumpPress + jumpPressDuration && yVelocity < jumpForce && CheckGround())
            {
                if (Time.time > lastJumpTime)
                    lastJumpTime = Time.time + jumpPressDuration;

                lastJumpPress = -1f;
                return new Vector3(0f, jumpForce - yVelocity, 0f);
            }

            return Vector3.zero;
        }

        private bool CheckGround()
        {
            Ray ray = new Ray(GTPlayer.Instance.bodyCollider.transform.position, Vector3.down);
            Bounds bounds = GTPlayer.Instance.bodyCollider.bounds;
            return Physics.Raycast(ray, bounds.extents.y + 0.1f, (int)GTPlayer.Instance.locomotionEnabledLayers);
        }

        private Vector2 GetLeftJoystickAxis()
        {
            Vector2 result = Vector2.zero;
            var device = (UnityEngine.XR.InputDevice)ControllerInputPoller.instance.leftControllerDevice;
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out result);
            return result;
        }
    }
}