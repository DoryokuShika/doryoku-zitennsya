using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ?????E?t???E?M??????J?E???g??????????A?x?????s???W?i?c??0?j?????????N???A?V?[????J???????B
/// ?V?[???? <see cref="PedestrianBellObjectiveUi"/> ?????????? Awake ??x???????????????????????????B
/// </summary>
public class ViolationTimes : MonoBehaviour
{
    public static bool SidewalkViolationComplete { get; private set; }
    public static bool WrongWayViolationComplete { get; private set; }
    public static bool SignalViolationComplete { get; private set; }
    public static bool PedestrianBellObjectiveComplete { get; private set; }

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

    /// <summary>?x????W UI ????Z?b?g?????B?N???A?V?[?????„„????B</summary>
    public static void ResetPedestrianBellObjectiveComplete()
    {
        PedestrianBellObjectiveComplete = false;
    }

    public static bool IsAllViolationsComplete()
    {
        return SidewalkViolationComplete
            && WrongWayViolationComplete
            && SignalViolationComplete
            && PedestrianBellObjectiveComplete;
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
    }

    void Awake()
    {
        var bell = FindObjectsOfType<PedestrianBellObjectiveUi>(includeInactive: true);
        if (bell == null || bell.Length == 0)
            NotifyPedestrianBellObjectiveComplete();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsForEnterPlayMode()
    {
        SidewalkViolationComplete = false;
        WrongWayViolationComplete = false;
        SignalViolationComplete = false;
        PedestrianBellObjectiveComplete = false;
    }
#endif
}
