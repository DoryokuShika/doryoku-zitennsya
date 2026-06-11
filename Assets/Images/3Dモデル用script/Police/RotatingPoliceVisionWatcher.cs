using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 一定間隔で90度ずつ回転し、現在の向きの視界内に指定ターゲットがいるか判定する。
/// ターゲットが視界内 かつ 違反中なら、警察警告（PoliceLineOfSightCatch）を依頼する。
/// </summary>
[DefaultExecutionOrder(35)]
public class RotatingPoliceVisionWatcher : MonoBehaviour
{
    static readonly HashSet<int> _moneyZeroWatcherIds = new HashSet<int>();
    public static bool IsAnyFakePoliceMoneyZeroNow { get; private set; }
    [Header("回転対象")]
    [Tooltip("未指定ならこの GameObject を回転させます。")]
    [SerializeField] Transform rotatingObject;
    [SerializeField] Space rotateSpace = Space.World;
    [SerializeField] Vector3 rotateAxis = Vector3.up;
    [SerializeField] float rotateStepDegrees = 90f;
    [Tooltip("オン: rotateStepDegrees の代わりに、下の候補角度から毎回ランダムで選んで回転します。")]
    [SerializeField] bool randomizeRotateStepFromCandidates;
    [Tooltip("randomizeRotateStepFromCandidates がオンのときに使う候補角度（例: 0,90,180,270）。")]
    [SerializeField] float[] randomRotateStepCandidates = new float[] { 0f, 90f, 180f, 270f };
    [SerializeField] float rotateIntervalSeconds = 20f;
    [SerializeField] bool useUnscaledTimeForRotation = false;

    [Header("視界判定")]
    [Tooltip("視界に入っているか判定したい対象（Inspector でセット）")]
    [SerializeField] Transform watchTarget;
    [SerializeField] float viewDistance = 40f;
    [Range(1f, 180f)]
    [SerializeField] float viewAngleDegrees = 90f;
    [Tooltip("オン: 遮蔽物があると視界外扱い")]
    [SerializeField] bool useOcclusionRaycast = true;
    [SerializeField] LayerMask occlusionMask = ~0;

    [Header("警察警告連携")]
    [Tooltip("オン: 視界内で違反中なら警察警告を依頼します。")]
    [SerializeField] bool requestPoliceCatchWhenTargetVisibleAndViolating = true;
    [Tooltip("オン: この監視の視界判定を PoliceLineOfSightState に流し込みます。偽警察では通常オフ推奨。")]
    [SerializeField] bool reportSightToGlobalPoliceState = false;
    [SerializeField] bool requirePlayerViolationState = true;
    [Tooltip("オン: 捕獲時に違反種別を自動判定して警告文・減額を一致させます。")]
    [SerializeField] bool autoDetectViolationKind = true;
    [SerializeField] PoliceCatchViolationKind catchViolationKind = PoliceCatchViolationKind.Signal;

    [Header("偽警察モード（この監視だけ別挙動）")]
    [Tooltip("オン: この監視で捕まったときだけ、青切符を増やさず所持金を減らします。")]
    [SerializeField] bool fakePoliceMode = true;
    [SerializeField] int startMoney = 10000;
    [Header("偽警察モード: 違反ごとの減額")]
    [SerializeField] int fineAmountWrongWay = 6000;
    [SerializeField] int fineAmountSidewalk = 6000;
    [SerializeField] int fineAmountSignal = 6000;
    [SerializeField] int fineAmountPedestrianBell = 3000;
    [SerializeField] int fineAmountUnlitBicycleAtNight = 5000;
    [SerializeField] int fineAmountDefault = 6000;
    [SerializeField] string fakePoliceCaughtMessage = "偽警察に捕まりました。";
    [SerializeField] string fakePoliceFineTextFormat = "{0}円";
    [SerializeField] string moneyTextFormat = "所持金: {0}円";
    [Header("所持金0円時の遷移")]
    [SerializeField] bool loadPoliceOverWhenMoneyZeroOnCatchClose = true;
    [SerializeField] string policeOverWhenMoneyZeroSceneName = "PoliceOver2";
    [SerializeField] string fallbackPoliceOverWhenMoneyZeroSceneName = "PoliceOver";
    [Header("再捕獲ガード")]
    [Tooltip("戻る直後に連続で再捕獲しないための待機秒（偽警察）。")]
    [SerializeField] float fakePoliceRecatchBlockSecondsAfterClose = 0.8f;
    [Tooltip("所持金表示（TMP）")]
    [SerializeField] TMP_Text moneyTmpText;
    [Tooltip("所持金表示（uGUI Text）")]
    [SerializeField] Text moneyUiText;
    [Tooltip("偽警察時に上書きする捕獲SE（1つ目）")]
    [SerializeField] AudioClip fakePoliceCatchSfxFirst;
    [Tooltip("偽警察時に上書きする捕獲SE（2つ目）")]
    [SerializeField] AudioClip fakePoliceCatchSfxSecond;
    [Range(0f, 1f)]
    [SerializeField] float fakePoliceCatchSfxVolume = 1f;

