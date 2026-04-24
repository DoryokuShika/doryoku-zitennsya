using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ?????E?t???E?M???????E???s??x???E?????????u???J?E???g?????v???W??A
/// ??????????????N???A?V?[????J???????B
/// ?x????W UI ???????V?[????? Awake ??x???????A???????????????V?[?????????????????????????????B
/// </summary>
public class ViolationTimes : MonoBehaviour
{
    public static bool SidewalkViolationComplete { get; private set; }
    public static bool WrongWayViolationComplete { get; private set; }
    public static bool SignalViolationComplete { get; private set; }
    public static bool PedestrianBellObjectiveComplete { get; private set; }
    public static bool UnlitLightsViolationComplete { get; private set; }

    public static void NotifySidewalkViolationComplete()
    {
        if (SidewalkViolationComplete)
            return;
        SidewalkViolationComplete = true;
        TryLoadClearSceneIfAllComplete();
    }

    public static void NotifyWrongWayViolationComplete()
    {
        if (WrongWayViolationComplete)
            return;
        WrongWayViolationComplete = true;
        TryLoadClearSceneIfAllComplete();
    }

    public static void NotifySignalViolationComplete()
    {
        if (SignalViolationComplete)
            return;
        SignalViolationComplete = true;
        TryLoadClearSceneIfAllComplete();
    }

    public static void NotifyPedestrianBellObjectiveComplete()
    {
        if (PedestrianBellObjectiveComplete)
            return;
        PedestrianBellObjectiveComplete = true;
        TryLoadClearSceneIfAllComplete();
    }

    /// <summary>?????т??????J?E???g?_?E???? 0 ????????????т???B</summary>
    public static void NotifyUnlitLightsViolationComplete()
    {
        if (UnlitLightsViolationComplete)
            return;
        UnlitLightsViolationComplete = true;
        TryLoadClearSceneIfAllComplete();
    }

    /// <summary>歩行者ベル目標をクリア条件から外すときなどに false に戻します。</summary>
    public static void ResetPedestrianBellObjectiveComplete()
    {
        PedestrianBellObjectiveComplete = false;
    }

    public static bool IsAllViolationsComplete()
    {
        return SidewalkViolationComplete
            && WrongWayViolationComplete
            && SignalViolationComplete
            && PedestrianBellObjectiveComplete
            && UnlitLightsViolationComplete;
    }

    static void TryLoadClearSceneIfAllComplete()
    {
        if (!IsAllViolationsComplete())
            return;

        Timer.isRunning = false;
        ScoreManager.SaveBestTime(Timer.timer);
        ShingouMushi.ClearTime = Timer.timer;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene("Clear");
    }

    public static void ResetAll()
    {
        SidewalkViolationComplete = false;
        WrongWayViolationComplete = false;
        SignalViolationComplete = false;
        PedestrianBellObjectiveComplete = false;
        UnlitLightsViolationComplete = false;
    }

    void Awake()
    {
        var bell = FindObjectsOfType<PedestrianBellObjectiveUi>(includeInactive: true);
        if (bell == null || bell.Length == 0)
            NotifyPedestrianBellObjectiveComplete();

        var unlit = FindObjectsOfType<UnlitBicyclePoliceWarningMark>(includeInactive: true);
        if (unlit == null || unlit.Length == 0)
            NotifyUnlitLightsViolationComplete();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsForEnterPlayMode()
    {
        SidewalkViolationComplete = false;
        WrongWayViolationComplete = false;
        SignalViolationComplete = false;
        PedestrianBellObjectiveComplete = false;
        UnlitLightsViolationComplete = false;
    }
#endif
}
