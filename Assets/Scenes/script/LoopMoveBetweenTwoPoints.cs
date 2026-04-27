using UnityEngine;

/// <summary>
/// 2つのポイント間で対象を移動し、到達したら開始ポイントへ瞬間的に戻して無限ループする。
/// </summary>
public class LoopMoveBetweenTwoPoints : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("移動させるオブジェクト。未設定ならこの GameObject が移動します。")]
    [SerializeField] Transform target;

    [Header("ポイント（空オブジェクトを2つ指定）")]
    [Tooltip("移動開始ポイント")]
    [SerializeField] Transform pointA;
    [Tooltip("到達ポイント")]
    [SerializeField] Transform pointB;

    [Header("移動設定")]
    [SerializeField] float moveSpeed = 3f;
    [Tooltip("開始時に target を pointA の位置へ合わせる")]
    [SerializeField] bool snapToPointAOnStart = true;
    [Tooltip("Time.timeScale の影響を受けずに動かす")]
    [SerializeField] bool useUnscaledTime = false;
    [Tooltip("この距離以内で到達判定する")]
    [SerializeField] float arriveDistance = 0.01f;

    void Reset()
    {
        target = transform;
    }

    void Awake()
    {
        if (target == null)
            target = transform;
    }

    void Start()
    {
        if (target == null || pointA == null || pointB == null)
            return;

        if (snapToPointAOnStart)
            target.position = pointA.position;
    }

    void Update()
    {
        if (target == null || pointA == null || pointB == null)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        float step = Mathf.Max(0f, moveSpeed) * dt;
        target.position = Vector3.MoveTowards(target.position, pointB.position, step);

        float threshold = Mathf.Max(0.0001f, arriveDistance);
        if ((target.position - pointB.position).sqrMagnitude <= threshold * threshold)
            target.position = pointA.position;
    }
}
