using UnityEngine;

/// <summary>
/// プレイヤーが「違反中」かどうかを集約した静的状態。
/// <see cref="PlayerViolationStateHub"/> が毎フレーム更新し、信号は <see cref="NotifySignalViolationMoment"/> で短時間だけ true になります。
/// </summary>
public static class PlayerViolationState
{
    static float _signalPulseUntilUnscaled = float.NegativeInfinity;
    static float _pedestrianBellPulseUntilUnscaled = float.NegativeInfinity;
    static bool _ongoingSignalMushi;
    static bool _ongoingUnlitViolation;
    static bool _prevWrongWayViolating;
    static bool _prevSidewalkViolating;

    /// <summary>逆走・歩道・信号パルスのいずれかで true。</summary>
    public static bool IsViolatingNow { get; private set; }

    /// <summary>直近の Rebuild で参照した逆走フラグ（<see cref="WrongWayRoadMonitor.IsWrongWayRuleViolationActiveNow"/> と同じ）。</summary>
    public static bool IsWrongWayViolating { get; private set; }

    /// <summary>直近の Rebuild で参照した歩道フラグ。</summary>
    public static bool IsSidewalkViolating { get; private set; }

    /// <summary>信号違反発生からの短時間ウィンドウ内か（約1秒）。</summary>
    public static bool IsSignalViolationPulseActive =>
        Time.unscaledTime < _signalPulseUntilUnscaled;
    public static bool IsPedestrianBellViolationPulseActive =>
        Time.unscaledTime < _pedestrianBellPulseUntilUnscaled;
    public static bool IsUnlitViolationActive => _ongoingUnlitViolation;
    public static PoliceCatchViolationKind LastViolationKind { get; private set; } = PoliceCatchViolationKind.None;
    public static float LastViolationUnscaledTime { get; private set; } = float.NegativeInfinity;

    /// <summary>
    /// 信号違反が成立したフレームなどから呼び出し、<paramref name="durationSeconds"/> 秒だけ違反扱いに含めます。
    /// </summary>
    public static void NotifySignalViolationMoment(float durationSeconds = 1f)
    {
        _signalPulseUntilUnscaled = Time.unscaledTime + Mathf.Max(0.05f, durationSeconds);
        MarkViolation(PoliceCatchViolationKind.Signal);
        RefreshAggregated();
    }

    /// <summary>歩行者ベル違反の短時間パルス（偽警察などの集約判定向け）。</summary>
    public static void NotifyPedestrianBellViolationMoment(float durationSeconds = 1.5f)
    {
        _pedestrianBellPulseUntilUnscaled = Time.unscaledTime + Mathf.Max(0.05f, durationSeconds);
        MarkViolation(PoliceCatchViolationKind.PedestrianBell);
        RefreshAggregated();
    }

    /// <summary>
    /// <see cref="ShingouMushi"/> が毎フレーム設定。赤信号の横断ゾーン内など、信号虫の「継続中」表示・警察連携用。
    /// </summary>
    public static void ReportOngoingSignalMushi(bool ongoing)
    {
        if (_ongoingSignalMushi == ongoing)
            return;
        _ongoingSignalMushi = ongoing;
        if (ongoing)
            MarkViolation(PoliceCatchViolationKind.Signal);
        RefreshAggregated();
    }

    /// <summary>無灯火違反の継続状態を更新します。</summary>
    public static void ReportOngoingUnlitViolation(bool ongoing)
    {
        if (_ongoingUnlitViolation == ongoing)
            return;
        _ongoingUnlitViolation = ongoing;
        if (ongoing)
            MarkViolation(PoliceCatchViolationKind.UnlitBicycleAtNight);
        RefreshAggregated();
    }

    /// <summary>
    /// <see cref="PlayerViolationStateHub"/> から呼び出し。逆走・歩道の現在値を渡し、信号パルスと合算して <see cref="IsViolatingNow"/> を更新します。
    /// </summary>
    public static void Rebuild(bool wrongWayNow, bool sidewalkNow)
    {
        IsWrongWayViolating = wrongWayNow;
        IsSidewalkViolating = sidewalkNow;
        if (wrongWayNow && !_prevWrongWayViolating)
            MarkViolation(PoliceCatchViolationKind.WrongWay);
        if (sidewalkNow && !_prevSidewalkViolating)
            MarkViolation(PoliceCatchViolationKind.Sidewalk);
        _prevWrongWayViolating = wrongWayNow;
        _prevSidewalkViolating = sidewalkNow;
        RefreshAggregated();
    }

    static void MarkViolation(PoliceCatchViolationKind kind)
    {
        LastViolationKind = kind;
        LastViolationUnscaledTime = Time.unscaledTime;
    }

    static void RefreshAggregated()
    {
        IsViolatingNow = IsWrongWayViolating
            || IsSidewalkViolating
            || IsSignalViolationPulseActive
            || _ongoingSignalMushi
            || IsPedestrianBellViolationPulseActive
            || _ongoingUnlitViolation;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsForEnterPlayMode()
    {
        _signalPulseUntilUnscaled = float.NegativeInfinity;
        _pedestrianBellPulseUntilUnscaled = float.NegativeInfinity;
        _ongoingSignalMushi = false;
        _ongoingUnlitViolation = false;
        _prevWrongWayViolating = false;
        _prevSidewalkViolating = false;
        IsViolatingNow = false;
        IsWrongWayViolating = false;
        IsSidewalkViolating = false;
        LastViolationKind = PoliceCatchViolationKind.None;
        LastViolationUnscaledTime = float.NegativeInfinity;
    }
}
