using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Sequential: waypoints を上から順に周回（従来どおり）。
/// RandomBranches: 各ノードに「次の候補 Transform 配列」を設定し、到着・待機後にその中からランダムで次へ。移動は直線（NavMesh ならエージェント、無ければ MoveTowards）。
/// </summary>
public class PatrolWaypoints : MonoBehaviour
{
    public enum PatrolRouteMode
    {
        Sequential,
        RandomBranches
    }

    [Header("Route")]
    [SerializeField] PatrolRouteMode routeMode = PatrolRouteMode.Sequential;

    [Header("Sequential: ordered waypoints")]
    [Tooltip("Mode = Sequential のとき。上から順に通ります。")]
    [SerializeField] Transform[] waypoints;

    [Header("Random branches: per-point next choices")]
    [Tooltip("Mode = RandomBranches のとき。各要素の point に着いたら randomNext からランダムで次を選びます（個数は自由）。")]
    [SerializeField] WaypointBranch[] branchGraph;
    [Tooltip("最初に向かう branchGraph のインデックス")]
    [SerializeField] int randomStartIndex = 0;

    [Header("Movement")]
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
    Transform currentRandomTarget;
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
        if (!patrolling)
            return;

        if (routeMode == PatrolRouteMode.Sequential)
        {
            if (waypoints == null || waypoints.Length == 0)
                return;
        }
        else
        {
            if (branchGraph == null || branchGraph.Length == 0 || currentRandomTarget == null)
                return;
        }

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                if (routeMode == PatrolRouteMode.Sequential)
                {
                    AdvanceIndex();
                    GoToTarget(GetSequentialTarget());
                }
                else
                    PickRandomNextAndGo();
            }
            return;
        }

        Transform target = routeMode == PatrolRouteMode.Sequential ? waypoints[currentIndex] : currentRandomTarget;
        if (target == null)
        {
            if (routeMode == PatrolRouteMode.Sequential)
            {
                AdvanceIndex();
                GoToTarget(GetSequentialTarget());
            }
            else
                PickRandomNextAndGo();
            return;
        }

        if (useNavMesh)
            UpdateNavMesh(target);
        else
            UpdateDirectMove(target);
    }

    Transform GetSequentialTarget()
    {
        if (waypoints == null || currentIndex < 0 || currentIndex >= waypoints.Length)
            return null;
        return waypoints[currentIndex];
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
            if (routeMode == PatrolRouteMode.Sequential)
            {
                AdvanceIndex();
                GoToTarget(GetSequentialTarget());
            }
            else
                PickRandomNextAndGo();
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

    void GoToTarget(Transform t)
    {
        if (!patrolling || t == null)
            return;
        navArrivalLatch = false;
        if (useNavMesh)
            navAgent.SetDestination(t.position);
    }

    WaypointBranch FindBranch(Transform point)
    {
        if (branchGraph == null || point == null)
            return null;
        for (int i = 0; i < branchGraph.Length; i++)
        {
            if (branchGraph[i].point == point)
                return branchGraph[i];
        }
        return null;
    }

    static Transform PickRandomNonNull(Transform[] options)
    {
        if (options == null || options.Length == 0)
            return null;
        int count = 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
                count++;
        }
        if (count == 0)
            return null;
        int r = Random.Range(0, count);
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null)
                continue;
            if (r == 0)
                return options[i];
            r--;
        }
        return null;
    }

    void PickRandomNextAndGo()
    {
        Transform arrived = currentRandomTarget;
        WaypointBranch branch = FindBranch(arrived);
        if (branch == null)
        {
            StopPatrolInternal();
            return;
        }

        Transform next = PickRandomNonNull(branch.randomNext);
        if (next == null)
        {
            if (loopRoute && branchGraph != null && branchGraph.Length > 0)
            {
                int start = Mathf.Clamp(randomStartIndex, 0, branchGraph.Length - 1);
                if (branchGraph[start].point != null)
                {
                    currentRandomTarget = branchGraph[start].point;
                    GoToTarget(currentRandomTarget);
                    return;
                }
            }
            StopPatrolInternal();
            return;
        }

        currentRandomTarget = next;
        GoToTarget(currentRandomTarget);
    }

    /// <summary>周回を開始（外部から呼び出し可）</summary>
    public void BeginPatrol()
    {
        patrolling = true;
        waiting = false;
        navArrivalLatch = false;

        if (routeMode == PatrolRouteMode.Sequential)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                patrolling = false;
                return;
            }
            currentIndex = 0;
            GoToTarget(GetSequentialTarget());
            return;
        }

        if (branchGraph == null || branchGraph.Length == 0)
        {
            patrolling = false;
            return;
        }
        int start = Mathf.Clamp(randomStartIndex, 0, branchGraph.Length - 1);
        if (branchGraph[start].point == null)
        {
            patrolling = false;
            return;
        }
        currentRandomTarget = branchGraph[start].point;
        GoToTarget(currentRandomTarget);
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
        if (routeMode == PatrolRouteMode.Sequential)
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
            return;
        }

        if (branchGraph == null || branchGraph.Length == 0)
            return;
        for (int i = 0; i < branchGraph.Length; i++)
        {
            if (branchGraph[i].point == null)
                continue;
            Vector3 a = branchGraph[i].point.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(a, 0.35f);
            if (branchGraph[i].randomNext == null)
                continue;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            for (int j = 0; j < branchGraph[i].randomNext.Length; j++)
            {
                if (branchGraph[i].randomNext[j] == null)
                    continue;
                Gizmos.DrawLine(a, branchGraph[i].randomNext[j].position);
            }
        }
    }
#endif
}

[System.Serializable]
public class WaypointBranch
{
    [Tooltip("この Transform に到着したあと、randomNext から次を選びます")]
    public Transform point;
    [Tooltip("次に行く候補（個数は自由。null は無視。空なら周回停止、loop 時は Random Start の point に戻ります）")]
    public Transform[] randomNext;
}