    [Header("デバッグ")]
    [SerializeField] bool drawGizmos = true;

    float _rotateTimer;
    bool _isTargetInSightNow;
    bool _requestedDuringCurrentSightWindow;
    int _money;
    bool _moneyZeroPendingGameOverOnCatchClose;
    float _nextAllowedFakePoliceRequestUnscaled = float.NegativeInfinity;

    /// <summary>このスクリプトの直近判定。</summary>
    public bool IsTargetInSightNow => _isTargetInSightNow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticStateOnSubsystemRegistration()
    {
        _moneyZeroWatcherIds.Clear();
        IsAnyFakePoliceMoneyZeroNow = false;
    }

    void Awake()
    {
        if (rotatingObject == null)
            rotatingObject = transform;
        _money = Mathf.Max(0, startMoney);
        ApplyMoneyText();
    }

    void OnEnable()
    {
        PoliceLineOfSightCatch.PoliceCatchClosed += OnPoliceCatchClosed;
        UpdateMoneyZeroAggregate();
    }

    void OnDisable()
    {
        PoliceLineOfSightCatch.PoliceCatchClosed -= OnPoliceCatchClosed;
        _moneyZeroWatcherIds.Remove(GetInstanceID());
        IsAnyFakePoliceMoneyZeroNow = _moneyZeroWatcherIds.Count > 0;
    }

    void Update()
    {
        if (rotatingObject == null)
            return;

        float dt = useUnscaledTimeForRotation ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        _rotateTimer += dt;
        float interval = Mathf.Max(0.01f, rotateIntervalSeconds);
        while (_rotateTimer >= interval)
        {
            _rotateTimer -= interval;
            float step = ResolveRotateStepDegrees();
            rotatingObject.Rotate(rotateAxis.normalized, step, rotateSpace);
        }
    }

    float ResolveRotateStepDegrees()
    {
        if (!randomizeRotateStepFromCandidates)
            return rotateStepDegrees;

        if (randomRotateStepCandidates == null || randomRotateStepCandidates.Length == 0)
            return rotateStepDegrees;

        int idx = Random.Range(0, randomRotateStepCandidates.Length);
        return randomRotateStepCandidates[idx];
    }

