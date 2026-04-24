using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 歩道（hodou）に触れている間だけ経過秒を蓄積し、一定秒数に達したら完了表示にします。
/// 検知は自転車配下の各 Collider の bounds で <see cref="Physics.OverlapBox"/> し、hodou タグのコライダが
/// 1 つでも重なれば歩道上（<see cref="roadOverlapSuppressesSidewalkCount"/> がオフなら douro は見ません）。
/// UI テキストのハイライト色は、歩道上かつ左手信号を出していないときだけ適用します。
/// 警察視界は <see cref="PoliceLineOfSightState"/>（プローブ更新後）を参照します。
/// </summary>
[DefaultExecutionOrder(25)]
[DisallowMultipleComponent]
public class SidewalkOnlyTextColor : MonoBehaviour
{
    [Header("Tags (Edit > Project Settings > Tags で作成し、歩道ブロックに付与)")]
    [SerializeField] string sidewalkTag = "hodou";

    [Tooltip("OverlapBox の extents に足す余白（m）。薄い hodou や端ギリの接触でも検出しやすくします。")]
    [SerializeField] float sidewalkOverlapExtentsPadding = 0.12f;

    [Tooltip("道路タグ（例: douro）。オフのときは参照しません。")]
    [SerializeField] string roadTag = "douro";

    [Tooltip("オン: hodou と douro の両方に同時に重なっている間は歩道扱いにしない。オフ: hodou に重なっていれば歩道（douro があってもカウント）。")]
    [SerializeField] bool roadOverlapSuppressesSidewalkCount;

    [Header("UI text — 色を変えるテキスト (TMP か uGUI のどちらか)")]
    [SerializeField] TMP_Text tmpText;
    [SerializeField] Text uiText;

    [Header("Colors — 歩道走行ラベル (Tmp / Ui Text)")]
    [Tooltip("歩道上・手信号なしのときの点滅の暗めの色（逆走のハイライトと同じ考え方）")]
    [SerializeField] Color colorWhenOnSidewalkNotOnRoad = new Color(1f, 0.35f, 0.35f, 1f);
    [Tooltip("歩道上・手信号なしのときの点滅の明るい色")]
    [SerializeField] Color colorWhenOnSidewalkPulseHigh = new Color(1f, 0.65f, 0.2f, 1f);
    [Tooltip("歩道にいない、または左手信号中のときの色")]
    [SerializeField] Color colorOtherwise = Color.white;
    [Tooltip("歩道ラベルの点滅速度（逆走の Glow Pulse Speed と同じく Sin 補間。下の Blink Pulse Speed と揃えると同期します）")]
    [SerializeField] float sidewalkLabelPulseSpeed = 5f;

    [Header("左手信号")]
    [Tooltip("未指定なら PlayerHandSignalState.Instance。歩道上でも左クリック（信号）中はハイライト色にしません。")]
    [SerializeField] PlayerHandSignalState handSignalStateSource;

    [Header("歩道上での走行時間 → 残り秒カウントダウン")]
    [Tooltip("歩道に触れている間だけ内部で経過が加算され、表示は残り秒が減っていきます。0に達すると完了表記に切り替わります")]
    [SerializeField] float targetSidewalkSeconds = 30f;
    [SerializeField] TMP_Text countTmpText;
    [SerializeField] Text countUiText;
    [Tooltip("残り秒は {0} に入ります。例: 残り{0}秒")]
    [SerializeField] string countdownFormat = "残り{0}秒";
    [SerializeField] string completedLabel = "完了";
    [Tooltip("カウントダウン中の「残り○秒」表示色")]
    [SerializeField] Color sidewalkCountTextColorCounting = Color.black;
    [Tooltip("歩道走行違反が完了したときの表示色（完了テキスト）")]
    [SerializeField] Color sidewalkCountTextColorCompleted = Color.red;

