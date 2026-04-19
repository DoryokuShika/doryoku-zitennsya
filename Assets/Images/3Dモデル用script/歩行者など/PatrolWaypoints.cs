using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Waypoints を順に周回。到達したら待たずに次のポイントへ向けてすぐ走り出す。
/// NavMeshAgent 使用時は距離で到達判定（減速しきるまで待たない）。直線移動時も同様。
/// </summary>
public class PatrolWaypoints : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("上から順に通ります（Inspector で並べ替え可）")]
    [SerializeField] Transform[] waypoints;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float rotateSpeed = 360f;
    [Tooltip("この距離以内に入ったら到達とみなし、すぐ次のウェイポイントへ切り替え")]
    [SerializeField] float arrivalDistance = 0.4f;
    [SerializeField] bool loopRoute = true;
    [SerializeField] bool startPatrollingOnEnable = true;

    NavMeshAgent navAgent;
    Rigidbody rb;
    bool useNavMesh;
    bool useRigidbodyMove;
    int currentIndex;
    bool patrolling;
    bool navArrivalLatch;

    void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        useNavMesh = navAgent != null && navAgent.isActiveAndEnabled;
        useRigidbodyMove = rb != null && !rb.isKinematic;
        if (useNavMesh)
        {
            navAgent.speed = moveSpeed;
            navAgent.angularSpeed = rotateSpeed;
            navAgent.stoppingDistance = arrivalDistance;
            navAgent.updateRotation = true;
            navAgent.autoBraking = false;
        }
    }

    void OnEnable()
    {
        if (startPatrollingOnEnable)
            BeginPatrol();
    }

    void OnDisable()
    {
        StopPatrolInternal();
    }

    void Update()
    {
        if (MobTrafficPause.IsFrozen)
            return;

        if (!patrolling || waypoints == null || waypoints.Length == 0)
            return;

        Transform target = waypoints[currentIndex];
        if (target == null)
        {
            AdvanceIndex();
            GoToCurrentWaypoint();
            return;
        }

        if (useNavMesh)
            UpdateNavMesh(target);
        else
            UpdateDirectMove(target);
    }

    void UpdateNavMesh(Transform target)
    {
        if (!navAgent.pathPending && navAgent.hasPath &&
            navAgent.remainingDistance <= arrivalDistance)
        {
            if (!navArrivalLatch)
            {
                navArrivalLatch = true;
                OnReachedWaypoint();
            }
            return;
        }

        navArrivalLatch = false;
        navAgent.SetDestination(target.position);
    }

    void UpdateDirectMove(Transform target)
    {
        Vector3 p = transform.position;
        Vector3 flatTarget = new Vector3(target.position.x, p.y, target.position.z);
        Vector3 dir = flatTarget - p;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        Vector3 next = Vector3.MoveTowards(p, flatTarget, moveSpeed * Time.deltaTime);
        if (useRigidbodyMove)
            rb.MovePosition(next);
        else
            transform.position = next;

        if ((flatTarget - next).sqrMagnitude <= arrivalDistance * arrivalDistance)
            OnReachedWaypoint();
    }

    void OnReachedWaypoint()
    {
        AdvanceIndex();
        GoToCurrentWaypoint();
    }

    void AdvanceIndex()
    {
        currentIndex++;
        if (currentIndex >= waypoints.Length)
        {
            if (loopRoute)
                currentIndex = 0;
            else
            {
                currentIndex = waypoints.Length - 1;
                patrolling = false;
            }
        }
    }

    void GoToCurrentWaypoint()
    {
        if (!patrolling || waypoints == null || waypoints.Length == 0)
            return;
        if (currentIndex < 0 || currentIndex >= waypoints.Length)
            return;
        Transform t = waypoints[currentIndex];
        if (t == null)
            return;
        navArrivalLatch = false;
        FaceHorizontalToward(t.position);
        if (useNavMesh)
            navAgent.SetDestination(t.position);
    }

    void FaceHorizontalToward(Vector3 worldPos)
    {
        Vector3 p = transform.position;
        Vector3 flat = new Vector3(worldPos.x, p.y, worldPos.z);
        Vector3 dir = flat - p;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    /// <summary>周回を開始（外部から呼び出し可）</summary>
    public void BeginPatrol()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            patrolling = false;
            return;
        }
        patrolling = true;
        navArrivalLatch = false;
        currentIndex = 0;
        GoToCurrentWaypoint();
    }

    /// <summary>周回を停止</summary>
    public void StopPatrol()
    {
        StopPatrolInternal();
    }

    /// <summary>交通一時停止解除直後に呼ばれ、ナビの行き先を付け直します。</summary>
    public void NotifyResumeFromTrafficPause()
    {
        if (!patrolling || waypoints == null || waypoints.Length == 0)
            return;
        GoToCurrentWaypoint();
    }

    void StopPatrolInternal()
    {
        patrolling = false;
        if (useNavMesh && navAgent != null && navAgent.isActiveAndEnabled)
            navAgent.ResetPath();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.35f);
            int next = (i + 1) % waypoints.Length;
            if (waypoints[next] != null && (loopRoute || i < waypoints.Length - 1))
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        }
    }
#endif
}