    void LateUpdate()
    {
        _isTargetInSightNow = ComputeIsTargetVisible();
        // 偽警察が通常警察の視界扱いになる誤判定を防ぐため、必要時のみ集約へ流す。
        if (reportSightToGlobalPoliceState)
            PoliceLineOfSightState.ProbeReportsInSight(_isTargetInSightNow);

        if (!requestPoliceCatchWhenTargetVisibleAndViolating)
            return;
        if (!_isTargetInSightNow)
        {
            // 視界から外れたら次回再捕獲を許可する
            _requestedDuringCurrentSightWindow = false;
            return;
        }
        if (requirePlayerViolationState && !PlayerViolationState.IsViolatingNow)
            return;
        if (fakePoliceMode && Time.unscaledTime < _nextAllowedFakePoliceRequestUnscaled)
            return;
        if (_requestedDuringCurrentSightWindow)
            return;

        PoliceCatchViolationKind kind = ResolveCatchViolationKind();
        if (fakePoliceMode)
        {
            bool accepted = PoliceLineOfSightCatch.RequestTryCatchWithOverridesWhenViolationVisibleToPolice(
                kind,
                true,
                true,
                fakePoliceCatchSfxFirst,
                fakePoliceCatchSfxSecond,
                fakePoliceCatchSfxVolume,
                fakePoliceCaughtMessage,
                FormatFakePoliceFineText(ResolveFineAmountForKind(kind)));
            if (!accepted)
                return;
            _requestedDuringCurrentSightWindow = true;
            DeductMoney(kind);
            return;
        }

        _requestedDuringCurrentSightWindow = true;
        PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice(kind);
    }

    void OnPoliceCatchClosed()
    {
        // 警告を閉じたら、視界内に居続けても再判定できるようにする
        _requestedDuringCurrentSightWindow = false;
        _nextAllowedFakePoliceRequestUnscaled =
            Time.unscaledTime + Mathf.Max(0f, fakePoliceRecatchBlockSecondsAfterClose);
        TryLoadMoneyGameOverSceneOnCatchClose();
    }

    int DeductMoney(PoliceCatchViolationKind kind)
    {
        int penalty = Mathf.Max(0, ResolveFineAmountForKind(kind));
        _money = Mathf.Max(0, _money - penalty);
        ApplyMoneyText();
        UpdateMoneyZeroAggregate();
        if (IsAnyFakePoliceMoneyZeroNow)
            _moneyZeroPendingGameOverOnCatchClose = true;
        return penalty;
    }

    void UpdateMoneyZeroAggregate()
    {
        int id = GetInstanceID();
        if (_money <= 0)
            _moneyZeroWatcherIds.Add(id);
        else
            _moneyZeroWatcherIds.Remove(id);
        IsAnyFakePoliceMoneyZeroNow = _moneyZeroWatcherIds.Count > 0;
    }

    void TryLoadMoneyGameOverSceneOnCatchClose()
    {
        if (!loadPoliceOverWhenMoneyZeroOnCatchClose)
            return;
        if (!_moneyZeroPendingGameOverOnCatchClose)
            return;
        _moneyZeroPendingGameOverOnCatchClose = false;

        string scene = string.IsNullOrWhiteSpace(policeOverWhenMoneyZeroSceneName)
            ? string.Empty
            : policeOverWhenMoneyZeroSceneName.Trim();
        string fallback = string.IsNullOrWhiteSpace(fallbackPoliceOverWhenMoneyZeroSceneName)
            ? string.Empty
            : fallbackPoliceOverWhenMoneyZeroSceneName.Trim();

        if (!string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene))
        {
            Timer.isRunning = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneManager.LoadScene(scene);
            return;
        }

        if (!string.IsNullOrEmpty(fallback) && Application.CanStreamedLevelBeLoaded(fallback))
        {
            Timer.isRunning = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneManager.LoadScene(fallback);
            return;
        }

