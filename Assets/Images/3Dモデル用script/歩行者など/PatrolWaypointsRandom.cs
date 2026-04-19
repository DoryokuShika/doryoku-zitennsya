using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 登録したウェイポイントの集合から、到着のたびにランダムで次の行き先を選んで移動します（PatrolWaypoints の順番周回版と別コンポーネント）。
/// NavMeshAgent があればナビ、無ければ直線移動。到達後は待たずに次へ。
/// </summary>
public class PatrolWaypointsRandom : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("この中から毎回ランダムで次の目的地を選びます（null は無視）")]
    [SerializeField] Transform[] waypoints;

    [Tooltip("ON のとき、直前と同じインデックスは避けようとします（候補が1つだけなら同じになり得ます）")]
    [SerializeField] bool avoidSameConsecutive = true;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float rotateSpeed = 360f;
    [Tooltip("この距離以内に入ったら到達とみなし、すぐ次のランダムポイントへ")]
    [SerializeField] float arrivalDistance = 0.4f;
    [SerializeField] bool startPatrollingOnEnable = true;

    NavMeshAgent navAgent;
    Rigidbody rb;
    bool useNavMesh;
    bool useRigidbodyMove;
    int currentIndex = -1;
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

        if (currentIndex < 0 || currentIndex >= waypoints.Length)
        {
            PickNextWaypointAndGo(allowSameAsCurrent: true);
            return;
        }

        Transform target = waypoints[currentIndex];
        if (target == null)
        {
            PickNextWaypointAndGo(allowSameAsCurrent: true);
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
        PickNextWaypointAndGo(allowSameAsCurrent: false);
    }

    void PickNextWaypointAndGo(bool allowSameAsCurrent)
    {
        int idx = ChooseRandomIndex(allowSameAsCurrent);
        if (idx < 0)
        {
            patrolling = false;
            return;
        }
        currentIndex = idx;
        GoToCurrentWaypoint();
    }

    int ChooseRandomIndex(bool allowSameAsCurrent)
    {
        if (waypoints == null || waypoints.Length == 0)
            return -1;

        int validCount = 0;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
                validCount++;
        }
        if (validCount == 0)
            return -1;

        bool tryAvoid = avoidSameConsecutive && !allowSameAsCurrent && waypoints.Length > 1 && validCount > 1;

        for (int attempt = 0; attempt < 64; attempt++)
        {
            int i = Random.Range(0, waypoints.Length);
            if (waypoints[i] == null)
                continue;
            if (tryAvoid && i == currentIndex)
                continue;
            return i;
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
                return i;
        }
        return -1;
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
        PickNextWaypointAndGo(allowSameAsCurrent: true);
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
        if (currentIndex < 0 || currentIndex >= waypoints.Length || waypoints[currentIndex] == null)
            PickNextWaypointAndGo(true);
        else
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
        Gizmos.color = Color.magenta;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.35f);
        }
    }
#endif
}
