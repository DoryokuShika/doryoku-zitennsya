using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Signal run (mushi) check. Legacy: one collider + Shinngoukichenge.
/// Crosswalk mode: CrosswalkFourWayTrafficController + four triggers; violation only if that side vehicle signal is red (0).
/// Optional: require bicycle/player forward on XZ to match a per-side compass (N/S/E/W) when counting red-light violation.
/// Optional: multiple strikes show "remaining count" UI until zero then "complete" and ViolationTimes.NotifySignalViolationComplete.
/// </summary>
[DefaultExecutionOrder(25)]
public class ShingouMushi : MonoBehaviour
{
    public enum ApproachCompass
    {
        Any = 0,
        North = 1,
        South = 2,
        East = 3,
        West = 4,
    }

    [Header("Legacy (single collider + Shinngoukichenge)")]
    [Tooltip("Collider for mushi zone. If empty, uses this GameObject.")]
    [SerializeField] GameObject MushiDecision;

    [Tooltip("Single-intersection lights. Unused for vehicle state in crosswalk mode.")]
    [SerializeField] Shinngoukichenge shinngoukired;

    [Tooltip("GameObject with Shinngoukichenge when reference above is empty.")]
    [SerializeField] GameObject shinngoukiredObject;

    [Header("Crosswalk (four blocks + CrosswalkFourWayTrafficController)")]
    [Tooltip("Cross traffic controller. Crosswalk mode if set and any block below is set.")]
    [SerializeField] CrosswalkFourWayTrafficController crosswalkTraffic;

    [Tooltip("North / +Z crosswalk trigger (GameObject with Is Trigger Collider).")]
    [SerializeField] GameObject crosswalkBlockUp;

    [Tooltip("South crosswalk trigger.")]
    [SerializeField] GameObject crosswalkBlockDown;

    [Tooltip("West crosswalk trigger.")]
    [SerializeField] GameObject crosswalkBlockLeft;

    [Tooltip("East crosswalk trigger.")]
    [SerializeField] GameObject crosswalkBlockRight;

    [Header("Crosswalk: approach heading (XZ, bicycle forward)")]
    [Tooltip("If true, red + heading must match the compass set for that crosswalk side. If false, only red (legacy crosswalk behavior).")]
    [SerializeField] bool requireApproachCompassForViolation;

    [Tooltip("Max angle (degrees) between horizontal forward and compass direction.")]
    [SerializeField] [Range(1f, 90f)] float approachCompassMaxAngleDegrees = 40f;

    [Tooltip("Up crosswalk on red: require this compass on XZ (e.g. North = world +Z).")]
    [SerializeField] ApproachCompass violationApproachWhenSideUp = ApproachCompass.North;

    [Tooltip("Down crosswalk on red: require this compass.")]
    [SerializeField] ApproachCompass violationApproachWhenSideDown = ApproachCompass.West;

    [Tooltip("Left crosswalk on red: require this compass.")]
    [SerializeField] ApproachCompass violationApproachWhenSideLeft = ApproachCompass.South;

    [Tooltip("Right crosswalk on red: require this compass.")]
    [SerializeField] ApproachCompass violationApproachWhenSideRight = ApproachCompass.North;

    [Header("Signal violation strikes (before ViolationTimes complete)")]
    [Tooltip("How many red-light crossings count before NotifySignalViolationComplete. 1 = single shot (old behavior).")]
    [SerializeField] [Min(1)] int signalViolationStrikesBeforeObjectiveComplete = 5;

    [Tooltip("String.Format: {0} = count shown for this strike (first strike shows configured total, e.g. 5).")]
    [SerializeField] string signalViolationRemainingCountFormat = "\u6b8b\u308a{0}\u56de";

    [Header("Signal violation cooldown")]
    [Tooltip("After one mushi strike resolves, wait this long (unscaled seconds) before another strike can start.")]
    [SerializeField] float signalViolationCooldownSecondsAfterStrike = 2f;

