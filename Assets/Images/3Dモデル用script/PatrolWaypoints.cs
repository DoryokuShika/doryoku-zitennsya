using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 空の GameObject をシーン上に置き、ここに Transform を順番に割り当てるとその順で周回します。
/// キャラに付け、Waypoints の要素順が通る順番です（Inspector でドラッグして並べ替え可能）。
/// </summary>
public class PatrolWaypoints : MonoBehaviour
{
    [Header("ルート（上から順に通ります）")]
    [Tooltip("待機／集会ポイント。空の GameObject の Transform をドラッグして並べる")]
    [SerializeField] Transform[] waypoints;

    [Header("移動")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float rotateSpeed = 360f;
    [SerializeField] float arrivalDistance = 0.4f;
    [Tooltip("各ポイントで止まる秒数（0 で即次へ）")]
    [SerializeField] float waitSecondsPerPoint = 2f;
    [SerializeField] bool loopRoute = true;
    [SerializeField] bool startPatrollingOnEnable = true;

    NavMeshAgent navAgent;
    Rigidbody rb;
    bool useNavMesh;
    bool useRigidbodyMove;
    int currentIndex;
    float waitTimer;
    bool waiting;
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
        if (!patrolling || waypoints == null || waypoints.Length == 0)
            return;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                AdvanceIndex();
                GoToCurrentWaypoint();
            }
            return;
        }

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
            navAgent.remainingDistance <= arrivalDistance &&
            navAgent.velocity.sqrMagnitude < 0.01f)
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
        Vector3 next = Vector3.MoveTowards(p, flatTarget, moveSpeed * Time.deltaTime);
        if (useRigidbodyMove)
            rb.MovePosition(next);
        else
            transform.position = next;

        Vector3 dir = flatTarget - p;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotateSpeed * Time.deltaTime);
        }

        if ((flatTarget - next).sqrMagnitude <= arrivalDistance * arrivalDistance)
            OnReachedWaypoint();
    }

    void OnReachedWaypoint()
    {
        if (waitSecondsPerPoint > 0f)
        {
            waiting = true;
            waitTimer = waitSecondsPerPoint;
            if (useNavMesh)
                navAgent.ResetPath();
        }
        else
        {
            AdvanceIndex();
            GoToCurrentWaypoint();
        }
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
        if (useNavMesh)
            navAgent.SetDestination(t.position);
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
        waiting = false;
        navArrivalLatch = false;
        currentIndex = 0;
        GoToCurrentWaypoint();
    }

    /// <summary>周回を停止</summary>
    public void StopPatrol()
    {
        StopPatrolInternal();
    }

    void StopPatrolInternal()
    {
        patrolling = false;
        waiting = false;
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
