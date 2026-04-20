using UnityEngine;

/// <summary>
/// 警察側から「対象が視野内か」だけをフレーム単位で集約した静的状態。
/// <see cref="PoliceTargetLineOfSightProbe"/> が毎フレーム報告し、複数警官がいても OR でまとまります。
/// </summary>
public static class PoliceLineOfSightState
{
    static int _aggregationFrame = -1;
    static bool _anyProbeSeesTarget;

    /// <summary>直近フレームで、いずれかのプローブが対象を視界内と判定したか。</summary>
    public static bool IsTargetInPoliceSightNow { get; private set; }

    /// <summary>
    /// プローブが1つも動いていないフレームでは集約が走らないため、
    /// <see cref="PoliceLineOfSightCatch"/> の LateUpdate 先頭などで呼び、古い true を消します。
    /// </summary>
    public static void ClearSightIfNoProbeUpdatedThisFrame()
    {
        if (Time.frameCount != _aggregationFrame)
            IsTargetInPoliceSightNow = false;
    }

    /// <summary>
    /// <see cref="PoliceTargetLineOfSightProbe"/> からのみ呼び出し。同一フレーム内で複数回よび、いずれかが true なら最終的に true。
    /// </summary>
    internal static void ProbeReportsInSight(bool visible)
    {
        int f = Time.frameCount;
        if (f != _aggregationFrame)
        {
            _aggregationFrame = f;
            _anyProbeSeesTarget = false;
        }

        if (visible)
            _anyProbeSeesTarget = true;

        IsTargetInPoliceSightNow = _anyProbeSeesTarget;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsForEnterPlayMode()
    {
        _aggregationFrame = -1;
        _anyProbeSeesTarget = false;
        IsTargetInPoliceSightNow = false;
    }
#endif
}