    [Header("Signal violation complete UI delay")]
    [Tooltip("最終ストライクで「残り1回」を表示してから「完了」表示・ViolationTimes.NotifySignalViolationComplete に切り替えるまでの保持秒数（unscaled）。同フレーム内で残り表示が「完了」へ即上書きされる問題を防ぐ。")]
    [SerializeField] float signalCompleteUiHoldSeconds = 0.6f;

    [Header("Violation complete UI (mushi)")]
    [SerializeField] TMP_Text signalViolationCompleteTmp;
    [SerializeField] Text signalViolationCompleteUi;
    [SerializeField] string signalViolationCompleteLabel = "\u5B8C\u4E86";
    [SerializeField] Color signalViolationCompleteTextColor = Color.red;

    [SerializeField] TMP_Text[] additionalTmpTurnRedOnComplete;
    [SerializeField] Text[] additionalUiTurnRedOnComplete;

    [Header("Fine amount")]
    [SerializeField] string fineAmountText = "6000\u5186";
    [SerializeField] TMP_Text fineAmountDisplayTmp;
    [SerializeField] Text fineAmountDisplayUi;
    [SerializeField] TMP_Text[] additionalFineAmountTmp;
    [SerializeField] Text[] additionalFineAmountUi;

    [Header("UI when police did not see")]
    [Tooltip("TMP when completed without police sight.")]
    [SerializeField] TMP_Text completedWhenNotSpottedTmp;

    [Tooltip("uGUI Text when completed without police sight.")]
    [SerializeField] Text completedWhenNotSpottedUi;

    [SerializeField] string completedWhenNotSpottedLabel = "\u5B8C\u4E86";
    [SerializeField] Color completedWhenNotSpottedTextColor = Color.white;

    [SerializeField] TMP_Text[] additionalTmpOnNotSpottedComplete;
    [SerializeField] Text[] additionalUiOnNotSpottedComplete;
    [SerializeField] Color additionalNotSpottedTextColor = Color.white;

    [Header("Police sight")]
    [Tooltip("Show fine UI only when police can see the violation this frame.")]
    [SerializeField] bool showCatchUiWhenPoliceSeePlayerOnMushiComplete = true;
    [Tooltip("??: ????????????????????????????")]
    [SerializeField] bool keepPoliceCatchAfterObjectiveComplete = true;

    Collider _legacyDecisionCollider;
    bool _playerInsideMushiZone;
    bool _pendingResolutionAfterEnter;
    bool _useCrosswalkMode;
    int _strikesRemaining = -1;
    float _mushiStrikeCooldownUntilUnscaled = float.NegativeInfinity;
    bool _strikeRegisteredForCurrentInside;
    bool _signalCompleteUiApplied;
    bool _signalCompletePending;
    float _signalCompleteUiDelayUntilUnscaled = float.NegativeInfinity;
    float _lastPlayerTriggerSeenUnscaled = float.NegativeInfinity;
    [SerializeField] float insideStateStaleTimeoutSeconds = 0.35f;

    /// <summary>
    /// クロスウォークモード時、現在違反状態（赤＋プレイヤー侵入中＋向き条件OK）の側を保持する集合。
    /// 4ブロック間で <see cref="_playerInsideMushiZone"/> が競合上書きされてカウント漏れする問題を防ぐため、
    /// 「どれか1側でも違反中ならゾーン内扱い」のロジックを集合化する。
    /// </summary>
    readonly HashSet<CrosswalkFourWayTrafficController.CrosswalkSide> _activeCrosswalkViolatingSides
        = new HashSet<CrosswalkFourWayTrafficController.CrosswalkSide>();

    /// <summary>Last crosswalk side the player entered (crosswalk mode).</summary>
    public CrosswalkFourWayTrafficController.CrosswalkSide LastEnteredCrosswalkSide { get; private set; }

    /// <summary>Vehicle signal then: 0=red, 1=green, 2=yellow (crosswalk mode).</summary>
    public int LastCrosswalkVehicleState { get; private set; }

    /// <summary>Last compass requirement used when checking heading (crosswalk mode).</summary>
    public ApproachCompass LastApproachCompassRequired { get; private set; }

