using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 水平速度から進行方向（東西南北）を更新し、<c>douro</c> タグの道路コライダに触れている間は
/// 道路の向き（東西／南北）と進行方向から「左側通行の帯」にいるかを推定します。
/// グリッド状の軸平行道路を想定しています（コライダの bounds で道路の長手／短手を判定）。
/// 道路は <c>Is Trigger</c> のとき <c>OnTriggerEnter</c>、通常コライダのとき <c>OnCollisionEnter</c> のどちらでも検知します。
/// 道路オブジェクトのコライダに <c>douro</c> タグが付いている必要があります。
/// </summary>
[DisallowMultipleComponent]
public class PlayerRoadTravelState : MonoBehaviour
{
    public enum Cardinal4
    {
        None = 0,
        North = 1,
        East = 2,
        South = 3,
        West = 4,
    }

    /// <summary>日本の左側通行として、その道路・進行方向で「左寄りの半分」にいるか。</summary>
    public enum LeftSideEstimate
    {
        Unknown = 0,
        NotOnRoad = 1,
        SpeedTooLow = 2,
        /// <summary>道路の左帯側と推定（左側通行として望ましい側）。</summary>
        LikelyCorrectLeft = 3,
        /// <summary>道路の右帯側と推定。</summary>
        LikelyWrongRight = 4,
    }

    [Header("Tags")]
    [SerializeField] string roadTag = "douro";

    [Header("Direction")]
    [Tooltip("この速度未満 (m/s) では進行方向を更新せず、前回値を維持します。")]
    [SerializeField] float minSpeedForDirection = 0.35f;
    [Tooltip("min(|x|,|z|)/max(|x|,|z|) がこの値より大きいと「斜め」とみなし DominantCardinal は None。大きいほど斜め扱いになりにくくチラつき減。")]
    [SerializeField] float diagonalCardinalRatio = 0.72f;
    [Tooltip("オン: 速度の角度・dirXZ はそのまま。North↔South と East↔West を入れ替え。")]
    [FormerlySerializedAs("swapDominantEastWestOnly")]
    [FormerlySerializedAs("invertCardinal180")]
    [SerializeField] bool swapDominantCardinalOpposite;
    [Tooltip("0 より大きいとき、進行角度・DominantCardinal の更新をこの秒間隔でのみ行う。0 以下で毎フレーム更新。")]
    [SerializeField] float directionCardinalUpdateIntervalSeconds = 0.5f;

    [Header("Lane (left-side traffic)")]
    [SerializeField] float laneCenterTolerance = 0.08f;

    [Header("Debug")]
    [SerializeField] bool debugLog;
    [Tooltip("オン: 毎フレームログ。オフ: 方角(Cardinal)か左側推定が変わったときだけ。")]
    [SerializeField] bool debugLogEveryFrame;

    static readonly Vector3 XAxis = Vector3.right;
    static readonly Vector3 ZAxis = Vector3.forward;

    Rigidbody _rb;
    readonly HashSet<Collider> _touchingRoads = new HashSet<Collider>();

    Cardinal4 _dbgPrevCardinal;
    LeftSideEstimate _dbgPrevLeft;
    bool _dbgHasPrev;
    float _nextCardinalSampleTime = float.NegativeInfinity;

    /// <summary>XZ 上の進行方向（単位ベクトル）。速度が小さいときは直前の有効値を保持。間隔設定時はその間は更新されない。</summary>
    public Vector2 TravelDirectionXZ { get; private set; }

    /// <summary>TravelDirectionXZ から決めた四方向。斜めは None。速度不足時は更新されず前回値。間隔設定時はその間は更新されない。</summary>
    public Cardinal4 DominantCardinal { get; private set; }

    public bool IsOnRoad => _touchingRoads.Count > 0;
    public Collider PrimaryRoadCollider { get; private set; }
    public LeftSideEstimate LeftSide { get; private set; }
    public float SignedLateralFromRoadCenter { get; private set; }

    /// <summary>指定コライダ（道路）に現在接触中か。</summary>
    public bool IsTouchingRoad(Collider road) => road != null && _touchingRoads.Contains(road);

