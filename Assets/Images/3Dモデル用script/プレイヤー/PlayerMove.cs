using UnityEngine;

/// <summary>
/// Constant forward movement; mouse yaw changes direction.
/// ブレーキ: カーソルロック中にマウス左または右を押している間（従来の S キーは任意）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Target forward speed when not braking.")]
    public float ForwardSpeed = 8f;
    [Tooltip("Acceleration toward target speed (units per second).")]
    public float AccelerationPerSecond = 35f;
    [Tooltip("Deceleration while braking (units per second).")]
    public float BrakePerSecond = 50f;

    [Tooltip("オン: カーソルロック中、左または右クリックを押している間ブレーキ。オフ: S キーのみでブレーキ（従来）。")]
    [SerializeField] bool brakeWhileMouseButtonHeld = true;

    [Tooltip("マウスブレーキ ON のとき、追加で S キーでもブレーキにする")]
    [SerializeField] bool allowSKeyBrakeInAdditionToMouse;

    [Header("Physics (optional)")]
    public RigidbodyInterpolation PositionInterpolation = RigidbodyInterpolation.Interpolate;
    public CollisionDetectionMode PhysicsCollisionMode = CollisionDetectionMode.Continuous;

    [Header("ATARI collision SFX")]
    [Tooltip("ATARI タグのオブジェクトに接触したとき効果音を鳴らす")]
    [SerializeField] bool playAtariCollisionSfx = true;
    [Tooltip("未指定ならこのオブジェクトの AudioSource を使用")]
    [SerializeField] AudioSource atariSfxSource;
    [SerializeField] AudioClip atariCollisionClip;
    [Range(0f, 1f)]
    [SerializeField] float atariCollisionVolume = 1f;
    [Tooltip("連続接触で鳴りすぎないようにする最小間隔（秒）")]
    [SerializeField] float atariSfxCooldownSeconds = 0.08f;

    float yaw;
    float currentForwardSpeed;
    Rigidbody rb;
    float _nextAtariSfxTime;

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

        if (atariSfxSource == null)
            atariSfxSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (MobTrafficPause.IsFrozen)
        {
            currentForwardSpeed = 0f;
            FullyStopRigidbody();
            return;
        }

        if (Time.frameCount > mouseIgnoreFramesAfterStart)
            MouseLook();

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (MobTrafficPause.IsFrozen)
            FullyStopRigidbody();
    }

    void FixedUpdate()
    {
        if (MobTrafficPause.IsFrozen)
        {
            currentForwardSpeed = 0f;
            FullyStopRigidbody();
            return;
        }

        MoveForwardAndBrake();
    }

    void FullyStopRigidbody()
    {
        if (rb == null)
            return;
        if (rb.isKinematic)
            return;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// 外部（例: 落下復帰処理）から内部状態をリセットしたいときに呼ぶ。
    /// yaw を現在の transform から取り直し、前進速度を 0 にクリアする。
    /// </summary>
    public void ResetInternalStateFromTransform()
    {
        yaw = transform.eulerAngles.y;
        if (yaw > 180f) yaw -= 360f;
        currentForwardSpeed = 0f;
        ApplyBodyRotation();
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

    bool IsBrakingNow()
    {
        if (MobTrafficPause.IsFrozen)
            return false;

        if (brakeWhileMouseButtonHeld)
        {
            if (Cursor.lockState == CursorLockMode.Locked &&
                (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
                return true;
            if (allowSKeyBrakeInAdditionToMouse && Input.GetKey(KeyCode.S))
                return true;
            return false;
        }

        return Input.GetKey(KeyCode.S);
    }

    void MoveForwardAndBrake()
    {
        bool brake = IsBrakingNow();
        float target = brake ? 0f : ForwardSpeed;
        float rate = brake ? BrakePerSecond : AccelerationPerSecond;
        currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, target, rate * Time.fixedDeltaTime);

        Vector3 forwardDir = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
        Vector3 horizontal = forwardDir * currentForwardSpeed;
        rb.velocity = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!playAtariCollisionSfx)
            return;
        if (collision == null || collision.gameObject == null || !collision.gameObject.CompareTag("ATARI"))
            return;
        if (Time.unscaledTime < _nextAtariSfxTime)
            return;
        if (atariSfxSource == null || atariCollisionClip == null)
            return;

        atariSfxSource.PlayOneShot(atariCollisionClip, atariCollisionVolume);
        _nextAtariSfxTime = Time.unscaledTime + Mathf.Max(0f, atariSfxCooldownSeconds);
    }
}