    /// <summary>Whether horizontal forward matched the requirement on last enter (or Any / check off).</summary>
    public bool LastApproachHeadingMatched { get; private set; }

    /// <summary>True while new mushi strikes cannot start (after last resolved strike).</summary>
    public bool IsSignalMushiStrikeCooldownActive =>
        Time.unscaledTime < _mushiStrikeCooldownUntilUnscaled;

    public bool IsActiveSignalViolationNow()
    {
        if (!_playerInsideMushiZone)
            return false;

        if (_useCrosswalkMode)
        {
            if (crosswalkTraffic == null)
                return false;
            int st = crosswalkTraffic.GetVehicleStateAtCrosswalkSide(LastEnteredCrosswalkSide);
            return st == 0;
        }

        if (shinngoukired == null || _legacyDecisionCollider == null)
            return false;
        return shinngoukired.State != 1;
    }

    void Awake()
    {
        if (MushiDecision == null)
            MushiDecision = gameObject;

        if (shinngoukired == null && shinngoukiredObject != null)
            shinngoukired = shinngoukiredObject.GetComponent<Shinngoukichenge>();

        if (MushiDecision != null)
            _legacyDecisionCollider = MushiDecision.GetComponent<Collider>();

        _useCrosswalkMode = crosswalkTraffic != null && HasAnyCrosswalkBlock();
        if (_useCrosswalkMode)
        {
            TryAddRelay(crosswalkBlockUp, CrosswalkFourWayTrafficController.CrosswalkSide.Up);
            TryAddRelay(crosswalkBlockDown, CrosswalkFourWayTrafficController.CrosswalkSide.Down);
            TryAddRelay(crosswalkBlockLeft, CrosswalkFourWayTrafficController.CrosswalkSide.Left);
            TryAddRelay(crosswalkBlockRight, CrosswalkFourWayTrafficController.CrosswalkSide.Right);
        }
    }

    void OnValidate()
    {
        signalViolationCooldownSecondsAfterStrike = Mathf.Max(0f, signalViolationCooldownSecondsAfterStrike);
        insideStateStaleTimeoutSeconds = Mathf.Max(0.05f, insideStateStaleTimeoutSeconds);
    }

    bool HasAnyCrosswalkBlock()
    {
        return crosswalkBlockUp != null
            || crosswalkBlockDown != null
            || crosswalkBlockLeft != null
            || crosswalkBlockRight != null;
    }

    void TryAddRelay(GameObject blockRoot, CrosswalkFourWayTrafficController.CrosswalkSide side)
    {
        if (blockRoot == null)
            return;
        var col = blockRoot.GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("ShingouMushi: crosswalk block '" + blockRoot.name + "' has no Collider.", this);
            return;
        }