    [Header("歩道上・カウント中の点滅（逆走と同様・任意）")]
    [Tooltip("歩道上にいて未完了のあいだだけ、指定したテキストの色が行き来します。完了後は完了色で固定。")]
    [SerializeField] TMP_Text[] blinkWhileOnSidewalkTmp;
    [SerializeField] Text[] blinkWhileOnSidewalkUi;
    [Tooltip("歩道に乗っていないときの上記テキストの色")]
    [SerializeField] Color sidewalkBlinkIdleColor = Color.white;
    [SerializeField] float sidewalkBlinkPulseSpeed = 5f;
    [SerializeField] Color sidewalkBlinkColorLow = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] Color sidewalkBlinkColorHigh = new Color(1f, 0.65f, 0.2f, 1f);

    [Header("Debug")]
    [SerializeField] bool debugLog;
    [SerializeField] string logPrefix = "[SidewalkOnlyTextColor]";

    [Header("警察警告")]
    [Tooltip("オン: 歩道違反中かつ PoliceLineOfSightState で視界内のとき、PoliceLineOfSightCatch に警告を依頼します。")]
    [SerializeField] bool requestPoliceCatchWhenSpottedDuringSidewalkViolation = true;
    [Tooltip("オン: カウント完了後も、歩道上かつ左手信号なしなら警察警告を継続します。")]
    [SerializeField] bool keepPoliceCatchAfterObjectiveComplete = true;

    [Header("反則金表示（歩道走行違反完了時）")]
    [SerializeField] string fineAmountText = "6000円";
    [SerializeField] TMP_Text fineAmountDisplayTmp;
    [SerializeField] Text fineAmountDisplayUi;
    [SerializeField] TMP_Text[] additionalFineAmountTmp;
    [SerializeField] Text[] additionalFineAmountUi;

    bool _lastHighlightState;
    bool _hasLastState;
    bool _runCompleted;

    readonly HashSet<Collider> _touchingSidewalk = new HashSet<Collider>();
    readonly HashSet<Collider> _touchingRoad = new HashSet<Collider>();
    float _accumulatedSidewalkTime;

    void TryAddSidewalk(Collider other)
    {
        if (sidewalkTag.Length > 0 && other.CompareTag(sidewalkTag))
            _touchingSidewalk.Add(other);
    }

    void TryAddRoad(Collider other)
    {
        if (roadTag.Length > 0 && other.CompareTag(roadTag))
            _touchingRoad.Add(other);
    }

    /// <summary>
    /// hodou に重なっており、かつ（設定で）douro による打ち消しが無いときに歩道扱い。
    /// </summary>
    bool IsOnSidewalkForTimerAndUi()
    {
        if (_touchingSidewalk.Count == 0)
            return false;
        if (roadOverlapSuppressesSidewalkCount && roadTag.Length > 0 && _touchingRoad.Count > 0)
            return false;
        return true;
    }

    /// <summary>
    /// 歩道ブロックがトリガーでない場合でも、衝突（重なり）で検出できるように毎フレーム再計算します。
    /// </summary>
    void RefreshSidewalkOverlaps()
    {
        _touchingSidewalk.Clear();
        _touchingRoad.Clear();

        float pad = Mathf.Max(0f, sidewalkOverlapExtentsPadding);
        Vector3 padVec = new Vector3(pad, pad, pad);

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col == null || !col.enabled)
                continue;

            Vector3 ext = col.bounds.extents + padVec;

            var hits = Physics.OverlapBox(
                col.bounds.center,
                ext,
                col.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (var h in hits)
            {
                if (h == null || h.transform.IsChildOf(transform))
                    continue;
                TryAddSidewalk(h);
                TryAddRoad(h);
            }
        }
    }

    void LateUpdate()
    {
        RefreshSidewalkOverlaps();
        TickSidewalkTimer();
        ApplyColor();
        ApplySidewalkBlinkTargets();

        bool policeCatchActive = IsSidewalkRuleViolationActiveNow() ||
                                 (keepPoliceCatchAfterObjectiveComplete && _runCompleted && IsOnSidewalkForTimerAndUi() && !IsLeftHandSignalHeld());

        if (requestPoliceCatchWhenSpottedDuringSidewalkViolation &&
            policeCatchActive &&
            PoliceLineOfSightState.IsTargetInPoliceSightNow)
        {
            ViolationFineAmountDisplay.SetFineText(
                fineAmountText,
                fineAmountDisplayTmp,
                fineAmountDisplayUi,
                additionalFineAmountTmp,
                additionalFineAmountUi);
            PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind.Sidewalk);
        }
    }

    void TickSidewalkTimer()
    {
        if (_runCompleted || targetSidewalkSeconds <= 0f)
        {
            UpdateCountDisplayText();
            return;
        }

        if (IsOnSidewalkForTimerAndUi())
            _accumulatedSidewalkTime += Time.deltaTime;

        if (!_runCompleted && _accumulatedSidewalkTime >= targetSidewalkSeconds)
        {
            _runCompleted = true;
            ViolationTimes.NotifySidewalkViolationComplete();
            ViolationFineAmountDisplay.SetFineText(
                fineAmountText,
                fineAmountDisplayTmp,
                fineAmountDisplayUi,
                additionalFineAmountTmp,
                additionalFineAmountUi);
            if (debugLog)
                LogDbg($"Completed: {targetSidewalkSeconds}s on sidewalk (accumulated).");
        }

        UpdateCountDisplayText();
    }

    void UpdateCountDisplayText()
    {
        string s;
        Color countColor;
        if (_runCompleted)
        {
            s = completedLabel;
            countColor = sidewalkCountTextColorCompleted;
        }
        else
        {
            float remaining = Mathf.Max(0f, targetSidewalkSeconds - _accumulatedSidewalkTime);
            int showSec = Mathf.CeilToInt(remaining);
            s = string.Format(countdownFormat, showSec);
            countColor = sidewalkCountTextColorCounting;
        }

        if (countTmpText != null)
        {
            countTmpText.text = s;
            countTmpText.color = countColor;
        }
        if (countUiText != null)
        {
            countUiText.text = s;
            countUiText.color = countColor;
        }
    }

    bool IsLeftHandSignalHeld()
    {
        var src = handSignalStateSource != null ? handSignalStateSource : PlayerHandSignalState.Instance;
        return src != null && src.IsLeftHandSignalHeld;
    }

    void ApplyColor()
    {
        bool onSidewalk = IsOnSidewalkForTimerAndUi();
        bool highlight = onSidewalk && !IsLeftHandSignalHeld();
        Color c;
        if (highlight)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * sidewalkLabelPulseSpeed);
            c = Color.Lerp(colorWhenOnSidewalkNotOnRoad, colorWhenOnSidewalkPulseHigh, pulse);
        }
        else
            c = colorOtherwise;

        if (debugLog)
        {
            if (!_hasLastState || highlight != _lastHighlightState)
            {
                _hasLastState = true;
                _lastHighlightState = highlight;
                string reason = highlight
                    ? "HIGHLIGHT (on sidewalk, hand signal OFF)"
                    : onSidewalk
                        ? "normal (on sidewalk but hand signal ON)"
                        : $"normal (hodouHits={_touchingSidewalk.Count}, onSidewalk={IsOnSidewalkForTimerAndUi()})";
                Debug.Log($"{logPrefix} {reason}", this);
            }
        }

        if (tmpText != null)
            tmpText.color = c;
        if (uiText != null)
            uiText.color = c;
    }

    void ApplySidewalkBlinkTargets()
    {
        if ((blinkWhileOnSidewalkTmp == null || blinkWhileOnSidewalkTmp.Length == 0) &&
            (blinkWhileOnSidewalkUi == null || blinkWhileOnSidewalkUi.Length == 0))
            return;

        Color c;
        if (_runCompleted)
            c = sidewalkCountTextColorCompleted;
        else if (IsOnSidewalkForTimerAndUi())
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * sidewalkBlinkPulseSpeed);
            c = Color.Lerp(sidewalkBlinkColorLow, sidewalkBlinkColorHigh, pulse);
        }
        else
            c = sidewalkBlinkIdleColor;

        if (blinkWhileOnSidewalkTmp != null)
        {
            foreach (var t in blinkWhileOnSidewalkTmp)
            {
                if (t != null)
                    t.color = c;
            }
        }
        if (blinkWhileOnSidewalkUi != null)
        {
            foreach (var t in blinkWhileOnSidewalkUi)
            {
                if (t != null)
                    t.color = c;
            }
        }
    }

    void LogDbg(string message)
    {
        if (debugLog)
            Debug.Log($"{logPrefix} {message}", this);
    }

    /// <summary>歩道上で左手信号を出しておらず、まだ完了していない（警察の警告文用）。</summary>
    public bool IsSidewalkRuleViolationActiveNow()
    {
        if (_runCompleted)
            return false;
        return IsOnSidewalkForTimerAndUi() && !IsLeftHandSignalHeld();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        sidewalkOverlapExtentsPadding = Mathf.Max(0f, sidewalkOverlapExtentsPadding);
        if (Application.isPlaying)
            return;
        if (tmpText != null)
            tmpText.color = colorOtherwise;
        if (uiText != null)
            uiText.color = colorOtherwise;
    }
#endif
}
