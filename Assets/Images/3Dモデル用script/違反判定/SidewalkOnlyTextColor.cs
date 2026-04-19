using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 歩道（hodou）に触れている間だけ経過秒を蓄積し、一定秒数に達したら完了表示にします。
/// UI テキストのハイライト色は、歩道上かつ左手信号を出していないときだけ適用します。
/// </summary>
[DisallowMultipleComponent]
public class SidewalkOnlyTextColor : MonoBehaviour
{
    [Header("Tags (Edit > Project Settings > Tags で作成し、歩道ブロックに付与)")]
    [SerializeField] string sidewalkTag = "hodou";

    [Header("UI text — 色を変えるテキスト (TMP か uGUI のどちらか)")]
    [SerializeField] TMP_Text tmpText;
    [SerializeField] Text uiText;

    [Header("Colors")]
    [SerializeField] Color colorWhenOnSidewalkNotOnRoad = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] Color colorOtherwise = Color.white;

    [Header("左手信号")]
    [Tooltip("未指定なら PlayerHandSignalState.Instance。歩道上でも左クリック（信号）中はハイライト色にしません。")]
    [SerializeField] PlayerHandSignalState handSignalStateSource;

    [Header("歩道上での走行時間 → 秒表示（以前の回数表示用テキスト）")]
    [Tooltip("歩道に触れている間だけ加算される秒。ここに達すると完了表記に切り替わります")]
    [SerializeField] float targetSidewalkSeconds = 30f;
    [SerializeField] TMP_Text countTmpText;
    [SerializeField] Text countUiText;
    [SerializeField] string completedLabel = "完了";

    [Header("Debug")]
    [SerializeField] bool debugLog;
    [SerializeField] string logPrefix = "[SidewalkOnlyTextColor]";

    bool _lastHighlightState;
    bool _hasLastState;
    bool _runCompleted;

    readonly HashSet<Collider> _touchingSidewalk = new HashSet<Collider>();
    float _accumulatedSidewalkTime;

    void Start()
    {
        StartCoroutine(RecountAfterPhysics());
    }

    IEnumerator RecountAfterPhysics()
    {
        yield return new WaitForFixedUpdate();
        _touchingSidewalk.Clear();

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col == null || !col.enabled)
                continue;

            var hits = Physics.OverlapBox(
                col.bounds.center,
                col.bounds.extents,
                col.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (var h in hits)
            {
                if (h == null || h.transform.IsChildOf(transform))
                    continue;
                TryAddSidewalk(h);
            }
        }

        UpdateCountDisplayText();
        ApplyColor(true);
    }

    void TryAddSidewalk(Collider other)
    {
        if (sidewalkTag.Length > 0 && other.CompareTag(sidewalkTag))
            _touchingSidewalk.Add(other);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.enabled)
            return;
        if (sidewalkTag.Length > 0 && other.CompareTag(sidewalkTag))
        {
            _touchingSidewalk.Add(other);
            LogDbg($"Enter SIDEWALK tag={sidewalkTag} obj={other.name}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (_touchingSidewalk.Remove(other))
            LogDbg($"Exit SIDEWALK obj={other.name}");
    }

    void LateUpdate()
    {
        PurgeDestroyedColliders();
        TickSidewalkTimer();
        ApplyColor(false);
    }

    void TickSidewalkTimer()
    {
        if (_runCompleted || targetSidewalkSeconds <= 0f)
        {
            UpdateCountDisplayText();
            return;
        }

        if (_touchingSidewalk.Count > 0)
            _accumulatedSidewalkTime += Time.deltaTime;

        if (!_runCompleted && _accumulatedSidewalkTime >= targetSidewalkSeconds)
        {
            _runCompleted = true;
            if (debugLog)
                LogDbg($"Completed: {targetSidewalkSeconds}s on sidewalk (accumulated).");
        }

        UpdateCountDisplayText();
    }

    void UpdateCountDisplayText()
    {
        string s;
        if (_runCompleted)
            s = completedLabel;
        else
            s = Mathf.FloorToInt(_accumulatedSidewalkTime).ToString();

        if (countTmpText != null)
            countTmpText.text = s;
        if (countUiText != null)
            countUiText.text = s;
    }

    void PurgeDestroyedColliders()
    {
        RemoveNulls(_touchingSidewalk);
    }

    static void RemoveNulls(HashSet<Collider> set)
    {
        if (set.Count == 0)
            return;
        List<Collider> dead = null;
        foreach (var c in set)
        {
            if (c == null)
                (dead ??= new List<Collider>()).Add(c);
        }
        if (dead == null)
            return;
        foreach (var c in dead)
            set.Remove(c);
    }

    bool IsLeftHandSignalHeld()
    {
        var src = handSignalStateSource != null ? handSignalStateSource : PlayerHandSignalState.Instance;
        return src != null && src.IsLeftHandSignalHeld;
    }

    void ApplyColor(bool fromStartupRecount = false)
    {
        bool onSidewalk = _touchingSidewalk.Count > 0;
        bool highlight = onSidewalk && !IsLeftHandSignalHeld();
        Color c = highlight ? colorWhenOnSidewalkNotOnRoad : colorOtherwise;

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
                        : $"normal (sidewalkCount={_touchingSidewalk.Count})";
                if (fromStartupRecount)
                    reason += " [after Start recount]";
                Debug.Log($"{logPrefix} {reason}", this);
            }
        }

        if (tmpText != null)
            tmpText.color = c;
        if (uiText != null)
            uiText.color = c;
    }

    void LogDbg(string message)
    {
        if (debugLog)
            Debug.Log($"{logPrefix} {message}", this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;
        if (tmpText != null)
            tmpText.color = colorOtherwise;
        if (uiText != null)
            uiText.color = colorOtherwise;
    }
#endif
}
