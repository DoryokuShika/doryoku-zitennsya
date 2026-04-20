using UnityEngine;

/// <summary>
/// プレイヤーが「違反中」かどうかを集約した静的状態。
/// <see cref="PlayerViolationStateHub"/> が毎フレーム更新し、信号は <see cref="NotifySignalViolationMoment"/> で短時間だけ true になります。
/// </summary>
public static class PlayerViolationState
{
    static float _signalPulseUntilUnscaled = float.NegativeInfinity;

    /// <summary>逆走・歩道・信号パルスのいずれかで true。</summary>
    public static bool IsViolatingNow { get; private set; }

    /// <summary>直近の Rebuild で参照した逆走フラグ（<see cref="WrongWayRoadMonitor.IsWrongWayRuleViolationActiveNow"/> と同じ）。</summary>
    public static bool IsWrongWayViolating { get; private set; }

    /// <summary>直近の Rebuild で参照した歩道フラグ。</summary>
    public static bool IsSidewalkViolating { get; private set; }

    /// <summary>信号違反発生からの短時間ウィンドウ内か（約1秒）。</summary>
    public static bool IsSignalViolationPulseActive =>
        Time.unscaledTime < _signalPulseUntilUnscaled;

    /// <summary>
    /// 信号違反が成立したフレームなどから呼び出し、<paramref name="durationSeconds"/> 秒だけ違反扱いに含めます。
    /// </summary>
    public static void NotifySignalViolationMoment(float durationSeconds = 1f)
    {
        _signalPulseUntilUnscaled = Time.unscaledTime + Mathf.Max(0.05f, durationSeconds);
        RefreshAggregated();
    }

    /// <summary>
    /// <see cref="PlayerViolationStateHub"/> から呼び出し。逆走・歩道の現在値を渡し、信号パルスと合算して <see cref="IsViolatingNow"/> を更新します。
    /// </summary>
    public static void Rebuild(bool wrongWayNow, bool sidewalkNow)
    {
        IsWrongWayViolating = wrongWayNow;
        IsSidewalkViolating = sidewalkNow;
        RefreshAggregated();
    }

    static void RefreshAggregated()
    {
        IsViolatingNow = IsWrongWayViolating || IsSidewalkViolating || IsSignalViolationPulseActive;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsForEnterPlayMode()
    {
        _signalPulseUntilUnscaled = float.NegativeInfinity;
        IsViolatingNow = false;
        IsWrongWayViolating = false;
        IsSidewalkViolating = false;
    }
#endif
}