    /// <summary>
    /// 道路 AABB の長手が東西なら横オフセットは Z、南北なら X（<see cref="SignedLateralFromRoadCenter"/> と同じ定義）。
    /// 東西道路: プラスは世界 +Z 側の半分。南北道路: プラスは世界 +X 側の半分。
    /// </summary>
    public static bool TryGetSignedLateralAlongRoad(Collider road, Vector3 worldPosition, out float signedLateral, out bool eastWestRoad)
    {
        signedLateral = 0f;
        eastWestRoad = false;
        if (road == null)
            return false;

        Bounds b = road.bounds;
        eastWestRoad = b.extents.x >= b.extents.z;
        if (eastWestRoad)
            signedLateral = worldPosition.z - b.center.z;
        else
            signedLateral = worldPosition.x - b.center.x;

        return true;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        if (MobTrafficPause.IsFrozen)
            return;

        RefreshTravelDirection();
        PickPrimaryRoadAndLane();
        MaybeDebugLogFacing();
    }

    /// <summary>マップ平面の方角ラベル（North=上, South=下, East=右, West=左）。</summary>
    public static string CardinalToRoseJa(Cardinal4 c)
    {
        return c switch
        {
            Cardinal4.North => "上 (+Z)",
            Cardinal4.South => "下 (-Z)",
            Cardinal4.East => "右 (+X)",
            Cardinal4.West => "左 (-X)",
            _ => "斜め／なし",
        };
    }

    void OnTriggerEnter(Collider other) => TryRegisterRoadCollider(other);

    void OnTriggerExit(Collider other) => TryUnregisterRoadCollider(other);

