using UnityEngine;

public class ResetOutMap : MonoBehaviour
{
    [Header("マップ外判定")]
    [SerializeField] private float MinZ = -10f;
    [SerializeField] private float MaxZ = 10f;
    [SerializeField] private float MinX = -10f;
    [SerializeField] private float MaxX = 10f;
    [SerializeField] private float MinY = -10f;
    [SerializeField] private float MaxY = 10f;
    [SerializeField] private float resetHeight;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Vector3 startposition;
    private Quaternion startrotation;
    private Rigidbody rb;
    private PlayerMove playerMove;

    // 復帰直後の数フレームは再判定をスキップ。
    // これを入れないと、物理同期が遅れて IsOutOfMap() が true のまま連続呼び出しされ、
    // 毎フレーム velocity が 0 に戻されて「重力で落ちない・前進しない」状態になる。
    private int skipCheckFrames;
    private const int SkipFramesAfterReset = 3;

    void Start()
    {
        startposition = transform.position;
        startrotation = transform.rotation;
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInChildren<Rigidbody>(true);
        playerMove = GetComponent<PlayerMove>();
        if (playerMove == null)
            playerMove = GetComponentInChildren<PlayerMove>(true);
    }

    void Update()
    {
        if (skipCheckFrames > 0)
        {
            skipCheckFrames--;
            return;
        }

        if (IsOutOfMap())
        {
            if (debugLog)
                Debug.Log($"[ResetOutMap] Out of Map! pos={transform.position}. Resetting.");
            ResetPosition();
        }
    }

    bool IsOutOfMap()
    {
        Vector3 pos = transform.position;
        return pos.x < MinX || pos.x > MaxX ||
                pos.y < MinY || pos.y > MaxY ||
                pos.z < MinZ || pos.z > MaxZ;
    }

    void ResetPosition()
    {
        // 警告UIで停止中に落下復帰した場合、先に交通停止を解除しないと移動不能のままになる。
        if (MobTrafficPause.IsFrozen)
            MobTrafficPause.UnfreezeCarAndWalkerMobs();

        Vector3 target = startposition;
        if (resetHeight != 0f)
            target.y = resetHeight;

        if (rb != null)
        {
            // まず Rigidbody を健全な状態に強制復帰する。
            // freeze が外れていない等、あらゆる「動かない」要因をここで打ち消す。
            rb.isKinematic = false;
            rb.useGravity = true;
            // 位置は自由・回転は固定（元の運用に合わせる）。
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // transform.position 直書きだと物理側との同期がズレることがあるので rb.position を使う。
            rb.position = target;
            rb.rotation = startrotation;

            // すぐに物理側を同期。次の FixedUpdate まで古い位置で判定が走るのを防ぐ。
            Physics.SyncTransforms();
        }
        else
        {
            transform.position = target;
            transform.rotation = startrotation;
        }

        // PlayerMove の内部キャッシュ（yaw, currentForwardSpeed）をリセット。
        // これをしないと、復帰直後 currentForwardSpeed=0 のまま、かつ yaw が落下前の向きのままになり、
        // 挙動が不自然になる。
        if (playerMove != null)
            playerMove.ResetInternalStateFromTransform();

        // 次フレームの IsOutOfMap() 再発を防ぐクールダウン。
        skipCheckFrames = SkipFramesAfterReset;

        if (debugLog)
            Debug.Log($"[ResetOutMap] Reset done. pos={transform.position} isKinematic={(rb != null ? rb.isKinematic.ToString() : "no rb")} useGravity={(rb != null ? rb.useGravity.ToString() : "no rb")} constraints={(rb != null ? rb.constraints.ToString() : "no rb")}");
    }

    // エディタ／シーンビューで範囲を描画
    void OnDrawGizmosSelected()
    {
        float centerX = (MinX + MaxX) / 2f;
        float centerY = (MinY + MaxY) / 2f;
        float centerZ = (MinZ + MaxZ) / 2f;
        Vector3 center = new Vector3(centerX, centerY, centerZ);

        float sizeX = MaxX - MinX;
        float sizeY = MaxY - MinY;
        float sizeZ = MaxZ - MinZ;
        Vector3 size = new Vector3(sizeX, sizeY, sizeZ);

        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}
