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

    [Header("対象")]
    [Tooltip("捕獲対象・Hub 付与先（自転車＝プレイヤーなど）。Probe の Target が空ならここから流し込みます。")]
    [SerializeField] Transform targetCharacter;
    [Tooltip("警告中に停止させる警察側のルート（パトカー親など）。未指定ならこのオブジェクト")]
    [SerializeField] Transform policeRootToFreeze;

    [Header("視界プローブ")]
    [Tooltip("未指定なら同じ GameObject から取得／なければ追加します。視野角・レイはプローブ側で設定します。")]
    [SerializeField] PoliceTargetLineOfSightProbe sightProbe;

    [Header("違反中のみ警告")]
    [Tooltip("オン: 逆走・歩道・信号の各スクリプトが「自違反 ∧ 視界内」を照合して依頼したときだけ警告。オフ: 下記の自動判定（視界のみ）。")]
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
    [Tooltip("違反中のみ警告がオフで視界だけ捕獲したとき、または種別なしで依頼されたときの種別欄用")]
    [SerializeField] string messageSightOnlyDetail = "";
    [Header("表示の再適用（テキストが切り替わらないとき）")]
    [Tooltip("catchUiRoot を有効化し onCaught の後にもう一度見出し・違反内容を書きます。子の OnEnable や他イベントで上書きされる場合に。")]
    [SerializeField] bool reapplyViolationMessagesAfterCatchUiShown = true;
    [Tooltip("上と同じタイミングで反則金テキストも書き直します。罰金額用 Text をここにも割り当てると確実です。")]
    [SerializeField] bool reapplyFineAmountAfterCatchUiShown = true;
    [SerializeField] string catchFineAmountText = "6000円";
    [SerializeField] TMP_Text catchFineAmountTmp;
    [SerializeField] Text catchFineAmountUi;
    [SerializeField] TMP_Text[] catchAdditionalFineTmp;
    [SerializeField] Text[] catchAdditionalFineUi;
    [FormerlySerializedAs("retryButton")]
    [SerializeField] Button backButton;
    [Tooltip("戻るボタンを大きくします（uGUI）")]
    [FormerlySerializedAs("enlargeRetryButton")]
    [SerializeField] bool enlargeBackButton = true;
    [FormerlySerializedAs("retryButtonFontSizeTmp")]
    [SerializeField] float backButtonFontSizeTmp = 40f;
    [FormerlySerializedAs("retryButtonFontSizeUi")]
    [SerializeField] float backButtonFontSizeUi = 36f;
    [FormerlySerializedAs("retryButtonMinSize")]
    [SerializeField] Vector2 backButtonMinSize = new Vector2(420f, 88f);

    [Header("警告中だけ非表示（通常はゲーム用 Canvas ルートなど）")]
    [Tooltip("捕獲中だけ SetActive(false)。戻るで直前の表示状態に復元します。")]
    [SerializeField] GameObject[] hideWhileWarningActive;

    [Tooltip("戻る直後、この秒は再度捕獲しません")]
    [SerializeField] float sightResumeDelayAfterRetry = 1.5f;

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
    GUIStyle _fallbackBackStyle;

    /// <summary>
    /// 逆走・歩道・信号などが「自違反かつ警察視界内」と判断したときに呼ぶ。シーンに1つのアクティブな Catch が反応します。
    /// </summary>
    public static void RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind violationKind)
    {
        if (_activeInstance == null)
            return;
        _activeInstance.TryCatchWhenViolationVisibleToPoliceInternal(violationKind);
    }

    void OnEnable()
    {
        if (_activeInstance == null)
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

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        CacheBackButtonLayout();
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

        Catch(PoliceCatchViolationKind.None);
    }

    void TryCatchWhenViolationVisibleToPoliceInternal(PoliceCatchViolationKind violationKind)
    {
        if (Time.unscaledTime < _sightResumeUnscaledTime)
            return;

        if (_caught || targetCharacter == null)
            return;

        if (!PoliceLineOfSightState.IsTargetInPoliceSightNow)
            return;

        Catch(violationKind);
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

    void Catch(PoliceCatchViolationKind violationKind)
    {
        if (_caught)
            return;
        _caught = true;

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

        ApplyMessageToUi(violationKind);
        ApplyHideWhileWarning();
        StyleBackButtonForCatch();

        if (catchUiRoot != null)
            EnsureAncestorsActive(catchUiRoot.transform);
        if (catchUiRoot != null)
            catchUiRoot.SetActive(true);

        onCaught?.Invoke();

        if (reapplyViolationMessagesAfterCatchUiShown)
            ApplyMessageToUi(violationKind);
        if (reapplyFineAmountAfterCatchUiShown)
        {
            ViolationFineAmountDisplay.SetFineText(
                catchFineAmountText,
                catchFineAmountTmp,
                catchFineAmountUi,
                catchAdditionalFineTmp,
                catchAdditionalFineUi);
        }

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

    void ApplyMessageToUi(PoliceCatchViolationKind violationKind)
    {
        string detail = GetDetailMessageForKind(violationKind);
        bool hasDetailSlot = violationKindDetailTmp != null || violationKindDetailUiText != null;

        if (hasDetailSlot)
        {
            if (violationMessageTmp != null)
                violationMessageTmp.text = messageOnCaught;
            if (violationMessageUiText != null)
                violationMessageUiText.text = messageOnCaught;
            if (violationKindDetailTmp != null)
                violationKindDetailTmp.text = detail;
            if (violationKindDetailUiText != null)
                violationKindDetailUiText.text = detail;
        }
        else
        {
            string combined = string.IsNullOrEmpty(detail)
                ? messageOnCaught
                : $"{messageOnCaught}\n{detail}";
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
            _ => messageSightOnlyDetail,
        };
    }

    void CacheBackButtonLayout()
    {
        if (backButton == null || _backLayoutCached)
            return;

        var rt = backButton.GetComponent<RectTransform>();
        if (rt != null)
            _backButtonSizeDeltaOrig = rt.sizeDelta;

        var tmp = backButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            _backTmpFontOrig = tmp.fontSize;

        var ui = backButton.GetComponentInChildren<Text>(true);
        if (ui != null)
            _backUiFontOrig = ui.fontSize;

        _backLayoutCached = true;
    }

    void StyleBackButtonForCatch()
    {
        if (!enlargeBackButton || backButton == null)
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
            if (tmp != null)
                tmp.fontSize = backButtonFontSizeTmp;
        }

        foreach (var ui in backButton.GetComponentsInChildren<Text>(true))
        {
            if (ui != null)
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
            if (tmp != null && _backTmpFontOrig > 0f)
                tmp.fontSize = _backTmpFontOrig;
        }

        foreach (var ui in backButton.GetComponentsInChildren<Text>(true))
        {
            if (ui != null && _backUiFontOrig > 0f)
                ui.fontSize = Mathf.RoundToInt(_backUiFontOrig);
        }
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

        if (resetTimeScaleOnRetry)
            Time.timeScale = timeScaleAfterRetry;

        MobTrafficPause.UnfreezeCarAndWalkerMobs();

        if (catchUiRoot != null)
            catchUiRoot.SetActive(false);

        RestoreHideWhileWarning();
        RestoreBackButtonLayout();

        if (relockCursorOnRetry)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Timer.isRunning = true;
        _caught = false;
        _sightResumeUnscaledTime = Time.unscaledTime + Mathf.Max(0f, sightResumeDelayAfterRetry);

        onRetry?.Invoke();

        if (debugLogRetryFlow)
            Debug.Log($"{debugLogPrefix} 戻る: 警告終了", this);
    }
}
