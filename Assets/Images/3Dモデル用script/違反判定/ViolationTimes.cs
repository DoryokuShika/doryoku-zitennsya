using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 歩道走行・逆走・信号無視の「違反完了」フラグを集約し、三つそろったときだけクリアシーンへ遷移します。
/// </summary>
public class ViolationTimes : MonoBehaviour
{
    /// <summary>信号違反ゾーンに一度入ったあと（二度目の進入でゲームオーバー）。</summary>
    public static bool isShingouMushi = false;

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
        isShingouMushi = false;
        SidewalkViolationComplete = false;
        WrongWayViolationComplete = false;
        SignalViolationComplete = false;
    }
}
