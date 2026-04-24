using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 夕方・夜・朝のいずれか（<see cref="DayNightCycleController.IsDarkishDrivingPhase"/>）かつ
/// 自転車ライトが消えている（<see cref="BicycleLightToggle.AreLightsOn"/>）かつ
/// 警察視界内（<see cref="PoliceLineOfSightState"/>）のとき、
/// <see cref="PoliceLineOfSightCatch"/> に <see cref="PoliceCatchViolationKind.UnlitBicycleAtNight"/> を依頼します
/// （歩行者ベル・逆走などと同じ警告パネル・反則金表示の流れ）。
/// </summary>
[DefaultExecutionOrder(26)]
public class UnlitBicyclePoliceWarningMark : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("昼夜サイクル。空なら常に「暗め時間帯」とみなさない（警告は出ない）。")]
    [SerializeField] DayNightCycleController dayNight;

    [Tooltip("空なら Tag Player から BicycleLightToggle を検索")]
    [SerializeField] BicycleLightToggle bicycleLights;

    [Header("警察警告")]
    [Tooltip("オン: 条件成立中かつ視界内のとき、PoliceLineOfSightCatch に警告を依頼します。")]
    [SerializeField] bool requestPoliceCatchWhenSpottedWhileUnlit = true;

    [Header("反則金表示（PoliceLineOfSightCatch の再適用と揃える）")]
    [SerializeField] string fineAmountText = "5000円";
    [SerializeField] TMP_Text fineAmountDisplayTmp;
    [SerializeField] Text fineAmountDisplayUi;
    [SerializeField] TMP_Text[] additionalFineAmountTmp;
    [SerializeField] Text[] additionalFineAmountUi;

    [Header("その他")]
    [Tooltip("一時停止中は依頼しない")]
    [SerializeField] bool skipWhileTrafficPaused = true;

    void Awake()
    {
        if (bicycleLights == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                bicycleLights = p.GetComponentInChildren<BicycleLightToggle>(true);
        }
    }

    void LateUpdate()
    {
        PoliceLineOfSightState.ClearSightIfNoProbeUpdatedThisFrame();

        if (!requestPoliceCatchWhenSpottedWhileUnlit)
            return;

        bool dark = dayNight != null && dayNight.IsDarkishDrivingPhase;
        bool lightsOn = bicycleLights == null || bicycleLights.AreLightsOn;
        bool policeSee = PoliceLineOfSightState.IsTargetInPoliceSightNow;
        bool cursorOk = Cursor.lockState == CursorLockMode.Locked;
        bool paused = skipWhileTrafficPaused && MobTrafficPause.IsFrozen;

        bool violating = !paused && dark && !lightsOn && policeSee && cursorOk;
        if (!violating)
            return;

        PoliceLineOfSightCatch.NotifyUnlitBicycleFineForNextCatch(fineAmountText);
        ViolationFineAmountDisplay.SetFineText(
            fineAmountText,
            fineAmountDisplayTmp,
            fineAmountDisplayUi,
            additionalFineAmountTmp,
            additionalFineAmountUi);
        PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind.UnlitBicycleAtNight);
    }
}
