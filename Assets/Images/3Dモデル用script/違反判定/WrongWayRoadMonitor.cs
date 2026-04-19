using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="PlayerRoadTravelState"/> の方角・道路接触を使い、インスペクターで登録した道路ごとに逆走を判定します。
/// よく使うのは「進行方角」だけ: その道路に触れている間、許可した上下左右以外に進んでいたら逆走。
/// 「横位置」は車線の左右（道路中心から見たプラス／マイナス側）用。方角だけなら Ignore でよい。
/// </summary>
[DisallowMultipleComponent]
public class WrongWayRoadMonitor : MonoBehaviour
{
    public enum LateralWrongWayMode
    {
        [Tooltip("左右どちらの車線かでは判定しない（進行方角だけ使うときはこれ）")]
        Ignore = 0,
        [Tooltip("道路中心から見て「プラス側」の帯にいると逆走（東西道路=+Z側の半分、南北=+X側）")]
        WrongWhenPositiveLateral = 1,
        [Tooltip("道路中心から見て「マイナス側」の帯にいると逆走")]
        WrongWhenNegativeLateral = 2,
    }

    [System.Serializable]
    public class RoadRule
    {
        [Tooltip("監視する道路コライダ。douro 等。プレイヤーがこのコライダに触れている必要があります。")]
        public Collider road;

        [Header("横位置（車線の左右・任意）")]
        [Tooltip("「この道路のどちら側の帯にいるか」で逆走にするか。方角だけでよい場合は Ignore。")]
        public LateralWrongWayMode lateralWrongWay = LateralWrongWayMode.Ignore;
        [Tooltip("中央付き近を逆走にしない幅。横位置判定を使うときだけ効く。")]
        public float lateralDeadZone = 0.08f;

        [Header("進行方角（この道で進んでよい向き・よく使う）")]
        [Tooltip("None=方角では判定しない。North等を指定=その道路にいる間はその進行方角だけ合法（他の上下左右は逆走）。例:上一方通行ならNorth。ログのCardinalと合わせる。")]
        public PlayerRoadTravelState.Cardinal4 allowedDominantCardinalOnly = PlayerRoadTravelState.Cardinal4.None;
    }

    [Header("参照")]
    [SerializeField] PlayerRoadTravelState travelState;
    [Tooltip("未指定ならこのオブジェクトの位置を使う")]
    [SerializeField] Transform worldPositionSource;

    [Header("ルール（好きな本数追加）")]
    [SerializeField] List<RoadRule> monitoredRoads = new List<RoadRule>();

    [Header("判定")]
    [Tooltip("これ未満の速度では判定しない（PlayerRoadTravelState に合わせる）")]
    [SerializeField] float minSpeed = 0.35f;

    [Header("デバッグログ")]
    [SerializeField] bool debugLogWrongWay = true;
    [Tooltip("オン: 逆走中は毎フレームログ。オフ: 逆走になった瞬間だけログ。")]
    [SerializeField] bool logEveryFrameWhileWrongWay;
    [Tooltip("オン: 逆走から抜けたときもログ")]
    [SerializeField] bool logWhenRecovered;
    [Tooltip("オン: 逆走検知は LogWarning（コンソールで黄色・目立つ）。オフ: 通常の Log。")]
    [SerializeField] bool logWrongWayAsWarning = true;

    [SerializeField] string logPrefix = "[WrongWayRoadMonitor]";

