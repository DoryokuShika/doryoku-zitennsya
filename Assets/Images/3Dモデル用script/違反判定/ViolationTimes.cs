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

    /// <summary>?????��??????J?E???g?_?E???? 0 ????????????��???B</summary>
    public static void NotifyUnlitLightsViolationComplete()
    {
        if (UnlitLightsViolationComplete)
            return;
        UnlitLightsViolationComplete = true;
        TryLoadClearSceneIfAllComplete();
    }

    /// <summary>���s�҃x���ڕW���N���A��������O���Ƃ��Ȃǂ� false �ɖ߂��܂��B</summary>
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

    /// <summary>
    /// 本編シーン（SampleScene）に入ったらフラグを毎回リセットする対象シーン名。
    /// 別名にする場合はここを変更してください。
    /// </summary>
    static readonly string[] GameplaySceneNamesToResetOn = new string[]
    {
        "SampleScene",
    };

    void Awake()
    {
        // ビルド .exe で前回プレイのフラグが残るのを防ぐため、本編シーンの開始時には必ずリセット。
        // （シーン未アタッチでも RuntimeInitialize 経由のリスナーで動くので保険）
        ResetAll();

        var bell = FindObjectsOfType<PedestrianBellObjectiveUi>(includeInactive: true);
        if (bell == null || bell.Length == 0)
            NotifyPedestrianBellObjectiveComplete();

        var unlit = FindObjectsOfType<UnlitBicyclePoliceWarningMark>(includeInactive: true);
        if (unlit == null || unlit.Length == 0)
            NotifyUnlitLightsViolationComplete();
    }

    /// <summary>
    /// .exe / エディタの両方で、起動直後と本編シーンに切り替わるたびに違反フラグを初期化する。
    /// ViolationTimes コンポーネントがどのシーンにも貼られていなくても確実にリセットされる。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterAutoResetOnSceneLoad()
    {
        ResetAll();

        SceneManager.sceneLoaded -= HandleSceneLoadedForViolationReset;
        SceneManager.sceneLoaded += HandleSceneLoadedForViolationReset;
    }

    static void HandleSceneLoadedForViolationReset(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        if (GameplaySceneNamesToResetOn == null)
            return;

        for (int i = 0; i < GameplaySceneNamesToResetOn.Length; i++)
        {
            if (!string.IsNullOrEmpty(GameplaySceneNamesToResetOn[i])
                && scene.name == GameplaySceneNamesToResetOn[i])
            {
                ResetAll();
                return;
            }
        }
    }
}