    void OnCollisionEnter(Collision collision)
    {
        int n = collision.contactCount;
        if (n > 0)
        {
            for (int i = 0; i < n; i++)
                TryRegisterRoadCollider(collision.GetContact(i).otherCollider);
        }
        else if (collision.collider != null)
            TryRegisterRoadCollider(collision.collider);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null)
            TryUnregisterRoadCollider(collision.collider);
    }

    void TryRegisterRoadCollider(Collider other)
    {
        if (roadTag.Length > 0 && other != null && other.CompareTag(roadTag))
            _touchingRoads.Add(other);
    }

    void TryUnregisterRoadCollider(Collider other)
    {
        if (roadTag.Length > 0 && other != null && other.CompareTag(roadTag))
            _touchingRoads.Remove(other);
    }

    void RefreshTravelDirection()
    {
        if (directionCardinalUpdateIntervalSeconds > 0f && Time.time < _nextCardinalSampleTime)
            return;

        Vector3 v = Vector3.zero;
        if (_rb != null)
        {
            v = _rb.velocity;
            v.y = 0f;
        }

        if (v.sqrMagnitude < minSpeedForDirection * minSpeedForDirection)
            return;

        if (directionCardinalUpdateIntervalSeconds > 0f)
            _nextCardinalSampleTime = Time.time + directionCardinalUpdateIntervalSeconds;

        Vector3 n = v.normalized;
        TravelDirectionXZ = new Vector2(n.x, n.z);
        DominantCardinal = ClassifyCardinal(TravelDirectionXZ, diagonalCardinalRatio);
        if (swapDominantCardinalOpposite)
            DominantCardinal = SwapDominantCardinalOpposite(DominantCardinal);
    }

    static Cardinal4 SwapDominantCardinalOpposite(Cardinal4 c)
    {
        return c switch
        {
            Cardinal4.North => Cardinal4.South,
            Cardinal4.South => Cardinal4.North,
            Cardinal4.East => Cardinal4.West,
            Cardinal4.West => Cardinal4.East,
            _ => c,
        };
    }

    static Cardinal4 ClassifyCardinal(Vector2 xz, float diagonalRatioMax)
    {
        float ax = Mathf.Abs(xz.x);
        float az = Mathf.Abs(xz.y);
        float mx = Mathf.Max(ax, az);
        if (mx < 1e-5f)
            return Cardinal4.None;

        float mn = Mathf.Min(ax, az);
        if (mn / mx > diagonalRatioMax)
            return Cardinal4.None;

        return ax >= az
            ? (xz.x >= 0f ? Cardinal4.East : Cardinal4.West)
            : (xz.y >= 0f ? Cardinal4.North : Cardinal4.South);
    }

    void PickPrimaryRoadAndLane()
    {
        PurgeDestroyed(_touchingRoads);

        if (_touchingRoads.Count == 0)
        {
            PrimaryRoadCollider = null;
            LeftSide = LeftSideEstimate.NotOnRoad;
            SignedLateralFromRoadCenter = 0f;
            return;
        }

        Vector3 vel = _rb != null ? Flatten(_rb.velocity) : Vector3.zero;
        if (vel.sqrMagnitude < minSpeedForDirection * minSpeedForDirection)
        {
            PrimaryRoadCollider = GetAnyRoad();
            LeftSide = LeftSideEstimate.SpeedTooLow;
            SignedLateralFromRoadCenter = 0f;
            return;
        }

        PrimaryRoadCollider = ChooseBestRoadCollider(_touchingRoads, vel);
        if (PrimaryRoadCollider == null)
        {
            LeftSide = LeftSideEstimate.Unknown;
            return;
        }

        if (!TryGetSignedLateralAlongRoad(PrimaryRoadCollider, transform.position, out float lateral, out bool eastWestRoad))
        {
            LeftSide = LeftSideEstimate.Unknown;
            return;
        }

        SignedLateralFromRoadCenter = lateral;
        Vector3 longAxis = eastWestRoad ? XAxis : ZAxis;
        float longSign = Mathf.Sign(Vector3.Dot(vel.normalized, longAxis));
        if (Mathf.Abs(longSign) < 0.15f)
        {
            LeftSide = LeftSideEstimate.Unknown;
            return;
        }

        bool correctLeft;
        if (eastWestRoad)
        {
            // 長手が X。東向き (+X) の左は -Z 側 → lateral が負。西向きは逆。
            correctLeft = longSign > 0f ? lateral <= laneCenterTolerance : lateral >= -laneCenterTolerance;
        }
        else
        {
            // 長手が Z。北向き (+Z) の左は +X 側。lateral = x - cx
            correctLeft = longSign > 0f ? lateral >= -laneCenterTolerance : lateral <= laneCenterTolerance;
        }

        LeftSide = correctLeft ? LeftSideEstimate.LikelyCorrectLeft : LeftSideEstimate.LikelyWrongRight;
    }

    static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    Collider GetAnyRoad()
    {
        foreach (var c in _touchingRoads)
        {
            if (c != null)
                return c;
        }
        return null;
    }

    /// <summary>速度ベクトルと道路の長手方向の一致が大きいコライダを優先（交差点の重なり用）。</summary>
    static Collider ChooseBestRoadCollider(HashSet<Collider> roads, Vector3 flatVelocity)
    {
        Collider best = null;
        float bestScore = -1f;
        Vector3 vn = flatVelocity.normalized;

        foreach (var c in roads)
        {
            if (c == null)
                continue;
            Bounds b = c.bounds;
            bool eastWest = b.extents.x >= b.extents.z;
            Vector3 longAxis = eastWest ? XAxis : ZAxis;
            float score = Mathf.Abs(Vector3.Dot(vn, longAxis));
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    static void PurgeDestroyed(HashSet<Collider> set)
    {
        if (set.Count == 0)
            return;
        set.RemoveWhere(static c => c == null);
    }

    void MaybeDebugLogFacing()
    {
        if (!debugLog)
            return;

        bool changed = !_dbgHasPrev
            || DominantCardinal != _dbgPrevCardinal
            || LeftSide != _dbgPrevLeft;

        if (!debugLogEveryFrame && !changed)
            return;

        _dbgHasPrev = true;
        _dbgPrevCardinal = DominantCardinal;
        _dbgPrevLeft = LeftSide;

        float spd = _rb != null ? Flatten(_rb.velocity).magnitude : 0f;
        string rose = CardinalToRoseJa(DominantCardinal);
        string roadName = PrimaryRoadCollider != null ? PrimaryRoadCollider.name : "(なし)";
        Debug.Log(
            $"[PlayerRoadTravelState] 進行方角: {rose}  (Cardinal={DominantCardinal})  " +
            $"dirXZ=({TravelDirectionXZ.x:F2},{TravelDirectionXZ.y:F2})  speed={spd:F2}  " +
            $"道路接触={IsOnRoad} primaryRoad={roadName}  左側推定={LeftSide}  lateral={SignedLateralFromRoadCenter:F2}",
            this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        directionCardinalUpdateIntervalSeconds = Mathf.Max(0f, directionCardinalUpdateIntervalSeconds);
    }
#endif
}