    [Header("UI — 逆走中のハイライト（光らせる）")]
    [Tooltip("逆走中だけ色が脈動します。TMP / uGUI / Image のどれかを指定可。")]
    [SerializeField] TMP_Text glowHighlightTmpText;
    [SerializeField] Text glowHighlightUiText;
    [SerializeField] Graphic glowGraphic;
    [SerializeField] Color glowColorNormal = Color.white;
    [SerializeField] Color glowColorWrongWay = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] Color glowColorWrongWayBright = new Color(1f, 0.65f, 0.2f, 1f);
    [Tooltip("光の点滅速度（大きいほど速く）")]
    [SerializeField] float glowPulseSpeed = 5f;

    [Header("UI — 逆走カウントダウン")]
    [Tooltip("逆走している間だけこの秒数が減る。開始時は満タン。")]
    [SerializeField] float wrongWayCountdownSeconds = 45f;
    [SerializeField] TMP_Text countdownTmpText;
    [SerializeField] Text countdownUiText;
    [Tooltip("例: あと{0}秒 の {0} に切り上げ秒が入る")]
    [SerializeField] string countdownFormat = "あと{0}秒";
    [SerializeField] string countdownCompletedLabel = "完了";
    [Tooltip("逆走をやめても残り秒は維持（一時停止）。オンにすると逆走解除で秒数が満タンに戻る。")]
    [SerializeField] bool resetCountdownWhenWrongWayEnds;

    Rigidbody _rb;
    bool[] _wasWrong;
    float _remainingWrongWaySeconds;
    bool _countdownCompleted;

    void Awake()
    {
        if (travelState == null)
            travelState = GetComponent<PlayerRoadTravelState>();
        if (travelState == null)
            travelState = GetComponentInParent<PlayerRoadTravelState>();

        if (worldPositionSource == null)
            worldPositionSource = transform;

        if (travelState != null)
        {
            _rb = travelState.GetComponent<Rigidbody>();
            if (_rb == null)
                _rb = travelState.GetComponentInParent<Rigidbody>();
        }

        _remainingWrongWaySeconds = Mathf.Max(0f, wrongWayCountdownSeconds);
        ApplyGlowVisual(false, false);
        UpdateCountdownTexts();
    }

    void LateUpdate()
    {
        if (travelState == null || monitoredRoads == null || monitoredRoads.Count == 0)
        {
            ApplyGlowVisual(false, false);
            return;
        }

        EnsureWasWrongArray();

        Vector3 vel = _rb != null ? FlatVelocityXZ(_rb.velocity) : Vector3.zero;
        bool speedOk = vel.sqrMagnitude >= minSpeed * minSpeed;
        Vector3 pos = worldPositionSource.position;

        bool anyWrong = false;

        if (speedOk)
        {
            for (int i = 0; i < monitoredRoads.Count; i++)
            {
                RoadRule rule = monitoredRoads[i];
                if (rule.road == null)
                {
                    ClearWrongState(i);
                    continue;
                }

                if (!travelState.IsTouchingRoad(rule.road))
                {
                    ClearWrongState(i);
                    continue;
                }

                if (!PlayerRoadTravelState.TryGetSignedLateralAlongRoad(rule.road, pos, out float lateral, out bool eastWest))
                    continue;

                bool lateralWrong = EvaluateLateralWrong(rule, lateral);
                bool cardinalWrong = EvaluateCardinalWrong(rule, travelState.DominantCardinal);
                bool wrongNow = lateralWrong || cardinalWrong;
                if (wrongNow)
                    anyWrong = true;

                if (wrongNow)
                {
                    if (debugLogWrongWay && (logEveryFrameWhileWrongWay || !_wasWrong[i]))
                        LogWrongWay(rule, eastWest, lateral, lateralWrong, cardinalWrong, travelState);

                    _wasWrong[i] = true;
                }
                else
                {
                    if (debugLogWrongWay && logWhenRecovered && _wasWrong[i])
                        Debug.Log($"{logPrefix} ---------- 逆走ではなくなりました（解除） road={rule.road.name} ----------", this);

                    _wasWrong[i] = false;
                }
            }
        }
        else
        {
            for (int i = 0; i < monitoredRoads.Count; i++)
                ClearWrongState(i);
        }

        TickWrongWayCountdownUi(anyWrong);
    }

    static Vector3 FlatVelocityXZ(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void ClearWrongState(int index)
    {
        if (_wasWrong == null || index < 0 || index >= _wasWrong.Length)
            return;

        if (logWhenRecovered && debugLogWrongWay && _wasWrong[index])
            Debug.Log($"{logPrefix} ---------- 逆走監視終了（この道路から離脱） index={index} ----------", this);

        _wasWrong[index] = false;
    }

    void EnsureWasWrongArray()
    {
        int n = monitoredRoads.Count;
        if (_wasWrong == null || _wasWrong.Length != n)
            _wasWrong = new bool[n];
    }

    static bool EvaluateLateralWrong(RoadRule rule, float lateral)
    {
        float z = Mathf.Max(0f, rule.lateralDeadZone);
        return rule.lateralWrongWay switch
        {
            LateralWrongWayMode.WrongWhenPositiveLateral => lateral > z,
            LateralWrongWayMode.WrongWhenNegativeLateral => lateral < -z,
            _ => false,
        };
    }

    static bool EvaluateCardinalWrong(RoadRule rule, PlayerRoadTravelState.Cardinal4 actual)
    {
        if (rule.allowedDominantCardinalOnly == PlayerRoadTravelState.Cardinal4.None)
            return false;
        if (actual == PlayerRoadTravelState.Cardinal4.None)
            return false;
        return actual != rule.allowedDominantCardinalOnly;
    }

    void LogWrongWay(RoadRule rule, bool eastWestRoad, float lateral, bool latBad, bool cardBad, PlayerRoadTravelState travel)
    {
        string latDesc = eastWestRoad
            ? "東西道路（横オフセット=Z−道路中心Z、+は+Z側の帯）"
            : "南北道路（横オフセット=X−道路中心X、+は+X側の帯）";

        string reasonLines = "";
        if (latBad)
            reasonLines += $"  ▶ 理由: 車線の左右（横位置）がルール違反  lateral={lateral:F3}\n";
        if (cardBad)
        {
            string act = PlayerRoadTravelState.CardinalToRoseJa(travel.DominantCardinal);
            string ok = PlayerRoadTravelState.CardinalToRoseJa(rule.allowedDominantCardinalOnly);
            reasonLines += $"  ▶ 理由: 進行方角が違う（いま {act} ／ この道では {ok} のみOK）\n";
        }

        float spd = FlatVelocityXZ(_rb != null ? _rb.velocity : Vector3.zero).magnitude;
        string block =
            "\n" +
            "████████████████████████████████████████████████████████\n" +
            "██  ギャクソウ（逆走）しています！！  はい = TRUE   ██\n" +
            "████████████████████████████████████████████████████████\n" +
            $"  道路オブジェクト: {rule.road.name}\n" +
            $"{reasonLines}" +
            $"  補足: {latDesc}\n" +
            $"  速度XZ: {spd:F2}\n" +
            "--------------------------------------------------------\n";

        if (logWrongWayAsWarning)
            Debug.LogWarning($"{logPrefix}{block}", this);
        else
            Debug.Log($"{logPrefix}{block}", this);
    }

    void TickWrongWayCountdownUi(bool anyWrongNow)
    {
        if (_countdownCompleted)
        {
            ApplyGlowVisual(false, true);
            SetCountdownDisplayText(countdownCompletedLabel);
            return;
        }

        if (anyWrongNow)
            _remainingWrongWaySeconds -= Time.deltaTime;

        if (_remainingWrongWaySeconds <= 0f)
        {
            _remainingWrongWaySeconds = 0f;
            _countdownCompleted = true;
            ApplyGlowVisual(false, true);
            SetCountdownDisplayText(countdownCompletedLabel);
            return;
        }

        if (!anyWrongNow && resetCountdownWhenWrongWayEnds)
            _remainingWrongWaySeconds = Mathf.Max(0f, wrongWayCountdownSeconds);

        int showSec = Mathf.CeilToInt(_remainingWrongWaySeconds);
        SetCountdownDisplayText(string.Format(countdownFormat, showSec));
        ApplyGlowVisual(anyWrongNow, false);
    }

    void ApplyGlowVisual(bool wrongWayPulse, bool completed)
    {
        Color c;
        if (completed || !wrongWayPulse)
            c = glowColorNormal;
        else
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseSpeed);
            c = Color.Lerp(glowColorWrongWay, glowColorWrongWayBright, t);
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
        if (countdownTmpText != null)
            countdownTmpText.text = text;
        if (countdownUiText != null)
            countdownUiText.text = text;
    }

    void UpdateCountdownTexts()
    {
        if (_countdownCompleted)
            SetCountdownDisplayText(countdownCompletedLabel);
        else
            SetCountdownDisplayText(string.Format(countdownFormat, Mathf.CeilToInt(_remainingWrongWaySeconds)));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        wrongWayCountdownSeconds = Mathf.Max(0.1f, wrongWayCountdownSeconds);
    }
#endif
}
