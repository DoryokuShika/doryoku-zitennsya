using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 昼→夕→夜→朝→昼を時間で繰り返し、Built-in レンダラ想定で環境光・太陽（Directional）・フォグを切り替えます。
/// 各フェーズはインスペクターで色・角度・秒数を空気感に合わせて調整してください（夕と朝は同系色でよい）。
/// </summary>
[DefaultExecutionOrder(5)]
public class DayNightCycleController : MonoBehaviour
{
    public enum PhaseId
    {
        Day = 0,
        Evening = 1,
        Night = 2,
        Dawn = 3,
    }

    [System.Serializable]
    public class PhaseSettings
    {
        [Tooltip("このフェーズが続く秒数")]
        [Min(1f)] public float durationSeconds = 120f;

        [Header("Ambient (Built-in Trilight)")]
        public Color ambientSky = new Color(0.55f, 0.65f, 0.85f);
        public Color ambientEquator = new Color(0.45f, 0.45f, 0.42f);
        public Color ambientGround = new Color(0.32f, 0.28f, 0.24f);

        [Header("Directional light (sun)")]
        public Color sunColor = Color.white;
        [Range(0f, 3f)] public float sunIntensity = 1.1f;
        [Tooltip("太陽ライトのローカルオイラー角（シーンの向きに合わせて調整）")]
        public Vector3 sunLocalEulerAngles = new Vector3(50f, -30f, 0f);

        [Header("Fog (optional)")]
        public bool overrideFog;
        public Color fogColor = new Color(0.5f, 0.52f, 0.58f);
        [Range(0f, 0.05f)] public float fogDensity;
    }

    [Header("Phases (順: 昼→夕→夜→朝)")]
    [SerializeField] PhaseSettings day = new PhaseSettings
    {
        durationSeconds = 180f,
        ambientSky = new Color(0.55f, 0.72f, 1f),
        ambientEquator = new Color(0.48f, 0.5f, 0.45f),
        ambientGround = new Color(0.35f, 0.32f, 0.28f),
        sunColor = new Color(1f, 0.96f, 0.88f),
        sunIntensity = 1.2f,
        sunLocalEulerAngles = new Vector3(55f, -40f, 0f),
        overrideFog = false,
    };

    [SerializeField] PhaseSettings evening = new PhaseSettings
    {
        durationSeconds = 90f,
        ambientSky = new Color(0.85f, 0.55f, 0.35f),
        ambientEquator = new Color(0.55f, 0.42f, 0.38f),
        ambientGround = new Color(0.32f, 0.26f, 0.22f),
        sunColor = new Color(1f, 0.65f, 0.35f),
        sunIntensity = 0.85f,
        sunLocalEulerAngles = new Vector3(18f, -40f, 0f),
        overrideFog = true,
        fogColor = new Color(0.55f, 0.38f, 0.32f),
        fogDensity = 0.008f,
    };

    [SerializeField] PhaseSettings night = new PhaseSettings
    {
        durationSeconds = 120f,
        ambientSky = new Color(0.12f, 0.14f, 0.22f),
        ambientEquator = new Color(0.08f, 0.09f, 0.12f),
        ambientGround = new Color(0.04f, 0.04f, 0.06f),
        sunColor = new Color(0.55f, 0.6f, 0.85f),
        sunIntensity = 0.15f,
        sunLocalEulerAngles = new Vector3(8f, -40f, 0f),
        overrideFog = true,
        fogColor = new Color(0.04f, 0.05f, 0.1f),
        fogDensity = 0.02f,
    };

    [SerializeField] PhaseSettings dawn = new PhaseSettings
    {
        durationSeconds = 90f,
        ambientSky = new Color(0.75f, 0.5f, 0.45f),
        ambientEquator = new Color(0.5f, 0.42f, 0.4f),
        ambientGround = new Color(0.3f, 0.26f, 0.22f),
        sunColor = new Color(1f, 0.72f, 0.45f),
        sunIntensity = 0.75f,
        sunLocalEulerAngles = new Vector3(12f, -40f, 0f),
        overrideFog = true,
        fogColor = new Color(0.5f, 0.4f, 0.38f),
        fogDensity = 0.006f,
    };

    [Header("Sun")]
    [Tooltip("空なら RenderSettings.sun、なければシーン内の Directional を検索")]
    [SerializeField] Light directionalSun;

    [Tooltip("一時停止中も時間を進める")]
    [SerializeField] bool advanceTimeWhileTrafficPaused;

    int _phaseIndex;
    float _elapsedInPhase;

    /// <summary>現在のフェーズ（昼以外を「暗め時間帯」とみなす用途）。</summary>
    public PhaseId CurrentPhase => (PhaseId)Mathf.Clamp(_phaseIndex, 0, 3);

    /// <summary>夕・夜・朝のいずれか（ライト未点灯警告の判定用）。</summary>
    public bool IsDarkishDrivingPhase => CurrentPhase != PhaseId.Day;

    PhaseSettings PhaseAt(int i)
    {
        return i switch
        {
            0 => day,
            1 => evening,
            2 => night,
            _ => dawn,
        };
    }

    void Awake()
    {
        if (directionalSun == null)
        {
            directionalSun = RenderSettings.sun;
            if (directionalSun == null)
            {
                var lights = FindObjectsOfType<Light>();
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Directional)
                    {
                        directionalSun = l;
                        break;
                    }
                }
            }
        }
    }

    void Start()
    {
        _phaseIndex = 0;
        _elapsedInPhase = 0f;
        ApplyCurrentPhaseVisuals();
    }

    void Update()
    {
        if (!advanceTimeWhileTrafficPaused && MobTrafficPause.IsFrozen)
            return;

        PhaseSettings p = PhaseAt(_phaseIndex);
        float dur = Mathf.Max(1f, p.durationSeconds);
        _elapsedInPhase += Time.deltaTime;
        if (_elapsedInPhase >= dur)
        {
            _elapsedInPhase = 0f;
            _phaseIndex = (_phaseIndex + 1) % 4;
            ApplyCurrentPhaseVisuals();
        }
    }

    void ApplyCurrentPhaseVisuals()
    {
        PhaseSettings p = PhaseAt(_phaseIndex);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;

        if (directionalSun != null)
        {
            directionalSun.color = p.sunColor;
            directionalSun.intensity = p.sunIntensity;
            directionalSun.transform.localEulerAngles = p.sunLocalEulerAngles;
        }

        if (p.overrideFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Max(0f, p.fogDensity);
        }
        else
        {
            RenderSettings.fog = false;
        }
    }
}
