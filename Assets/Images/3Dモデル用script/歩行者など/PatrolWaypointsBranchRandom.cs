using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 各地点（ノード）ごとに「次に行ける候補」をインスペクターで設定し、
/// その地点に到着するたびに候補からランダムで次の行き先を選びます。
/// PatrolWaypoints（順番）／PatrolWaypointsRandom（プール一括ランダム）とは別コンポーネントです。
/// </summary>
public class PatrolWaypointsBranchRandom : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("各地点と、その地点に着いたあとの次候補")]
    [SerializeField] PatrolWaypointNode[] nodes;

    [Tooltip("最初に向かう nodes のインデックス")]
    [SerializeField] int startNodeIndex = 0;

    [Tooltip("次候補が空のとき、Start の地点へ戻って続行する")]
    [SerializeField] bool restartFromStartWhenNoNext = true;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float rotateSpeed = 360f;
    [Tooltip("この距離以内に入ったら到達とみなし、次候補から選んで即移動")]
    [SerializeField] float arrivalDistance = 0.4f;
    [SerializeField] bool startPatrollingOnEnable = true;

    NavMeshAgent navAgent;
    Rigidbody rb;
    bool useNavMesh;
    bool useRigidbodyMove;
    Transform currentTarget;
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

        if (!patrolling || nodes == null || nodes.Length == 0)
            return;

        if (currentTarget == null)
        {
            TryRestartFromStartOrStop();
            return;
        }

        if (useNavMesh)
            UpdateNavMesh(currentTarget);
        else
            UpdateDirectMove(currentTarget);
    }

    void UpdateNavMesh(Transform target)
    {
        if (!navAgent.pathPending && navAgent.hasPath &&
            navAgent.remainingDistance <= arrivalDistance)
        {
            if (!navArrivalLatch)
            {
                navArrivalLatch = true;
                OnReachedDestination();
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
            OnReachedDestination();
    }

    void OnReachedDestination()
    {
        PatrolWaypointNode arrivedNode = FindNodeByPoint(currentTarget);
        if (arrivedNode == null)
        {
            TryRestartFromStartOrStop();
            return;
        }

        Transform next = PickRandomNonNull(arrivedNode.nextCandidates);
        if (next == null)
        {
            if (restartFromStartWhenNoNext)
                TryRestartFromStartOrStop();
            else
                StopPatrolInternal();
            return;
        }

        GoToTarget(next);
    }

    PatrolWaypointNode FindNodeByPoint(Transform point)
    {
        if (nodes == null || point == null)
            return null;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].point == point)
                return nodes[i];
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

    void GoToTarget(Transform t)
    {
        if (t == null || !patrolling)
            return;
        currentTarget = t;
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

    void TryRestartFromStartOrStop()
    {
        if (nodes == null || nodes.Length == 0)
        {
            StopPatrolInternal();
            return;
        }
        int start = Mathf.Clamp(startNodeIndex, 0, nodes.Length - 1);
        if (nodes[start].point == null)
        {
            StopPatrolInternal();
            return;
        }
        GoToTarget(nodes[start].point);
    }

    /// <summary>周回を開始（外部から呼び出し可）</summary>
    public void BeginPatrol()
    {
        if (nodes == null || nodes.Length == 0)
        {
            patrolling = false;
            return;
        }
        int start = Mathf.Clamp(startNodeIndex, 0, nodes.Length - 1);
        if (nodes[start].point == null)
        {
            patrolling = false;
            return;
        }
        patrolling = true;
        navArrivalLatch = false;
        GoToTarget(nodes[start].point);
    }

    /// <summary>周回を停止</summary>
    public void StopPatrol()
    {
        StopPatrolInternal();
    }

    /// <summary>交通一時停止解除直後に呼ばれ、ナビの行き先を付け直します。</summary>
    public void NotifyResumeFromTrafficPause()
    {
        if (!patrolling)
            return;
        if (currentTarget != null)
            GoToTarget(currentTarget);
        else
            TryRestartFromStartOrStop();
    }

    void StopPatrolInternal()
    {
        patrolling = false;
        currentTarget = null;
        if (useNavMesh && navAgent != null && navAgent.isActiveAndEnabled)
            navAgent.ResetPath();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (nodes == null || nodes.Length == 0)
            return;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].point == null)
                continue;
            Vector3 a = nodes[i].point.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(a, 0.35f);
            if (nodes[i].nextCandidates == null)
                continue;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.85f);
            for (int j = 0; j < nodes[i].nextCandidates.Length; j++)
            {
                if (nodes[i].nextCandidates[j] == null)
                    continue;
                Gizmos.DrawLine(a, nodes[i].nextCandidates[j].position);
            }
        }
    }
#endif
}

[System.Serializable]
public class PatrolWaypointNode
{
    [Tooltip("この地点へ向かい、到着したら Next Candidates から次を選びます")]
    public Transform point;

    [Tooltip("この地点に着いたあと、次に向かう候補（個数自由。null は無視）")]
    public Transform[] nextCandidates;
}
