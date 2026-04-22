using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ???????s?E?t???E?M????????u???????v?t???O???W??A?O????????????????N???A?V?[????J???????B
/// </summary>
public class ViolationTimes : MonoBehaviour
{
    public static bool SidewalkViolationComplete { get; private set; }
    public static bool WrongWayViolationComplete { get; private set; }
    public static bool SignalViolationComplete { get; private set; }

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

    public static bool IsAllViolationsComplete()
    {
        return SidewalkViolationComplete && WrongWayViolationComplete && SignalViolationComplete;
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
    }
}
