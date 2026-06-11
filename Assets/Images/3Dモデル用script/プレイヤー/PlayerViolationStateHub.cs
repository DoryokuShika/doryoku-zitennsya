using UnityEngine;

/// <summary>
/// 同じオブジェクト上の <see cref="WrongWayRoadMonitor"/> / <see cref="SidewalkOnlyTextColor"/> から
/// 違反中かどうかを読み、<see cref="PlayerViolationState.Rebuild"/> に渡します。
/// <see cref="WrongWayRoadMonitor"/> / <see cref="SidewalkOnlyTextColor"/> の LateUpdate より後で集約します（既定 25 の後）。
/// </summary>
[DefaultExecutionOrder(30)]
[DisallowMultipleComponent]
public class PlayerViolationStateHub : MonoBehaviour
{
    [Tooltip("オフのままだと逆走フラグが更新されません。オンで再生開始時にコンポーネントを有効化します。")]
    [SerializeField] bool autoEnableWrongWayMonitor = true;
    [Tooltip("歩道違反表示用。同様に無効だと判定が進みません。")]
    [SerializeField] bool autoEnableSidewalkMonitor = true;
    [Tooltip("逆走判定に必要。オフだと方角が更新されず警察連携しません。")]
    [SerializeField] bool autoEnablePlayerRoadTravelState = true;

    [SerializeField] WrongWayRoadMonitor wrongWayMonitor;
    [SerializeField] SidewalkOnlyTextColor sidewalkMonitor;

    void Awake()
    {
        if (wrongWayMonitor == null)
            wrongWayMonitor = GetComponentInChildren<WrongWayRoadMonitor>(true);
        if (sidewalkMonitor == null)
            sidewalkMonitor = GetComponentInChildren<SidewalkOnlyTextColor>(true);
    }

    void Start()
    {
        if (!Application.isPlaying)
            return;

        if (autoEnableWrongWayMonitor && wrongWayMonitor != null && !wrongWayMonitor.enabled)
            wrongWayMonitor.enabled = true;

        if (autoEnableSidewalkMonitor && sidewalkMonitor != null && !sidewalkMonitor.enabled)
            sidewalkMonitor.enabled = true;

        if (autoEnablePlayerRoadTravelState)
        {
            var road = GetComponentInChildren<PlayerRoadTravelState>(true);
            if (road != null && !road.enabled)
                road.enabled = true;
        }
    }

    void LateUpdate()
    {
        bool w = wrongWayMonitor != null && wrongWayMonitor.IsWrongWayViolatingForPlayerStateAggregator();
        bool s = sidewalkMonitor != null && sidewalkMonitor.IsSidewalkViolatingForPlayerStateAggregator();
        PlayerViolationState.Rebuild(w, s);
    }
}
