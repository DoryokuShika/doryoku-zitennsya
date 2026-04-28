using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 警告 UI・一時停止などの実行部。視界の数値は <see cref="PoliceTargetLineOfSightProbe"/>、
/// 視界の集約は <see cref="PoliceLineOfSightState"/>。
/// 「違反中かつ視界内」は各違反スクリプトが照合して <see cref="RequestTryCatchWhenViolationVisibleToPolice"/> を呼びます。
/// 「違反中のみ警告」がオフのときだけ、ここで視界のみの自動捕獲を行います。
/// </summary>
[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
public class PoliceLineOfSightCatch : MonoBehaviour
{
    static PoliceLineOfSightCatch _activeInstance;
    public static event Action<PoliceCatchViolationKind> PoliceCatchShown;
    public static event Action PoliceCatchClosed;

    /// <summary>
    /// <see cref="PatrolWaypointsBranchRandom"/> が Request 直前に渡す反則金。Catch で 1 回読んだら消費します。
    /// （Catch の catchPedestrianBellFineAmountText より優先し、未設定ならそちらへフォールバック）
    /// </summary>
    static string _pendingPedestrianBellFineFromPatrol;

    /// <summary>
    /// <see cref="UnlitBicyclePoliceWarningMark"/> が Request 直前に渡す反則金。Catch で 1 回読んだら消費します。
    /// （catchUnlitBicycleFineAmountText より優先し、未設定ならそちらへフォールバック）
    /// </summary>
    static string _pendingUnlitBicycleFineFromMonitor;
    sealed class CatchRequestOverrides
    {
        public bool SuppressPoliceCatchShownEvent;
        public bool SkipGlobalSightCheck;
        public AudioClip SfxFirst;
        public AudioClip SfxSecond;
        public float? SfxVolume;
        public string HeaderMessage;
        public string FineAmountText;
    }

    /// <summary>歩行者ベル退避から、次の PedestrianBell の Catch で使う反則金文字列を登録します。空白なら登録しない。</summary>
    public static void NotifyPedestrianBellFineFromPatrolForNextCatch(string fineText)
    {
        _pendingPedestrianBellFineFromPatrol = string.IsNullOrWhiteSpace(fineText) ? null : fineText.Trim();
    }

    /// <summary>無灯火監視から、次の UnlitBicycleAtNight の Catch で使う反則金文字列を登録します。空白なら登録しない。</summary>
    public static void NotifyUnlitBicycleFineForNextCatch(string fineText)
    {
        _pendingUnlitBicycleFineFromMonitor = string.IsNullOrWhiteSpace(fineText) ? null : fineText.Trim();
    }

    static void ClearPendingPedestrianBellFineFromPatrol()
    {
        _pendingPedestrianBellFineFromPatrol = null;
    }

    static void ClearPendingUnlitBicycleFineFromMonitor()
    {
        _pendingUnlitBicycleFineFromMonitor = null;
    }

    /// <summary>
    /// 1回の捕獲リクエストにだけ適用する上書き付き依頼。
    /// 偽警察のように「文言・SE・イベント抑止」を競合なしで適用したいときに使います。
    /// </summary>
    public static bool RequestTryCatchWithOverridesWhenViolationVisibleToPolice(
        PoliceCatchViolationKind violationKind,
        bool suppressPoliceCatchShownEvent,
        bool skipGlobalSightCheck,
        AudioClip sfxFirst,
        AudioClip sfxSecond,
        float sfxVolume,
        string headerMessageOverride,
        string fineAmountTextOverride)
    {
        if (_activeInstance == null)
            return false;

        var o = new CatchRequestOverrides
        {
            SuppressPoliceCatchShownEvent = suppressPoliceCatchShownEvent,
            SkipGlobalSightCheck = skipGlobalSightCheck,
            SfxFirst = sfxFirst,
            SfxSecond = sfxSecond,
            SfxVolume = Mathf.Clamp01(sfxVolume),
            HeaderMessage = string.IsNullOrWhiteSpace(headerMessageOverride) ? null : headerMessageOverride.Trim(),
            FineAmountText = string.IsNullOrWhiteSpace(fineAmountTextOverride) ? null : fineAmountTextOverride.Trim(),
        };
        return _activeInstance.TryCatchWhenViolationVisibleToPoliceInternal(violationKind, o);
    }

    [Header("対象")]
    [Tooltip("捕獲対象・Hub 付与先（自転車＝プレイヤーなど）。Probe の Target が空ならここから流し込みます。")]
    [SerializeField] Transform targetCharacter;
    [Tooltip("警告中に停止させる警察側のルート（パトカー親など）。未指定ならこのオブジェクト")]
    [SerializeField] Transform policeRootToFreeze;

    [Header("視界プローブ")]
    [Tooltip("未指定なら同じ GameObject から取得／なければ追加します。視野角・レイはプローブ側で設定します。")]
    [SerializeField] PoliceTargetLineOfSightProbe sightProbe;

    [Header("違反中のみ警告")]
    [Tooltip("オン: 逆走・歩道・信号・無灯火などが「自違反 ∧ 視界内」を照合して依頼したときだけ警告。オフ: 下記の自動判定（視界のみ）。")]
    [SerializeField] bool requireViolationStateToCatch = true;

    [Header("警告パネル（見つかったとき表示）")]
    [Tooltip("捕獲中だけ表示するルート（パネル・テキスト・戻るボタンの親）")]
    [SerializeField] GameObject catchUiRoot;
    [Tooltip("見つかったときの見出しなど（TMP）")]
    [SerializeField] TMP_Text violationMessageTmp;
    [Tooltip("見つかったときの見出しなど（uGUI Text）")]
    [SerializeField] Text violationMessageUiText;
    [FormerlySerializedAs("violationMessage")]
    [SerializeField] string messageOnCaught = "見つかりました。";

    [Header("違反内容テキスト（任意・未設定なら見出しに種別文を足す）")]
    [Tooltip("何の違反かを書き込む TMP。空なら violationMessage に見出し＋改行＋種別文をまとめて入れます。")]
    [SerializeField] TMP_Text violationKindDetailTmp;
    [Tooltip("何の違反かを書き込む uGUI Text")]
    [SerializeField] Text violationKindDetailUiText;
    [SerializeField] string messageWrongWayCaught = "逆走がばれました。";
    [SerializeField] string messageSidewalkCaught = "歩道走行がばれました。";
    [SerializeField] string messageSignalCaught = "信号無視がばれました。";
    [SerializeField] string messagePedestrianBellCaught =
        "\u6b69\u884c\u8005\u306b\u30d9\u30eb\u306a\u3089\u3057\u307e\u3057\u305f\u306d\uff1f";
    [SerializeField] string messageUnlitBicycleCaught = "\u591c\u9593\u306b\u706f\u706b\u304c\u6d88\u3048\u305f\u307e\u307e\u8d70\u884c\u304c\u3070\u308c\u307e\u3057\u305f\u3002";
    [Tooltip("違反中のみ警告がオフで視界だけ捕獲したとき、または種別なしで依頼されたときの種別欄用")]
    [SerializeField] string messageSightOnlyDetail = "";
    [Header("表示の再適用（テキストが切り替わらないとき）")]
    [Tooltip("catchUiRoot を有効化し onCaught の後にもう一度見出し・違反内容を書きます。子の OnEnable や他イベントで上書きされる場合に。")]
    [SerializeField] bool reapplyViolationMessagesAfterCatchUiShown = true;
    [Tooltip("上と同じタイミングで反則金テキストも書き直します。罰金額用 Text をここにも割り当てると確実です。")]
    [SerializeField] bool reapplyFineAmountAfterCatchUiShown = true;
    [SerializeField] string catchFineAmountText = "6000円";
    [Tooltip("PedestrianBell のときの反則金。PatrolWaypointsBranchRandom から非空白で渡された値が優先され、無ければこの欄、それも空なら catchFineAmountText。")]
    [SerializeField] string catchPedestrianBellFineAmountText = "3000\u5186";
    [Tooltip("UnlitBicycleAtNight のときの反則金。UnlitBicyclePoliceWarningMark から非空白で渡された値が優先され、無ければこの欄、それも空なら catchFineAmountText。")]
    [SerializeField] string catchUnlitBicycleFineAmountText = "5000\u5186";
    [SerializeField] TMP_Text catchFineAmountTmp;
    [SerializeField] Text catchFineAmountUi;
    [SerializeField] TMP_Text[] catchAdditionalFineTmp;
    [SerializeField] Text[] catchAdditionalFineUi;
    [Tooltip("警告パネル上に置いた戻るボタンをここに割り当てます。空のときは下の自動生成、または OnGUI のフォールバックになります。")]
    [FormerlySerializedAs("retryButton")]
    [SerializeField] Button backButton;
    [Tooltip("オン: Inspector で指定した戻るボタンを警告中に強制表示し、終了時に元の表示状態へ戻します。")]
    [SerializeField] bool forceShowAssignedBackButtonWhileCaught = true;
    [Tooltip("オン: パネルに手で配置した戻るボタンは、捕獲時にサイズ・フォントを書き換えません（割り当て時はオン推奨）。オフにすると enlargeBackButton が手動ボタンにも適用されます。名前が __AutoGeneratedPoliceBackButton__ の自動生成ボタンは常に enlarge の対象です。")]
    [SerializeField] bool respectAssignedBackButtonLayout = true;
    [Tooltip("戻るボタンを大きくします（uGUI）。respectAssignedBackButtonLayout がオンで手動ボタンのときは無視されます。")]
    [FormerlySerializedAs("enlargeRetryButton")]
    [SerializeField] bool enlargeBackButton = true;
    [FormerlySerializedAs("retryButtonFontSizeTmp")]
    [SerializeField] float backButtonFontSizeTmp = 40f;
    [FormerlySerializedAs("retryButtonFontSizeUi")]
    [SerializeField] float backButtonFontSizeUi = 36f;
    [FormerlySerializedAs("retryButtonMinSize")]
    [SerializeField] Vector2 backButtonMinSize = new Vector2(420f, 88f);

    [Header("戻るボタン自動生成（パネル内に配置）")]
    [Tooltip("オン: backButton 未割り当てのとき、catchUiRoot の子として戻るボタンを自動生成し、画面比率によらず常にパネル内の決まった位置に配置します。手動で backButton を割り当てている場合はこの生成は走りません。")]
    [SerializeField] bool autoCreateBackButtonInsidePanelIfMissing = true;
    [Tooltip("自動生成戻るボタンの幅・高さ（uGUI 単位）。enlargeBackButton と組み合わさるので、最低値はそちらに従います。")]
    [SerializeField] Vector2 autoBackButtonSize = new Vector2(420f, 96f);
    [Tooltip("catchUiRoot の下端中央を基準にした自動生成戻るボタンの位置オフセット (Y を上方向に取る)。例: (0,96) ならパネル下端から 96px 上にボタンの中心を置く。")]
    [SerializeField] Vector2 autoBackButtonAnchoredPositionFromBottomCenter = new Vector2(0f, 96f);
    [Tooltip("自動生成戻るボタンに表示するラベル文字列")]
    [SerializeField] string autoBackButtonLabel = "\u623B\u308B";
    [Tooltip("自動生成戻るボタンの背景色")]
    [SerializeField] Color autoBackButtonBackgroundColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("自動生成戻るボタンの文字色")]
    [SerializeField] Color autoBackButtonTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [Tooltip("自動生成戻るボタンに使う TMP フォント。null の場合は uGUI Text + 組み込みフォントにフォールバックします。")]
    [SerializeField] TMP_FontAsset autoBackButtonTmpFont;

    [Header("警告中だけ非表示（通常はゲーム用 Canvas ルートなど）")]
    [Tooltip("捕獲中だけ SetActive(false)。戻るで直前の表示状態に復元します。")]
    [SerializeField] GameObject[] hideWhileWarningActive;

    [Tooltip("戻る直後、この秒は再度捕獲しません")]
    [SerializeField] float sightResumeDelayAfterRetry = 1.5f;

    [Header("捕獲時 効果音（警告表示のタイミング）")]
    [Tooltip("オン: 先に1つ、続けて2つ目のSEを鳴らします。オフ: 何もしません。")]
    [SerializeField] bool playCatchSfx = true;
    [Tooltip("未指定ならこのコンポーネントと同じ GameObject から取得。なければ鳴しません。")]
    [SerializeField] AudioSource catchSfxSource;
    [Tooltip("1つ目のSE")]
    [SerializeField] AudioClip catchSfxFirst;
    [Tooltip("2つ目のSE（1つ目の再生後に鳴る）")]
    [SerializeField] AudioClip catchSfxSecond;
    [Range(0f, 1f)]
    [SerializeField] float catchSfxVolume = 1f;
    [Tooltip("1つ目のクリップの長さに加算する、2つ目までの待ち秒（0なら1つ目が終わってすぐ2つ目）")]
    [SerializeField] float catchSfxExtraDelayAfterFirstSeconds;

    [Header("捕獲時の挙動")]
    [SerializeField] bool stopTimerOnCatch = true;
    [SerializeField] bool unlockCursorOnCatch = true;
    [SerializeField] bool relockCursorOnRetry = true;
    [SerializeField] bool pauseCarAndWalkerMobsOnCatch = true;
    [SerializeField] bool resetTimeScaleOnRetry = true;
    [SerializeField] float timeScaleAfterRetry = 1f;
    [SerializeField] UnityEvent onCaught;
    [SerializeField] UnityEvent onRetry;

    [Header("デバッグ")]
    [SerializeField] bool debugLogRetryFlow;
    [SerializeField] string debugLogPrefix = "[PoliceCatch]";

    [Header("自動配線")]
    [FormerlySerializedAs("autoWireInPlayMode")]
    [SerializeField] bool autoWirePlayerInPlayMode = true;

    bool _caught;
    float _sightResumeUnscaledTime = float.NegativeInfinity;
    Coroutine _catchSfxRoutine;

    struct HiddenGoSnap
    {
        public GameObject Go;
        public bool WasActive;
    }

    readonly List<HiddenGoSnap> _hiddenWhileWarningSnaps = new List<HiddenGoSnap>();

    Vector2 _backButtonSizeDeltaOrig;
    float _backTmpFontOrig = -1f;
    float _backUiFontOrig = -1f;
    bool _backLayoutCached;
    bool _backButtonWasActiveBeforeCatch;
    GUIStyle _fallbackBackStyle;

    /// <summary>
    /// 逆走・歩道・信号などが「自違反かつ警察視界内」と判断したときに呼ぶ。シーンに1つのアクティブな Catch が反応します。
    /// </summary>
    public static void RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind violationKind)
    {
        if (_activeInstance == null)
            return;
        _activeInstance.TryCatchWhenViolationVisibleToPoliceInternal(violationKind, null);
    }

    /// <summary>
    /// 警告 UI 表示中、または戻る直後の再捕獲クールダウン中かどうか。
    /// 他の違反監視が fine 表示や再依頼を重ねるのを防ぐ共通ガードとして使う。
    /// </summary>
    public static bool IsCatchUiBusyNow()
    {
        return _activeInstance != null && _activeInstance.IsCatchUiBusyLocal();
    }

    bool IsCatchUiBusyLocal()
    {
        return _caught || Time.unscaledTime < _sightResumeUnscaledTime;
    }

    void OnEnable()
    {
        if (_activeInstance != null && _activeInstance != this)
            Debug.LogWarning("[PoliceLineOfSightCatch] 複数のアクティブインスタンスがあります。最後に有効化されたものが使用されます。", this);
        _activeInstance = this;
    }

    void OnDisable()
    {
        if (_activeInstance == this)
            _activeInstance = null;
    }

    void Awake()
    {
        if (Application.isPlaying && autoWirePlayerInPlayMode && targetCharacter == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                targetCharacter = p.transform;
        }

        if (policeRootToFreeze == null)
            policeRootToFreeze = transform;

        if (sightProbe == null)
            sightProbe = GetComponent<PoliceTargetLineOfSightProbe>();
        if (sightProbe == null)
            sightProbe = gameObject.AddComponent<PoliceTargetLineOfSightProbe>();
        sightProbe.SetTargetCharacterIfUnset(targetCharacter);

        if (Application.isPlaying && autoWirePlayerInPlayMode && targetCharacter != null &&
            targetCharacter.GetComponent<PlayerViolationStateHub>() == null)
            targetCharacter.gameObject.AddComponent<PlayerViolationStateHub>();

        if (catchUiRoot != null)
            catchUiRoot.SetActive(false);

        TryCreateInPanelBackButtonIfMissing();

        if (backButton != null)
        {
            // 同じ catchUiRoot を共有する別インスタンスが既に登録している可能性に備え、
            // 重複登録を避けるため一度外してから付け直す。
            backButton.onClick.RemoveListener(OnBackPressed);
            backButton.onClick.AddListener(OnBackPressed);
        }

        CacheBackButtonLayout();

        if (catchSfxSource == null)
            catchSfxSource = GetComponent<AudioSource>();
    }

    void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackPressed);
    }

    void LateUpdate()
    {
        PoliceLineOfSightState.ClearSightIfNoProbeUpdatedThisFrame();

        if (requireViolationStateToCatch)
            return;

        if (Time.unscaledTime < _sightResumeUnscaledTime)
            return;

        if (_caught || targetCharacter == null)
            return;

        if (!PoliceLineOfSightState.IsTargetInPoliceSightNow)
            return;

        Catch(PoliceCatchViolationKind.None, null);
    }

    bool TryCatchWhenViolationVisibleToPoliceInternal(PoliceCatchViolationKind violationKind, CatchRequestOverrides requestOverrides)
    {
        if (Time.unscaledTime < _sightResumeUnscaledTime)
        {
            ClearPendingPedestrianBellFineIfKind(violationKind);
            ClearPendingUnlitBicycleFineIfKind(violationKind);
            return false;
        }

        if (_caught || targetCharacter == null)
        {
            ClearPendingPedestrianBellFineIfKind(violationKind);
            ClearPendingUnlitBicycleFineIfKind(violationKind);
            return false;
        }

        if ((requestOverrides == null || !requestOverrides.SkipGlobalSightCheck) &&
            !PoliceLineOfSightState.IsTargetInPoliceSightNow)
        {
            ClearPendingPedestrianBellFineIfKind(violationKind);
            ClearPendingUnlitBicycleFineIfKind(violationKind);
            return false;
        }

        Catch(violationKind, requestOverrides);
        return true;
    }

    void ClearPendingPedestrianBellFineIfKind(PoliceCatchViolationKind violationKind)
    {
        if (violationKind == PoliceCatchViolationKind.PedestrianBell)
            ClearPendingPedestrianBellFineFromPatrol();
    }

    void ClearPendingUnlitBicycleFineIfKind(PoliceCatchViolationKind violationKind)
    {
        if (violationKind == PoliceCatchViolationKind.UnlitBicycleAtNight)
            ClearPendingUnlitBicycleFineFromMonitor();
    }

    void OnGUI()
    {
        if (!_caught || backButton != null)
            return;

        if (_fallbackBackStyle == null)
        {
            _fallbackBackStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(Mathf.Max(backButtonFontSizeUi, 28f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        const float w = 480f;
        const float h = 96f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.58f, w, h);
        if (GUI.Button(r, "戻る", _fallbackBackStyle))
            OnBackPressed();
    }

    void Catch(PoliceCatchViolationKind violationKind, CatchRequestOverrides requestOverrides)
    {
        if (_caught)
            return;
        _caught = true;

        if (violationKind != PoliceCatchViolationKind.PedestrianBell)
            ClearPendingPedestrianBellFineFromPatrol();

        if (violationKind != PoliceCatchViolationKind.UnlitBicycleAtNight)
            ClearPendingUnlitBicycleFineFromMonitor();

        if (pauseCarAndWalkerMobsOnCatch)
        {
            Transform police = policeRootToFreeze != null ? policeRootToFreeze : transform;
            MobTrafficPause.FreezeForPoliceCatch(targetCharacter, police);
        }

        if (stopTimerOnCatch)
            Timer.isRunning = false;

        if (unlockCursorOnCatch)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        string headingText = requestOverrides != null && !string.IsNullOrEmpty(requestOverrides.HeaderMessage)
            ? requestOverrides.HeaderMessage
            : null;
        string fineText = ResolveFineAmountTextForCatch(violationKind, requestOverrides);

        ApplyMessageToUi(violationKind, headingText);
        ApplyHideWhileWarning();
        StyleBackButtonForCatch();
        RememberAndForceShowBackButtonIfNeeded();

        if (catchUiRoot != null)
            EnsureAncestorsActive(catchUiRoot.transform);
        if (catchUiRoot != null)
            catchUiRoot.SetActive(true);

        onCaught?.Invoke();
        if (requestOverrides == null || !requestOverrides.SuppressPoliceCatchShownEvent)
            PoliceCatchShown?.Invoke(violationKind);

        AudioClip firstClip = catchSfxFirst;
        AudioClip secondClip = catchSfxSecond;
        float sfxVolume = catchSfxVolume;
        if (requestOverrides != null)
        {
            firstClip = requestOverrides.SfxFirst;
            secondClip = requestOverrides.SfxSecond;
            if (requestOverrides.SfxVolume.HasValue)
                sfxVolume = requestOverrides.SfxVolume.Value;
        }

        if (playCatchSfx && (firstClip != null || secondClip != null))
        {
            if (_catchSfxRoutine != null)
            {
                StopCoroutine(_catchSfxRoutine);
                _catchSfxRoutine = null;
            }
            _catchSfxRoutine = StartCoroutine(PlayCatchSfxSequence(firstClip, secondClip, sfxVolume));
        }

        if (reapplyViolationMessagesAfterCatchUiShown)
            ApplyMessageToUi(violationKind, headingText);
        if (reapplyFineAmountAfterCatchUiShown)
        {
            ViolationFineAmountDisplay.SetFineText(
                fineText,
                catchFineAmountTmp,
                catchFineAmountUi,
                catchAdditionalFineTmp,
                catchAdditionalFineUi);
        }

        if (violationKind == PoliceCatchViolationKind.PedestrianBell)
            ClearPendingPedestrianBellFineFromPatrol();

        if (violationKind == PoliceCatchViolationKind.UnlitBicycleAtNight)
            ClearPendingUnlitBicycleFineFromMonitor();

        if (debugLogRetryFlow)
            Debug.Log($"{debugLogPrefix} Catch: 警告 UI 表示、hide 数={_hiddenWhileWarningSnaps.Count}", this);
    }

    static void EnsureAncestorsActive(Transform leaf)
    {
        if (leaf == null)
            return;
        var chain = new List<Transform>();
        Transform t = leaf;
        while (t != null)
        {
            chain.Add(t);
            t = t.parent;
        }
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i] != null && !chain[i].gameObject.activeSelf)
                chain[i].gameObject.SetActive(true);
        }
    }

    string ResolveFineAmountTextForCatch(PoliceCatchViolationKind violationKind, CatchRequestOverrides requestOverrides)
    {
        if (requestOverrides != null && !string.IsNullOrEmpty(requestOverrides.FineAmountText))
            return requestOverrides.FineAmountText;
        return FineAmountTextForKind(violationKind);
    }

    void ApplyMessageToUi(PoliceCatchViolationKind violationKind, string headingOverride = null)
    {
        string heading = string.IsNullOrEmpty(headingOverride) ? messageOnCaught : headingOverride;
        string detail = GetDetailMessageForKind(violationKind);
        bool hasDetailSlot = violationKindDetailTmp != null || violationKindDetailUiText != null;

        if (hasDetailSlot)
        {
            if (violationMessageTmp != null)
                violationMessageTmp.text = heading;
            if (violationMessageUiText != null)
                violationMessageUiText.text = heading;
            if (violationKindDetailTmp != null)
                violationKindDetailTmp.text = detail;
            if (violationKindDetailUiText != null)
                violationKindDetailUiText.text = detail;
        }
        else
        {
            string combined = string.IsNullOrEmpty(detail)
                ? heading
                : $"{heading}\n{detail}";
            if (violationMessageTmp != null)
                violationMessageTmp.text = combined;
            if (violationMessageUiText != null)
                violationMessageUiText.text = combined;
        }
    }

    string GetDetailMessageForKind(PoliceCatchViolationKind kind)
    {
        return kind switch
        {
            PoliceCatchViolationKind.WrongWay => messageWrongWayCaught,
            PoliceCatchViolationKind.Sidewalk => messageSidewalkCaught,
            PoliceCatchViolationKind.Signal => messageSignalCaught,
            PoliceCatchViolationKind.PedestrianBell => messagePedestrianBellCaught,
            PoliceCatchViolationKind.UnlitBicycleAtNight => messageUnlitBicycleCaught,
            _ => messageSightOnlyDetail,
        };
    }

    string FineAmountTextForKind(PoliceCatchViolationKind kind)
    {
        if (kind == PoliceCatchViolationKind.PedestrianBell)
        {
            if (!string.IsNullOrEmpty(_pendingPedestrianBellFineFromPatrol))
            {
                string s = _pendingPedestrianBellFineFromPatrol;
                _pendingPedestrianBellFineFromPatrol = null;
                return s;
            }

            if (!string.IsNullOrWhiteSpace(catchPedestrianBellFineAmountText))
                return catchPedestrianBellFineAmountText;
            return catchFineAmountText;
        }

        if (kind == PoliceCatchViolationKind.UnlitBicycleAtNight)
        {
            if (!string.IsNullOrEmpty(_pendingUnlitBicycleFineFromMonitor))
            {
                string s = _pendingUnlitBicycleFineFromMonitor;
                _pendingUnlitBicycleFineFromMonitor = null;
                return s;
            }

            if (!string.IsNullOrWhiteSpace(catchUnlitBicycleFineAmountText))
                return catchUnlitBicycleFineAmountText;
            return catchFineAmountText;
        }

        return catchFineAmountText;
    }

    void CacheBackButtonLayout()
    {
        if (backButton == null || _backLayoutCached)
            return;

        var rt = backButton.GetComponent<RectTransform>();
        if (rt != null)
            _backButtonSizeDeltaOrig = rt.sizeDelta;

        foreach (var tmp in backButton.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp != null && !IsCatchPanelDedicatedTmp(tmp))
            {
                _backTmpFontOrig = tmp.fontSize;
                break;
            }
        }

        foreach (var ui in backButton.GetComponentsInChildren<Text>(true))
        {
            if (ui != null && !IsCatchPanelDedicatedUiText(ui))
            {
                _backUiFontOrig = ui.fontSize;
                break;
            }
        }

        _backLayoutCached = true;
    }

    bool IsCatchPanelDedicatedTmp(TMP_Text t)
    {
        if (t == null)
            return false;
        if (t == violationMessageTmp || t == violationKindDetailTmp || t == catchFineAmountTmp)
            return true;
        if (catchAdditionalFineTmp != null)
        {
            for (int i = 0; i < catchAdditionalFineTmp.Length; i++)
            {
                if (catchAdditionalFineTmp[i] == t)
                    return true;
            }
        }
        return false;
    }

    bool IsCatchPanelDedicatedUiText(Text t)
    {
        if (t == null)
            return false;
        if (t == violationMessageUiText || t == violationKindDetailUiText || t == catchFineAmountUi)
            return true;
        if (catchAdditionalFineUi != null)
        {
            for (int i = 0; i < catchAdditionalFineUi.Length; i++)
            {
                if (catchAdditionalFineUi[i] == t)
                    return true;
            }
        }
        return false;
    }

    bool IsAutoGeneratedRuntimeBackButton()
    {
        return backButton != null &&
               string.Equals(backButton.gameObject.name, AutoBackButtonHierarchyName, StringComparison.Ordinal);
    }

    void StyleBackButtonForCatch()
    {
        if (backButton == null)
            return;

        if (respectAssignedBackButtonLayout && !IsAutoGeneratedRuntimeBackButton())
            return;

        if (!enlargeBackButton)
            return;

        CacheBackButtonLayout();

        var rt = backButton.GetComponent<RectTransform>();
        if (rt != null)
        {
            float w = Mathf.Max(backButtonMinSize.x, _backButtonSizeDeltaOrig.x);
            float h = Mathf.Max(backButtonMinSize.y, _backButtonSizeDeltaOrig.y);
            rt.sizeDelta = new Vector2(w, h);
        }

        foreach (var tmp in backButton.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp != null && !IsCatchPanelDedicatedTmp(tmp))
                tmp.fontSize = backButtonFontSizeTmp;
        }

        foreach (var ui in backButton.GetComponentsInChildren<Text>(true))
        {
            if (ui != null && !IsCatchPanelDedicatedUiText(ui))
                ui.fontSize = Mathf.RoundToInt(backButtonFontSizeUi);
        }
    }

    void RestoreBackButtonLayout()
    {
        if (!enlargeBackButton || backButton == null || !_backLayoutCached)
            return;

        var rt = backButton.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = _backButtonSizeDeltaOrig;

        foreach (var tmp in backButton.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp != null && _backTmpFontOrig > 0f && !IsCatchPanelDedicatedTmp(tmp))
                tmp.fontSize = _backTmpFontOrig;
        }

        foreach (var ui in backButton.GetComponentsInChildren<Text>(true))
        {
            if (ui != null && _backUiFontOrig > 0f && !IsCatchPanelDedicatedUiText(ui))
                ui.fontSize = Mathf.RoundToInt(_backUiFontOrig);
        }
    }

    void RememberAndForceShowBackButtonIfNeeded()
    {
        _backButtonWasActiveBeforeCatch = false;
        if (backButton == null)
            return;

        var go = backButton.gameObject;
        if (go == null)
            return;

        _backButtonWasActiveBeforeCatch = go.activeSelf;
        if (forceShowAssignedBackButtonWhileCaught && !go.activeSelf)
            go.SetActive(true);
    }

    void RestoreBackButtonActiveIfNeeded()
    {
        if (!forceShowAssignedBackButtonWhileCaught || backButton == null)
            return;

        var go = backButton.gameObject;
        if (go == null)
            return;

        go.SetActive(_backButtonWasActiveBeforeCatch);
    }

    void ApplyHideWhileWarning()
    {
        _hiddenWhileWarningSnaps.Clear();
        if (hideWhileWarningActive == null)
            return;

        for (int i = 0; i < hideWhileWarningActive.Length; i++)
        {
            var go = hideWhileWarningActive[i];
            if (go == null)
                continue;
            _hiddenWhileWarningSnaps.Add(new HiddenGoSnap { Go = go, WasActive = go.activeSelf });
            go.SetActive(false);
        }
    }

    void RestoreHideWhileWarning()
    {
        for (int i = 0; i < _hiddenWhileWarningSnaps.Count; i++)
        {
            var s = _hiddenWhileWarningSnaps[i];
            if (s.Go != null)
                s.Go.SetActive(s.WasActive);
        }

        _hiddenWhileWarningSnaps.Clear();
    }

    void OnBackPressed()
    {
        if (!_caught)
            return;

        if (_catchSfxRoutine != null)
        {
            StopCoroutine(_catchSfxRoutine);
            _catchSfxRoutine = null;
        }

        if (resetTimeScaleOnRetry)
            Time.timeScale = timeScaleAfterRetry;

        MobTrafficPause.UnfreezeCarAndWalkerMobs();

        if (catchUiRoot != null)
            catchUiRoot.SetActive(false);

        RestoreHideWhileWarning();
        RestoreBackButtonLayout();
        RestoreBackButtonActiveIfNeeded();

        if (relockCursorOnRetry)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Timer.isRunning = true;
        _caught = false;
        _sightResumeUnscaledTime = Time.unscaledTime + Mathf.Max(0f, sightResumeDelayAfterRetry);

        onRetry?.Invoke();
        PoliceCatchClosed?.Invoke();

        if (debugLogRetryFlow)
            Debug.Log($"{debugLogPrefix} 戻る: 警告終了", this);
    }

    IEnumerator PlayCatchSfxSequence(AudioClip firstClip, AudioClip secondClip, float volume)
    {
        if (catchSfxSource == null)
            catchSfxSource = GetComponent<AudioSource>();
        if (catchSfxSource == null)
        {
            if (debugLogRetryFlow)
                Debug.LogWarning($"{debugLogPrefix} 効果音: AudioSource がありません", this);
            _catchSfxRoutine = null;
            yield break;
        }

        if (firstClip != null)
        {
            catchSfxSource.PlayOneShot(firstClip, volume);
            float wait = Mathf.Max(0f, firstClip.length) + Mathf.Max(0f, catchSfxExtraDelayAfterFirstSeconds);
            yield return new WaitForSecondsRealtime(wait);
        }
        else if (secondClip != null && catchSfxExtraDelayAfterFirstSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(catchSfxExtraDelayAfterFirstSeconds);
        }

        if (secondClip != null)
            catchSfxSource.PlayOneShot(secondClip, volume);

        _catchSfxRoutine = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPedestrianBellFineStaticForEnterPlayMode()
    {
        _pendingPedestrianBellFineFromPatrol = null;
        _pendingUnlitBicycleFineFromMonitor = null;
    }

    /// <summary>
    /// 自動生成戻るボタンを catchUiRoot 内に作成する際に使うヒエラルキー名。
    /// 同じ catchUiRoot に複数インスタンスが同時に作るのを防ぐためのマーカー。
    /// </summary>
    const string AutoBackButtonHierarchyName = "__AutoGeneratedPoliceBackButton__";

    /// <summary>
    /// backButton 未割り当てのとき、catchUiRoot の子として戻るボタンを自動生成し、
    /// アンカーで下端中央に固定して画面比率によらず常にパネル内に表示されるようにする。
    /// 同じ catchUiRoot 上に既に自動生成ボタンが存在する場合はそれを再利用する。
    /// </summary>
    void TryCreateInPanelBackButtonIfMissing()
    {
        if (backButton != null)
            return;
        if (!autoCreateBackButtonInsidePanelIfMissing)
            return;
        if (catchUiRoot == null)
            return;

        // 別インスタンスが既に同じ catchUiRoot に作成しているなら、それを再利用する。
        Transform existingChild = catchUiRoot.transform.Find(AutoBackButtonHierarchyName);
        if (existingChild != null)
        {
            Button existingBtn = existingChild.GetComponent<Button>();
            if (existingBtn != null)
            {
                backButton = existingBtn;
                return;
            }
        }

        // ボタン本体を作成
        var btnGo = new GameObject(AutoBackButtonHierarchyName, typeof(RectTransform));
        btnGo.transform.SetParent(catchUiRoot.transform, false);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = autoBackButtonSize;
        rt.anchoredPosition = autoBackButtonAnchoredPositionFromBottomCenter;
        rt.localScale = Vector3.one;

        var img = btnGo.AddComponent<Image>();
        img.color = autoBackButtonBackgroundColor;
        img.raycastTarget = true;

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;

        // ラベル: TMP フォント指定があれば TMP、なければ uGUI Text
        if (autoBackButtonTmpFont != null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(btnGo.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = autoBackButtonLabel;
            tmp.font = autoBackButtonTmpFont;
            tmp.color = autoBackButtonTextColor;
            tmp.fontSize = Mathf.Max(20f, backButtonFontSizeTmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = Mathf.Max(20f, backButtonFontSizeTmp);
            tmp.raycastTarget = false;
        }
        else
        {
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(btnGo.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var txt = labelGo.AddComponent<Text>();
            txt.text = autoBackButtonLabel;
            txt.color = autoBackButtonTextColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = Mathf.Max(20, Mathf.RoundToInt(backButtonFontSizeUi));
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 14;
            txt.resizeTextMaxSize = Mathf.Max(20, Mathf.RoundToInt(backButtonFontSizeUi));
            txt.raycastTarget = false;

            // 組み込みフォント（Unity バージョン差を吸収）
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null)
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null)
                txt.font = f;
        }

        backButton = btn;
    }
}