        var relay = blockRoot.GetComponent<ShingouMushiCrosswalkRelay>();
        if (relay == null)
            relay = blockRoot.AddComponent<ShingouMushiCrosswalkRelay>();
        relay.Initialize(this, side);
    }

    void Update()
    {
        if (_useCrosswalkMode)
            return;

        if (shinngoukired == null || _legacyDecisionCollider == null)
            return;
        _legacyDecisionCollider.enabled = shinngoukired.State != 1;
    }

    public static float ClearTime = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticRuntimeState()
    {
        ClearTime = 0f;
    }

    void OnTriggerExit(Collider other)
    {
        if (_useCrosswalkMode)
            return;
        if (!other.gameObject.CompareTag("Player"))
            return;
        SetInsideState(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_useCrosswalkMode)
            return;
        if (!other.gameObject.CompareTag("Player"))
            return;

        _lastPlayerTriggerSeenUnscaled = Time.unscaledTime;
        SetInsideState(true);

        if (ViolationTimes.SignalViolationComplete && !keepPoliceCatchAfterObjectiveComplete)
            return;

        if (IsSignalMushiStrikeCooldownActive)
            return;

        if (!_strikeRegisteredForCurrentInside)
            _pendingResolutionAfterEnter = true;
    }

    void OnTriggerStay(Collider other)
    {
        if (_useCrosswalkMode)
            return;
        if (!other.gameObject.CompareTag("Player"))
            return;

        _lastPlayerTriggerSeenUnscaled = Time.unscaledTime;
        SetInsideState(true);

        if (ViolationTimes.SignalViolationComplete && !keepPoliceCatchAfterObjectiveComplete)
            return;

        if (IsSignalMushiStrikeCooldownActive)
            return;

        // Enter 取りこぼしや、クールダウン明けにゾーン内へ残っているケースを補完する。
        if (!_strikeRegisteredForCurrentInside)
            _pendingResolutionAfterEnter = true;
    }

    /// <summary>Called from ShingouMushiCrosswalkRelay on each crosswalk block.</summary>
    public void OnCrosswalkTriggerEnter(CrosswalkFourWayTrafficController.CrosswalkSide side, Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
            return;
        if (crosswalkTraffic == null)
            return;

        _lastPlayerTriggerSeenUnscaled = Time.unscaledTime;
        bool conditionsMet = EvaluateSideViolation(side, other);
        UpdateCrosswalkSideActivity(side, conditionsMet);

        if (!conditionsMet)
            return;

        if (ViolationTimes.SignalViolationComplete && !keepPoliceCatchAfterObjectiveComplete)
            return;

        if (IsSignalMushiStrikeCooldownActive)
            return;

        if (!_strikeRegisteredForCurrentInside)
            _pendingResolutionAfterEnter = true;
    }

    /// <summary>
    /// 各クロスウォークブロックの Exit から呼ばれる。古いオーバーロード互換用。
    /// 個別の側を渡す版（<see cref="OnCrosswalkTriggerExit(CrosswalkFourWayTrafficController.CrosswalkSide, Collider)"/>）
    /// を優先して使用してください。側が分からない場合は安全側として全側をクリアします。
    /// </summary>
    public void OnCrosswalkTriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
            return;
        // 側が分からないとき：すべての側を一括クリア（プレイヤーが完全に交差点から離れたケース）。
        _activeCrosswalkViolatingSides.Clear();
        SetInsideState(false);
    }

    /// <summary>側を指定してExitを処理する版。同一プレイヤーが他ブロックに居る場合に他側を保持する。</summary>
    public void OnCrosswalkTriggerExit(CrosswalkFourWayTrafficController.CrosswalkSide side, Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
            return;
        // 出た側だけをクリア。他側がまだ違反中なら _playerInsideMushiZone は維持される。
        _activeCrosswalkViolatingSides.Remove(side);
        SetInsideState(_activeCrosswalkViolatingSides.Count > 0);
    }

    public void OnCrosswalkTriggerStay(CrosswalkFourWayTrafficController.CrosswalkSide side, Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
            return;
        if (crosswalkTraffic == null)
            return;

        _lastPlayerTriggerSeenUnscaled = Time.unscaledTime;
        bool conditionsMet = EvaluateSideViolation(side, other);
        UpdateCrosswalkSideActivity(side, conditionsMet);

        if (!conditionsMet)
            return;

        if (ViolationTimes.SignalViolationComplete && !keepPoliceCatchAfterObjectiveComplete)
            return;

        if (IsSignalMushiStrikeCooldownActive)
            return;

        // Enter 取りこぼしや、クールダウン明けにゾーン内へ残っているケースを補完する。
        if (!_strikeRegisteredForCurrentInside)
            _pendingResolutionAfterEnter = true;
    }

    /// <summary>
    /// 指定した側で違反条件（赤信号 + 向き一致）を満たすか判定し、Last* プロパティも更新する。
    /// </summary>
    bool EvaluateSideViolation(CrosswalkFourWayTrafficController.CrosswalkSide side, Collider other)
    {
        LastEnteredCrosswalkSide = side;
        LastCrosswalkVehicleState = crosswalkTraffic.GetVehicleStateAtCrosswalkSide(side);
        LastApproachCompassRequired = ResolveRequiredCompassForSide(side);
        LastApproachHeadingMatched = !requireApproachCompassForViolation
            || LastApproachCompassRequired == ApproachCompass.Any
            || MeetsApproachCompass(other, LastApproachCompassRequired);

        bool isRedForThisSide = LastCrosswalkVehicleState == 0;
        bool headingOk = !requireApproachCompassForViolation || LastApproachHeadingMatched;
        return isRedForThisSide && headingOk;
    }

    /// <summary>
    /// 側ごとの違反状態を集合に反映し、集合が空かどうかで <see cref="_playerInsideMushiZone"/> を更新する。
    /// 別ブロックの Stay 競合や、Exit の上書きでカウントが消えるバグを防ぐ。
    /// </summary>
    void UpdateCrosswalkSideActivity(CrosswalkFourWayTrafficController.CrosswalkSide side, bool isViolatingNow)
    {
        if (isViolatingNow)
            _activeCrosswalkViolatingSides.Add(side);
        else
            _activeCrosswalkViolatingSides.Remove(side);

        SetInsideState(_activeCrosswalkViolatingSides.Count > 0);
    }

    void SetInsideState(bool inside)
    {
        if (_playerInsideMushiZone == inside)
            return;

        _playerInsideMushiZone = inside;
        if (!inside)
        {
            _pendingResolutionAfterEnter = false;
            _strikeRegisteredForCurrentInside = false;
        }
    }

    ApproachCompass ResolveRequiredCompassForSide(CrosswalkFourWayTrafficController.CrosswalkSide side)
    {
        return side switch
        {
            CrosswalkFourWayTrafficController.CrosswalkSide.Up => violationApproachWhenSideUp,
            CrosswalkFourWayTrafficController.CrosswalkSide.Down => violationApproachWhenSideDown,
            CrosswalkFourWayTrafficController.CrosswalkSide.Left => violationApproachWhenSideLeft,
            CrosswalkFourWayTrafficController.CrosswalkSide.Right => violationApproachWhenSideRight,
            _ => ApproachCompass.Any,
        };
    }

    bool MeetsApproachCompass(Collider other, ApproachCompass required)
    {
        if (required == ApproachCompass.Any)
            return true;

        Transform t = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        Vector3 flat = new Vector3(t.forward.x, 0f, t.forward.z);
        if (flat.sqrMagnitude < 1e-6f)
            return false;
        flat.Normalize();

        Vector3 want = CompassToFlatVector(required);
        float maxAng = Mathf.Clamp(approachCompassMaxAngleDegrees, 1f, 90f);
        return Vector3.Angle(flat, want) <= maxAng;
    }

    /// <summary>World XZ: North=+Z, South=-Z, East=+X, West=-X (change scene orientation via parent rotation if needed).</summary>
    public static Vector3 CompassToFlatVector(ApproachCompass c)
    {
        return c switch
        {
            ApproachCompass.North => new Vector3(0f, 0f, 1f),
            ApproachCompass.South => new Vector3(0f, 0f, -1f),
            ApproachCompass.East => new Vector3(1f, 0f, 0f),
            ApproachCompass.West => new Vector3(-1f, 0f, 0f),
            _ => Vector3.forward,
        };
    }

    void LateUpdate()
    {
        AutoRecoverStaleInsideState();
        SyncSignalCompleteUiIfNeeded();
        ApplyDelayedSignalCompletionIfDue();
        RefreshOngoingSignalForPoliceUi();

        if (!_pendingResolutionAfterEnter)
            return;

        if (!_playerInsideMushiZone)
        {
            _pendingResolutionAfterEnter = false;
            return;
        }

        if (ViolationTimes.SignalViolationComplete && !keepPoliceCatchAfterObjectiveComplete)
        {
            _pendingResolutionAfterEnter = false;
            return;
        }

        _pendingResolutionAfterEnter = false;
        _strikeRegisteredForCurrentInside = true;

        bool policeSeesPlayerOnMushiFrame = PoliceLineOfSightState.IsTargetInPoliceSightNow;

        if (!ViolationTimes.SignalViolationComplete)
        {
            if (signalViolationStrikesBeforeObjectiveComplete <= 1)
            {
                ViolationTimes.NotifySignalViolationComplete();
                ApplyViolationUiAllTargets(
                    completedWhenNotSpottedLabel,
                    completedWhenNotSpottedTextColor,
                    signalViolationCompleteTextColor,
                    additionalNotSpottedTextColor);
            }
            else
            {
                if (_strikesRemaining < 0)
                    _strikesRemaining = signalViolationStrikesBeforeObjectiveComplete;

                int displayCount = _strikesRemaining;
                _strikesRemaining--;
                string progress = string.Format(signalViolationRemainingCountFormat, displayCount);
                ApplyViolationUiAllTargets(
                    progress,
                    completedWhenNotSpottedTextColor,
                    completedWhenNotSpottedTextColor,
                    additionalNotSpottedTextColor);

                if (_strikesRemaining <= 0)
                {
                    // 同フレームで「残り1回」を「完了」へ上書きせず、保持時間のあとに切替える。
                    _signalCompletePending = true;
                    _signalCompleteUiDelayUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, signalCompleteUiHoldSeconds);
                }
            }
        }

        if (showCatchUiWhenPoliceSeePlayerOnMushiComplete && policeSeesPlayerOnMushiFrame)
        {
            if (PoliceLineOfSightCatch.IsCatchUiBusyNow())
            {
                BeginMushiStrikeCooldown();
                return;
            }
            ViolationFineAmountDisplay.SetFineText(
                fineAmountText,
                fineAmountDisplayTmp,
                fineAmountDisplayUi,
                additionalFineAmountTmp,
                additionalFineAmountUi);
            PlayerViolationState.NotifySignalViolationMoment(1f);
            PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind.Signal);
        }

        BeginMushiStrikeCooldown();
    }

    void AutoRecoverStaleInsideState()
    {
        if (!_playerInsideMushiZone)
            return;

        float timeout = Mathf.Max(0.05f, insideStateStaleTimeoutSeconds);
        if (Time.unscaledTime - _lastPlayerTriggerSeenUnscaled <= timeout)
            return;

        _activeCrosswalkViolatingSides.Clear();
        SetInsideState(false);
    }

    void SyncSignalCompleteUiIfNeeded()
    {
        if (ViolationTimes.SignalViolationComplete)
        {
            if (_signalCompleteUiApplied)
                return;

            ApplyViolationUiAllTargets(
                signalViolationCompleteLabel,
                completedWhenNotSpottedTextColor,
                signalViolationCompleteTextColor,
                additionalNotSpottedTextColor);
            _signalCompleteUiApplied = true;
            return;
        }

        _signalCompleteUiApplied = false;
        if (_strikesRemaining <= 0 && !_signalCompletePending)
            _strikesRemaining = -1;
    }

    /// <summary>
    /// 最終ストライクで「残り1回」を見せたあと、保持時間が経過してから
    /// 「完了」UI と <see cref="ViolationTimes.NotifySignalViolationComplete"/> を発火する。
    /// 同一フレーム内で残り表示が即上書きされる問題への対策。
    /// </summary>
    void ApplyDelayedSignalCompletionIfDue()
    {
        if (!_signalCompletePending)
            return;

        if (Time.unscaledTime < _signalCompleteUiDelayUntilUnscaled)
            return;

        _signalCompletePending = false;

        // すでに ViolationTimes が完了済み（他経路で先行）なら UI 反映だけ任せて重複通知を避ける。
        if (ViolationTimes.SignalViolationComplete)
        {
            _signalCompleteUiApplied = true;
            return;
        }

        ApplyViolationUiAllTargets(
            signalViolationCompleteLabel,
            completedWhenNotSpottedTextColor,
            signalViolationCompleteTextColor,
            additionalNotSpottedTextColor);
        ViolationTimes.NotifySignalViolationComplete();
        _signalCompleteUiApplied = true;
    }

    void BeginMushiStrikeCooldown()
    {
        float d = Mathf.Max(0f, signalViolationCooldownSecondsAfterStrike);
        _mushiStrikeCooldownUntilUnscaled = Time.unscaledTime + d;
    }

    /// <summary>
    /// Lets <see cref="PlayerViolationState.IsViolatingNow"/> stay true while in red + crosswalk zone (even after objective complete),
    /// so UI / police logic that reads aggregated violation can still react.
    /// </summary>
    void RefreshOngoingSignalForPoliceUi()
    {
        if (_useCrosswalkMode)
        {
            if (crosswalkTraffic == null)
            {
                PlayerViolationState.ReportOngoingSignalMushi(false);
                return;
            }

            bool ongoing = _playerInsideMushiZone && IsActiveSignalViolationNow();
            PlayerViolationState.ReportOngoingSignalMushi(ongoing);
            return;
        }

        if (shinngoukired != null && _legacyDecisionCollider != null)
        {
            bool ongoing = _playerInsideMushiZone && shinngoukired.State != 1;
            PlayerViolationState.ReportOngoingSignalMushi(ongoing);
        }
        else
        {
            PlayerViolationState.ReportOngoingSignalMushi(false);
        }
    }

    void ApplyViolationUiAllTargets(
        string text,
        Color completedGroupColor,
        Color signalCompleteSlotColor,
        Color additionalGroupColor)
    {
        if (completedWhenNotSpottedTmp != null)
        {
            completedWhenNotSpottedTmp.text = text;
            completedWhenNotSpottedTmp.color = completedGroupColor;
        }

        if (completedWhenNotSpottedUi != null)
        {
            completedWhenNotSpottedUi.text = text;
            completedWhenNotSpottedUi.color = completedGroupColor;
        }

        if (signalViolationCompleteTmp != null)
        {
            signalViolationCompleteTmp.text = text;
            signalViolationCompleteTmp.color = signalCompleteSlotColor;
        }

        if (signalViolationCompleteUi != null)
        {
            signalViolationCompleteUi.text = text;
            signalViolationCompleteUi.color = signalCompleteSlotColor;
        }

        if (additionalTmpTurnRedOnComplete != null)
        {
            foreach (var t in additionalTmpTurnRedOnComplete)
            {
                if (t != null)
                {
                    t.text = text;
                    t.color = completedGroupColor;
                }
            }
        }

        if (additionalUiTurnRedOnComplete != null)
        {
            foreach (var t in additionalUiTurnRedOnComplete)
            {
                if (t != null)
                {
                    t.text = text;
                    t.color = completedGroupColor;
                }
            }
        }

        if (additionalTmpOnNotSpottedComplete != null)
        {
            foreach (var t in additionalTmpOnNotSpottedComplete)
            {
                if (t != null)
                {
                    t.text = text;
                    t.color = additionalGroupColor;
                }
            }
        }

        if (additionalUiOnNotSpottedComplete != null)
        {
            foreach (var t in additionalUiOnNotSpottedComplete)
            {
                if (t != null)
                {
                    t.text = text;
                    t.color = additionalGroupColor;
                }
            }
        }
    }
}

/// <summary>Forwards trigger events from a crosswalk block to ShingouMushi.</summary>
[DisallowMultipleComponent]
public class ShingouMushiCrosswalkRelay : MonoBehaviour
{
    ShingouMushi _owner;
    CrosswalkFourWayTrafficController.CrosswalkSide _side;

    public void Initialize(ShingouMushi owner, CrosswalkFourWayTrafficController.CrosswalkSide side)
    {
        _owner = owner;
        _side = side;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_owner != null)
            _owner.OnCrosswalkTriggerEnter(_side, other);
    }

    void OnTriggerExit(Collider other)
    {
        if (_owner != null)
            _owner.OnCrosswalkTriggerExit(_side, other);
    }

    void OnTriggerStay(Collider other)
    {
        if (_owner != null)
            _owner.OnCrosswalkTriggerStay(_side, other);
    }
}