        Debug.LogWarning($"[RotatingPoliceVisionWatcher] 所持金0円時の遷移先シーンが見つかりません。'{scene}' / '{fallback}' を Build Settings に追加してください。", this);
    }

    string FormatFakePoliceFineText(int amount)
    {
        string fmt = string.IsNullOrWhiteSpace(fakePoliceFineTextFormat) ? "{0}円" : fakePoliceFineTextFormat;
        return string.Format(fmt, Mathf.Max(0, amount));
    }

    int ResolveFineAmountForKind(PoliceCatchViolationKind kind)
    {
        return kind switch
        {
            PoliceCatchViolationKind.WrongWay => fineAmountWrongWay,
            PoliceCatchViolationKind.Sidewalk => fineAmountSidewalk,
            PoliceCatchViolationKind.Signal => fineAmountSignal,
            PoliceCatchViolationKind.PedestrianBell => fineAmountPedestrianBell,
            PoliceCatchViolationKind.UnlitBicycleAtNight => fineAmountUnlitBicycleAtNight,
            _ => fineAmountDefault,
        };
    }

    PoliceCatchViolationKind ResolveCatchViolationKind()
    {
        if (!autoDetectViolationKind)
            return catchViolationKind;

        // まず「直近で成立した違反種別」を優先。古いリークを防ぐため鮮度窓を設ける。
        const float recentKindWindowSeconds = 0.6f;
        if (Time.unscaledTime - PlayerViolationState.LastViolationUnscaledTime <= recentKindWindowSeconds &&
            PlayerViolationState.LastViolationKind != PoliceCatchViolationKind.None)
            return PlayerViolationState.LastViolationKind;

        // フォールバック（直近種別が古い/未設定のとき）
        if (PlayerViolationState.IsPedestrianBellViolationPulseActive)
            return PoliceCatchViolationKind.PedestrianBell;
        if (PlayerViolationState.IsUnlitViolationActive)
            return PoliceCatchViolationKind.UnlitBicycleAtNight;
        if (PlayerViolationState.IsSidewalkViolating)
            return PoliceCatchViolationKind.Sidewalk;
        if (PlayerViolationState.IsWrongWayViolating)
            return PoliceCatchViolationKind.WrongWay;
        if (PlayerViolationState.IsSignalViolationPulseActive)
            return PoliceCatchViolationKind.Signal;

        return catchViolationKind;
    }

    void ApplyMoneyText()
    {
        string text = string.Format(moneyTextFormat, _money);
        if (moneyTmpText != null)
            moneyTmpText.text = text;
        if (moneyUiText != null)
            moneyUiText.text = text;
    }

    bool ComputeIsTargetVisible()
    {
        if (rotatingObject == null || watchTarget == null)
            return false;

        Vector3 origin = rotatingObject.position;
        Vector3 toTarget = watchTarget.position - origin;
        float sqrDist = toTarget.sqrMagnitude;
        float maxDist = Mathf.Max(0.01f, viewDistance);
        if (sqrDist > maxDist * maxDist)
            return false;

        Vector3 forward = rotatingObject.forward;
        Vector3 dir = toTarget.normalized;
        float halfAngle = Mathf.Clamp(viewAngleDegrees, 1f, 180f) * 0.5f;
        if (Vector3.Angle(forward, dir) > halfAngle)
            return false;

        if (!useOcclusionRaycast)
            return true;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, occlusionMask, QueryTriggerInteraction.Ignore))
        {
            // 自分の子階層ヒットは無視して先を見る
            if (hit.transform == rotatingObject || hit.transform.IsChildOf(rotatingObject))
            {
                Vector3 nextOrigin = hit.point + dir * 0.02f;
                float remain = Mathf.Max(0f, maxDist - Vector3.Distance(origin, nextOrigin));
                if (Physics.Raycast(nextOrigin, dir, out RaycastHit hit2, remain, occlusionMask, QueryTriggerInteraction.Ignore))
                    return hit2.transform == watchTarget || hit2.transform.IsChildOf(watchTarget);
                return false;
            }

            return hit.transform == watchTarget || hit.transform.IsChildOf(watchTarget);
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Transform t = rotatingObject != null ? rotatingObject : transform;
        if (t == null)
            return;

        Vector3 origin = t.position;
        Vector3 forward = t.forward;
        float dist = Mathf.Max(0.01f, viewDistance);
        float half = Mathf.Clamp(viewAngleDegrees, 1f, 180f) * 0.5f;
        Quaternion leftRot = Quaternion.AngleAxis(-half, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(half, Vector3.up);

        Gizmos.color = _isTargetInSightNow ? Color.green : Color.yellow;
        Gizmos.DrawRay(origin, forward * dist);
        Gizmos.DrawRay(origin, leftRot * forward * dist);
        Gizmos.DrawRay(origin, rightRot * forward * dist);
    }
}
