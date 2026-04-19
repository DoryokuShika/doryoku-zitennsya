using UnityEngine;

/// <summary>
/// 同じオブジェクト上の <see cref="WrongWayRoadMonitor"/> / <see cref="SidewalkOnlyTextColor"/> から
/// 違反中かどうかを読み、<see cref="PlayerViolationState.Rebuild"/> に渡します。
/// <see cref="WrongWayRoadMonitor"/> / <see cref="SidewalkOnlyTextColor"/> の LateUpdate より後で集約します。
/// </summary>
[DefaultExecutionOrder(10)]
[DisallowMultipleComponent]
public class PlayerViolationStateHub : MonoBehaviour
{
    [SerializeField] WrongWayRoadMonitor wrongWayMonitor;
    [SerializeField] SidewalkOnlyTextColor sidewalkMonitor;

    void Awake()
    {
        if (wrongWayMonitor == null)
            wrongWayMonitor = GetComponentInChildren<WrongWayRoadMonitor>(true);
        if (sidewalkMonitor == null)
            sidewalkMonitor = GetComponentInChildren<SidewalkOnlyTextColor>(true);
    }

    void LateUpdate()
    {
        bool w = wrongWayMonitor != null && wrongWayMonitor.IsWrongWayActiveNow;
        bool s = sidewalkMonitor != null && sidewalkMonitor.IsSidewalkRuleViolationActiveNow();
        PlayerViolationState.Rebuild(w, s);
    }
}
