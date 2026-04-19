using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 警察視点で対象が視野内なら「捕獲」。カメラは切り替えず、
/// 警告用パネル（Canvas）を表示し、指定オブジェクト（通常は別 Canvas ルート）を一時非表示にします。
/// 戻るボタンで非表示を巻き戻します。
/// </summary>
[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
public class PoliceLineOfSightCatch : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("捕獲判定の対象（自転車＝プレイヤーなど）。コライダがあるとレイ判定が確実です。")]
    [SerializeField] Transform targetCharacter;
    [Tooltip("警告中に停止させる警察側のルート（パトカー親など）。未指定ならこのオブジェクト")]
    [SerializeField] Transform policeRootToFreeze;

    [Header("視点（未指定ならこのオブジェクトの位置＋オフセット）")]
    [SerializeField] Transform eyeTransform;
    [SerializeField] Vector3 eyeLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("視野")]
    [Tooltip("左右合わせた水平視野角（度）。例: 90")]
    [SerializeField] float horizontalViewAngleDegrees = 90f;
    [SerializeField] float maxViewDistance = 30f;
    [Tooltip("遮蔽として扱うレイヤー（壁など）")]
    [SerializeField] LayerMask obstacleLayers = ~0;

    [Header("違反中のみ警告")]
    [Tooltip("オン: PlayerViolationState.IsViolatingNow が true のときだけ捕獲（青切符）。オフ: 視界に入れば常に警告。")]
    [SerializeField] bool requireViolationStateToCatch = true;

    [Header("警告パネル（見つかったとき表示）")]
    [Tooltip("捕獲中だけ表示するルート（パネル・テキスト・戻るボタンの親）")]
    [SerializeField] GameObject catchUiRoot;
    [SerializeField] TMP_Text violationMessageTmp;
    [SerializeField] Text violationMessageUiText;
    [FormerlySerializedAs("violationMessage")]
    [SerializeField] string messageOnCaught = "見つかりました。";
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
        if (Time.unscaledTime < _sightResumeUnscaledTime)
            return;

        if (_caught || targetCharacter == null)
            return;

        if (!IsTargetInSight())
            return;

        if (requireViolationStateToCatch && !PlayerViolationState.IsViolatingNow)
            return;

        Catch();
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

    public bool IsTargetInSight()
    {
        if (targetCharacter == null)
            return false;

        Vector3 origin = GetEyeWorldPosition();
        Vector3 targetPoint = GetTargetSamplePoint(targetCharacter);
        Vector3 toTarget = targetPoint - origin;
        float dist = toTarget.magnitude;
        if (dist < 0.01f || dist > maxViewDistance)
            return false;

        Vector3 forward = transform.forward;
        float halfAngle = Mathf.Clamp(horizontalViewAngleDegrees * 0.5f, 0.1f, 179f);
        if (Vector3.Angle(forward, toTarget) > halfAngle)
            return false;

        Vector3 dir = toTarget / dist;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleLayers, QueryTriggerInteraction.Ignore))
            return IsTransformPartOfTarget(hit.transform, targetCharacter);

        return true;
    }

    static bool IsTransformPartOfTarget(Transform t, Transform targetRoot)
    {
        if (t == null || targetRoot == null)
            return false;
        return t == targetRoot || t.IsChildOf(targetRoot);
    }

    Vector3 GetEyeWorldPosition()
    {
        if (eyeTransform != null)
            return eyeTransform.position;
        return transform.TransformPoint(eyeLocalOffset);
    }

    static Vector3 GetTargetSamplePoint(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        var col = target.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return target.position;
    }

    void Catch()
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

        ApplyMessageToUi();
        ApplyHideWhileWarning();
        StyleBackButtonForCatch();

        if (catchUiRoot != null)
            EnsureAncestorsActive(catchUiRoot.transform);
        if (catchUiRoot != null)
            catchUiRoot.SetActive(true);

        onCaught?.Invoke();

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

    void ApplyMessageToUi()
    {
        if (violationMessageTmp != null)
            violationMessageTmp.text = messageOnCaught;
        if (violationMessageUiText != null)
            violationMessageUiText.text = messageOnCaught;
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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = eyeTransform != null ? eyeTransform.position : transform.TransformPoint(eyeLocalOffset);
        float half = Mathf.Clamp(horizontalViewAngleDegrees * 0.5f, 1f, 89f);
        Vector3 f = transform.forward * maxViewDistance;
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        Gizmos.DrawRay(origin, f);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-half, transform.up) * f);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(half, transform.up) * f);
    }
#endif
}
