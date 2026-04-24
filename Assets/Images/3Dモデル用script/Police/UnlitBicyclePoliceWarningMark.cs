using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 暗時間帯に灯火が消えているあいだのカウントダウン（逆走監視と同様の UI）と、
/// その間だけ点滅する警告テキスト。条件成立かつ警察視界内では
/// <see cref="PoliceLineOfSightCatch"/> に <see cref="PoliceCatchViolationKind.UnlitBicycleAtNight"/> を依頼します。
/// </summary>
[DefaultExecutionOrder(26)]
public class UnlitBicyclePoliceWarningMark : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("昼夜サイクル。空なら常に「暗め時間帯」とみなさない（カウント・警告は進まない）。")]
    [SerializeField] DayNightCycleController dayNight;

    [Tooltip("空なら Tag Player から BicycleLightToggle を検索")]
    [SerializeField] BicycleLightToggle bicycleLights;

    [Tooltip("速度判定用。未指定なら Player または bicycleLights から検索")]
    [SerializeField] Rigidbody speedSourceBody;

    [Header("カウントダウン（暗時間帯かつ無灯火・走行中）")]
    [Tooltip("満タンから 0 まで減る秒数")]
    [SerializeField] float unlitCountdownSeconds = 30f;
    [Tooltip("オン: 一定速度以上のときだけカウントが進む（逆走と同様）")]
    [SerializeField] bool countdownOnlyWhileMoving = true;
    [Tooltip("countdownOnlyWhileMoving のとき、これ未満の水平速度ではカウントしない")]
    [SerializeField] float minSpeed = 0.35f;
    [Tooltip("例: あと{0}秒 の {0} に切り上げ秒が入る")]
    [SerializeField] string countdownFormat = "あと{0}秒";
    [SerializeField] string countdownCompletedLabel = "完了";
    [SerializeField] TMP_Text countdownTmpText;
    [SerializeField] Text countdownUiText;
    [SerializeField] Color countdownTextColorCounting = Color.black;
    [SerializeField] Color countdownTextColorCompleted = Color.red;
    [Tooltip("オン: 昼・点灯に戻ったら残り秒を満タンに戻す")]
    [SerializeField] bool resetCountdownWhenUnlitEnds = true;
    [Tooltip("オン: 昼・点灯に戻ったら「完了」状態も解除し、次の無灯火で再度カウントできる")]
    [SerializeField] bool clearCompletedWhenSafe = true;

    [Header("UI — 無灯火中のハイライト（点滅）")]
    [Tooltip("カウントが進む無灯火中だけ色が脈動します。TMP / uGUI / Graphic のいずれかを指定可。")]
    [SerializeField] TMP_Text glowHighlightTmpText;
    [SerializeField] Text glowHighlightUiText;
    [SerializeField] Graphic glowGraphic;
    [SerializeField] Color glowColorNormal = Color.white;
    [SerializeField] Color glowColorUnlit = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] Color glowColorUnlitBright = new Color(1f, 0.65f, 0.2f, 1f);
    [Tooltip("点滅速度（大きいほど速く）")]
    [SerializeField] float glowPulseSpeed = 5f;

    [Header("警察警告")]
    [Tooltip("オン: 無灯火違反カウント進行中かつ視界内のとき、PoliceLineOfSightCatch に警告を依頼します。")]
    [SerializeField] bool requestPoliceCatchWhenSpottedWhileUnlit = true;
    [Tooltip("オン: カウント完了後も、暗時間帯で無灯火なら警察警告を継続します。")]
    [SerializeField] bool keepPoliceCatchAfterObjectiveComplete = true;

    [Header("反則金表示（PoliceLineOfSightCatch の再適用と揃える）")]
    [SerializeField] string fineAmountText = "5000円";
    [SerializeField] TMP_Text fineAmountDisplayTmp;
    [SerializeField] Text fineAmountDisplayUi;
    [SerializeField] TMP_Text[] additionalFineAmountTmp;
    [SerializeField] Text[] additionalFineAmountUi;

    [Header("その他")]
    [Tooltip("一時停止中はカウントを進めず、警察依頼もしない")]
    [SerializeField] bool skipWhileTrafficPaused = true;

    float _remainingUnlitSeconds;
    bool _countdownCompleted;
    bool _ruleViolationActiveSnapshot;

    /// <summary>
    /// 無灯火カウントが進行中（完了前）で、点滅 UI と同期。警察視界依頼の条件に使用。
    /// </summary>
    public bool IsUnlitRuleViolationActiveNow() => _ruleViolationActiveSnapshot;

    void Awake()
    {
        if (bicycleLights == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                bicycleLights = p.GetComponentInChildren<BicycleLightToggle>(true);
        }

        if (speedSourceBody == null && bicycleLights != null)
            speedSourceBody = bicycleLights.GetComponentInParent<Rigidbody>();
        if (speedSourceBody == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                speedSourceBody = p.GetComponent<Rigidbody>();
        }

        _remainingUnlitSeconds = Mathf.Max(0f, unlitCountdownSeconds);
        ApplyGlowVisual(false, false);
        UpdateCountdownTexts();
    }

    void LateUpdate()
    {
        PoliceLineOfSightState.ClearSightIfNoProbeUpdatedThisFrame();

        bool dark = dayNight != null && dayNight.IsDarkishDrivingPhase;
        bool lightsOn = bicycleLights == null || bicycleLights.AreLightsOn;
        bool paused = skipWhileTrafficPaused && MobTrafficPause.IsFrozen;

        Vector3 vel = speedSourceBody != null ? FlatVelocityXZ(speedSourceBody.velocity) : Vector3.zero;
        bool speedOk = vel.sqrMagnitude >= minSpeed * minSpeed;
        bool movingGate = !countdownOnlyWhileMoving || speedOk;

        bool unlitNightNoLights = dark && !lightsOn && movingGate;
        bool countActive = unlitNightNoLights && !paused;

        if ((!dark || lightsOn) && resetCountdownWhenUnlitEnds)
        {
            _remainingUnlitSeconds = Mathf.Max(0f, unlitCountdownSeconds);
            if (clearCompletedWhenSafe)
                _countdownCompleted = false;
        }

        TickUnlitCountdownUi(countActive, dark, lightsOn, movingGate);

        if (!requestPoliceCatchWhenSpottedWhileUnlit)
            return;

        bool policeSee = PoliceLineOfSightState.IsTargetInPoliceSightNow;
        bool cursorOk = Cursor.lockState == CursorLockMode.Locked;

        bool policeCatchActive = IsUnlitRuleViolationActiveNow() ||
                                 (keepPoliceCatchAfterObjectiveComplete && _countdownCompleted && dark && !lightsOn);

        if (!policeCatchActive || !policeSee || !cursorOk || paused)
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

    static Vector3 FlatVelocityXZ(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void TickUnlitCountdownUi(bool countActiveNow, bool dark, bool lightsOn, bool movingGate)
    {
        if (_countdownCompleted)
        {
            ApplyGlowVisual(false, true);
            SetCountdownDisplayText(countdownCompletedLabel);
            return;
        }

        if (countActiveNow)
            _remainingUnlitSeconds -= Time.deltaTime;

        if (_remainingUnlitSeconds <= 0f)
        {
            _remainingUnlitSeconds = 0f;
            _countdownCompleted = true;
            ViolationTimes.NotifyUnlitLightsViolationComplete();
            ViolationFineAmountDisplay.SetFineText(
                fineAmountText,
                fineAmountDisplayTmp,
                fineAmountDisplayUi,
                additionalFineAmountTmp,
                additionalFineAmountUi);
            ApplyGlowVisual(false, true);
            SetCountdownDisplayText(countdownCompletedLabel);
            return;
        }

        // 夜・無灯火だが速度不足のときは残り秒を満タンに戻す（逆走で道から外れたときに近い）
        if (!countActiveNow && resetCountdownWhenUnlitEnds && dark && !lightsOn && !movingGate)
            _remainingUnlitSeconds = Mathf.Max(0f, unlitCountdownSeconds);

        int showSec = Mathf.CeilToInt(_remainingUnlitSeconds);
        SetCountdownDisplayText(string.Format(countdownFormat, showSec));
        ApplyGlowVisual(countActiveNow, false);
    }

    void ApplyGlowVisual(bool unlitPulse, bool completed)
    {
        _ruleViolationActiveSnapshot = unlitPulse && !completed;

        Color c;
        if (completed || !unlitPulse)
            c = glowColorNormal;
        else
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseSpeed);
            c = Color.Lerp(glowColorUnlit, glowColorUnlitBright, t);
        }

        if (glowHighlightTmpText != null)
            glowHighlightTmpText.color = c;
        if (glowHighlightUiText != null)
            glowHighlightUiText.color = c;
        if (glowGraphic != null)
            glowGraphic.color = c;
    }

    void SetCountdownDisplayText(string text)
    {
        bool completed = _countdownCompleted;
        Color c = completed ? countdownTextColorCompleted : countdownTextColorCounting;
        if (countdownTmpText != null)
        {
            countdownTmpText.text = text;
            countdownTmpText.color = c;
        }
        if (countdownUiText != null)
        {
            countdownUiText.text = text;
            countdownUiText.color = c;
        }
    }

    void UpdateCountdownTexts()
    {
        if (_countdownCompleted)
            SetCountdownDisplayText(countdownCompletedLabel);
        else
            SetCountdownDisplayText(string.Format(countdownFormat, Mathf.CeilToInt(_remainingUnlitSeconds)));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        unlitCountdownSeconds = Mathf.Max(0.1f, unlitCountdownSeconds);
    }
#endif
}
