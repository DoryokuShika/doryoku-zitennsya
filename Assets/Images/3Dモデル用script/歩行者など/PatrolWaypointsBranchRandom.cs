using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 各地点（ノード）ごとに「次に行ける候補」をインスペクターで設定し、
/// その地点に到着するたびに候補からランダムで次の行き先を選びます。
/// PatrolWaypoints（順番）／PatrolWaypointsRandom（プール一括ランダム）とは別コンポーネントです。
/// 任意: 自転車が近くにいる状態でマウスホイール操作をしたときに一定時間非表示（当たり・移動停止）し、
/// 復帰時に自転車が至近にいれば Walker 衝突と同様にゲームオーバー。
/// 警察視界内で発動したら、歩道・逆走と同じ流れで <see cref="ViolationFineAmountDisplay"/> と
/// <see cref="PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice"/>（種別 PedestrianBell）を呼びます。
/// </summary>
[DefaultExecutionOrder(25)]
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

    [Header("Bell hide (mouse wheel + bicycle proximity)")]
    [Tooltip("ベル隠れ（近接＋ホイール）機能を使う")]
    [SerializeField] bool enablePedestrianBellHide = true;

    [Tooltip("未設定なら Tag Player のオブジェクトの Transform を使用")]
    [SerializeField] Transform playerBicycle;

    [Tooltip("この歩行者位置から自転車までがこの距離以内なら「近い」とみなす（m）")]
    [SerializeField] float bellProximityRadiusMeters = 6f;

    [Tooltip("ON: XZ のみ。OFF: 3D 距離")]
    [SerializeField] bool bellProximityHorizontalOnly = true;

    [Tooltip("Input.GetAxis(\"Mouse ScrollWheel\") の絶対値がこの以上なら「ホイールが回った」")]
    [SerializeField] float mouseScrollTriggerAbs = 0.02f;

    [Tooltip("非表示・停止する秒数（リアルタイム）")]
    [SerializeField] float hideSeconds = 10f;

    [Tooltip("復帰直後、自転車がこの距離以内（XZ）なら Walker 衝突と同じゲームオーバー")]
    [SerializeField] float gameOverIfBicycleWithinMetersAfterReappear = 2.5f;

    [Tooltip("見た目だけ消す場合のルート。未指定ならこのオブジェクト以下の Renderer")]
    [SerializeField] Transform visualRootForHide;

    [Tooltip("オン: ベル退避が成立したときシーンの PedestrianBellObjectiveUi に残り人数を報告します。")]
    [SerializeField] bool reportBellDismissalToObjectiveUi = true;

    [Header("Bell hide / police catch (same as WrongWay / sidewalk)")]
    [Tooltip("警察に見えたベル退避の反則金。空白なら PoliceLineOfSightCatch の Catch Pedestrian Bell Fine Amount を使います。非空白のときは捕獲 UI の再適用にも優先されます。")]
    [SerializeField] string bellPoliceFineAmountText = "";

    [SerializeField] TMP_Text bellFineAmountDisplayTmp;
    [SerializeField] Text bellFineAmountDisplayUi;
    [SerializeField] TMP_Text[] bellAdditionalFineAmountTmp;
    [SerializeField] Text[] bellAdditionalFineAmountUi;

    NavMeshAgent navAgent;
    Rigidbody rb;
    bool useNavMesh;
    bool useRigidbodyMove;
    Transform currentTarget;
    bool patrolling;
    bool navArrivalLatch;
    Animator _animator;

    bool _bellHideRoutineActive;
    Coroutine _bellHideCoroutine;
    bool _bellAppliedVisualHide;

    Vector3 _bellSavedPos;
    Quaternion _bellSavedRot;
    Transform _bellSavedTarget;
    bool _bellWasPatrolling;

    Collider[] _cachedColliders;
    Renderer[] _cachedRenderers;
    readonly List<bool> _colliderPrevEnabled = new List<bool>();
    readonly List<bool> _rendererPrevEnabled = new List<bool>();

    bool _rbWasKinematic;
    bool _savedRbUseGravity;
    RigidbodyConstraints _savedRbConstraints;

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

        _animator = GetComponentInChildren<Animator>(true);

        if (Application.isPlaying && enablePedestrianBellHide && playerBicycle == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerBicycle = p.transform;
        }
    }

    void OnValidate()
    {
        bellProximityRadiusMeters = Mathf.Max(0.05f, bellProximityRadiusMeters);
        mouseScrollTriggerAbs = Mathf.Max(0.001f, mouseScrollTriggerAbs);
        hideSeconds = Mathf.Max(0f, hideSeconds);
        gameOverIfBicycleWithinMetersAfterReappear = Mathf.Max(0.05f, gameOverIfBicycleWithinMetersAfterReappear);
    }

    void OnEnable()
    {
        if (startPatrollingOnEnable)
            BeginPatrol();
    }

    void OnDisable()
    {
        if (_bellHideCoroutine != null)
        {
            StopCoroutine(_bellHideCoroutine);
            _bellHideCoroutine = null;
        }

        if (_bellHideRoutineActive)
        {
            RestoreBellHidePhysicsAndVisualsOnly();
            _bellHideRoutineActive = false;
        }

        StopPatrolInternal();
    }

    void Update()
    {
        if (_bellHideRoutineActive)
            return;

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

    void LateUpdate()
    {
        if (!enablePedestrianBellHide || _bellHideRoutineActive || MobTrafficPause.IsFrozen)
            return;

        TryStartPedestrianBellHideFromInput();
    }

    void TryStartPedestrianBellHideFromInput()
    {
        if (!patrolling || currentTarget == null)
            return;
        if (playerBicycle == null)
            return;
        if (!IsTransformWithinRadius(transform, playerBicycle, bellProximityRadiusMeters, bellProximityHorizontalOnly))
            return;

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) < mouseScrollTriggerAbs)
            return;

        _bellHideCoroutine = StartCoroutine(BellHideRoutine());
    }

    static bool IsTransformWithinRadius(Transform origin, Transform target, float radiusMeters, bool horizontalOnly)
    {
        if (origin == null || target == null)
            return false;
        Vector3 a = origin.position;
        Vector3 b = target.position;
        float distSq;
        if (horizontalOnly)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            distSq = dx * dx + dz * dz;
        }
        else
        {
            distSq = (a - b).sqrMagnitude;
        }

        float r = radiusMeters;
        return distSq <= r * r;
    }

    IEnumerator BellHideRoutine()
    {
        _bellHideRoutineActive = true;

        if (reportBellDismissalToObjectiveUi)
            PedestrianBellObjectiveUi.NotifyBellDismissed();

        _bellSavedPos = transform.position;
        _bellSavedRot = transform.rotation;
        _bellSavedTarget = currentTarget;
        _bellWasPatrolling = patrolling;

        StopPatrolInternal();

        if (useNavMesh && navAgent != null)
        {
            navAgent.ResetPath();
            navAgent.enabled = false;
        }

        if (rb != null)
        {
            _rbWasKinematic = rb.isKinematic;
            _savedRbUseGravity = rb.useGravity;
            _savedRbConstraints = rb.constraints;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (_animator != null)
            _animator.speed = 0f;

        EnsureVisualCaches();
        ApplyVisualAndColliderHidden(true);
        _bellAppliedVisualHide = true;

        if (PoliceLineOfSightState.IsTargetInPoliceSightNow)
        {
            PoliceLineOfSightCatch.NotifyPedestrianBellFineFromPatrolForNextCatch(bellPoliceFineAmountText);
            if (!string.IsNullOrWhiteSpace(bellPoliceFineAmountText))
            {
                ViolationFineAmountDisplay.SetFineText(
                    bellPoliceFineAmountText,
                    bellFineAmountDisplayTmp,
                    bellFineAmountDisplayUi,
                    bellAdditionalFineAmountTmp,
                    bellAdditionalFineAmountUi);
            }

            PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind.PedestrianBell);
        }

        yield return new WaitForSecondsRealtime(hideSeconds);

        if (playerBicycle != null &&
            IsTransformWithinRadiusAtPosition(
                _bellSavedPos,
                playerBicycle,
                gameOverIfBicycleWithinMetersAfterReappear,
                horizontalOnly: true))
        {
            RestoreBellHidePhysicsAndVisualsOnly();
            _bellHideRoutineActive = false;
            _bellHideCoroutine = null;
            Gemeover.TriggerWalkerCollisionGameOver();
            yield break;
        }

        RestoreBellHidePhysicsAndVisualsOnly();
        ResumePatrolAfterBellSnapshot();
        _bellHideRoutineActive = false;
        _bellHideCoroutine = null;
    }

    void RestoreBellHidePhysicsAndVisualsOnly()
    {
        transform.SetPositionAndRotation(_bellSavedPos, _bellSavedRot);

        if (useNavMesh && navAgent != null)
        {
            navAgent.enabled = true;
            if (navAgent.isOnNavMesh)
                navAgent.Warp(_bellSavedPos);
        }

        if (rb != null)
        {
            rb.isKinematic = _rbWasKinematic;
            rb.useGravity = _savedRbUseGravity;
            rb.constraints = _savedRbConstraints;
        }

        if (_animator != null)
            _animator.speed = 1f;

        if (_bellAppliedVisualHide)
        {
            ApplyVisualAndColliderHidden(false);
            _bellAppliedVisualHide = false;
        }
    }

    void ResumePatrolAfterBellSnapshot()
    {
        if (_bellWasPatrolling && nodes != null && nodes.Length > 0)
        {
            patrolling = true;
            navArrivalLatch = false;
            if (_bellSavedTarget != null)
                GoToTarget(_bellSavedTarget);
            else
                TryRestartFromStartOrStop();
        }
    }

    static bool IsTransformWithinRadiusAtPosition(Vector3 worldPos, Transform target, float radiusMeters, bool horizontalOnly)
    {
        if (target == null)
            return false;
        Vector3 b = target.position;
        float distSq;
        if (horizontalOnly)
        {
            float dx = worldPos.x - b.x;
            float dz = worldPos.z - b.z;
            distSq = dx * dx + dz * dz;
        }
        else
        {
            distSq = (worldPos - b).sqrMagnitude;
        }

        float r = radiusMeters;
        return distSq <= r * r;
    }

    void EnsureVisualCaches()
    {
        if (_cachedColliders != null && _cachedRenderers != null)
            return;

        Transform root = visualRootForHide != null ? visualRootForHide : transform;
        _cachedColliders = root.GetComponentsInChildren<Collider>(true);
        _cachedRenderers = root.GetComponentsInChildren<Renderer>(true);
    }

    void ApplyVisualAndColliderHidden(bool hidden)
    {
        EnsureVisualCaches();

        if (hidden)
        {
            _colliderPrevEnabled.Clear();
            if (_cachedColliders != null)
            {
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    Collider c = _cachedColliders[i];
                    if (c == null)
                        continue;
                    _colliderPrevEnabled.Add(c.enabled);
                    c.enabled = false;
                }
            }

            _rendererPrevEnabled.Clear();
            if (_cachedRenderers != null)
            {
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    Renderer r = _cachedRenderers[i];
                    if (r == null)
                        continue;
                    _rendererPrevEnabled.Add(r.enabled);
                    r.enabled = false;
                }
            }
        }
        else
        {
            if (_cachedColliders != null)
            {
                int k = 0;
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    Collider c = _cachedColliders[i];
                    if (c == null)
                        continue;
                    if (k < _colliderPrevEnabled.Count)
                        c.enabled = _colliderPrevEnabled[k++];
                }
            }

            if (_cachedRenderers != null)
            {
                int k = 0;
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    Renderer r = _cachedRenderers[i];
                    if (r == null)
                        continue;
                    if (k < _rendererPrevEnabled.Count)
                        r.enabled = _rendererPrevEnabled[k++];
                }
            }
        }
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
        if (_bellHideRoutineActive)
            return;

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

        if (!enablePedestrianBellHide)
            return;
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, bellProximityRadiusMeters);
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, gameOverIfBicycleWithinMetersAfterReappear);
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
