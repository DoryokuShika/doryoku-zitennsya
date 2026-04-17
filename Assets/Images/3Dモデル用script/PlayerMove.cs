using UnityEngine;

/// <summary>
/// Constant forward movement; mouse yaw changes direction. Hold S to brake and stop.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Target forward speed when not braking.")]
    public float ForwardSpeed = 8f;
    [Tooltip("Acceleration toward target speed (units per second).")]
    public float AccelerationPerSecond = 35f;
    [Tooltip("Deceleration while S is held (units per second).")]
    public float BrakePerSecond = 50f;

    [Header("Physics (optional)")]
    public RigidbodyInterpolation PositionInterpolation = RigidbodyInterpolation.Interpolate;
    public CollisionDetectionMode PhysicsCollisionMode = CollisionDetectionMode.Continuous;

    float yaw;
    float currentForwardSpeed;
    Rigidbody rb;

    [SerializeField]
    [Tooltip("Ignore mouse deltas for this many frames after start (avoids a jump when the cursor locks).")]
    int mouseIgnoreFramesAfterStart = 3;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = PositionInterpolation;
        rb.collisionDetectionMode = PhysicsCollisionMode;
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;
        if (yaw > 180f) yaw -= 360f;

        ApplyBodyRotation();
        currentForwardSpeed = ForwardSpeed;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb.freezeRotation = true;
    }

    void Update()
    {
        if (Time.frameCount > mouseIgnoreFramesAfterStart)
            MouseLook();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void FixedUpdate()
    {
        MoveForwardAndBrake();
    }

    void MouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float dx = Input.GetAxis("Mouse X");
        yaw += dx;
        ApplyBodyRotation();
    }

    void ApplyBodyRotation()
    {
        transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up);
    }

    void MoveForwardAndBrake()
    {
        float target = Input.GetKey(KeyCode.S) ? 0f : ForwardSpeed;
        float rate = Input.GetKey(KeyCode.S) ? BrakePerSecond : AccelerationPerSecond;
        currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, target, rate * Time.fixedDeltaTime);

        Vector3 forwardDir = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
        Vector3 horizontal = forwardDir * currentForwardSpeed;
        rb.velocity = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
    }
}
