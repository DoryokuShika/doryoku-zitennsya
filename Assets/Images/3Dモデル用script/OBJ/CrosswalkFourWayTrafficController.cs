using System;
using UnityEngine;

/// <summary>
/// 十字（上下・左右）4灯ずつの車両信号と、歩行者8灯×2系統を同期させます。
/// 主道路の車が青→黄→赤の半周期では「従道路を横断する歩灯（Secondary）」だけが青一定→青のみ点滅→赤、
/// 従道路の車の半周期では「主道路を横断する歩灯（Primary）」だけが同じパターンになります。
/// 歩行者の点滅は青ランプだけが消灯／点灯し、赤ランプは交互に赤を出しません。
/// </summary>
public class CrosswalkFourWayTrafficController : MonoBehaviour
{
    public enum RoadAxis
    {
        /// <summary>上・下を同一道路（同じ車両表示）として扱います。</summary>
        UpDown = 0,
        /// <summary>左・右を同一道路として扱います。</summary>
        LeftRight = 1,
    }

    enum VehicleHalfCycle
    {
        /// <summary>主道路（Primary Axis で選んだ軸）の車が青→黄→赤。</summary>
        PrimaryVehicleWindow = 0,
        /// <summary>従道路の車が青→黄→赤。</summary>
        SecondaryVehicleWindow = 1,
    }

    [Header("道路の割り当て")]
    [Tooltip("この軸が「主道路」。最初の半周期はこの軸の車が青→黄→赤になります。")]
    [SerializeField] RoadAxis primaryAxis = RoadAxis.UpDown;

    [Header("車両灯（各3色×上下左右）")]
    [SerializeField] Directional4 vehicleBlue = new Directional4();
    [SerializeField] Directional4 vehicleYellow = new Directional4();
    [SerializeField] Directional4 vehicleRed = new Directional4();

    [Header("歩行者灯（主道路側の横断・4方向×青/赤＝8）")]
    [SerializeField] PedestrianPerCrossing8 pedestrianPrimaryCrossing = new PedestrianPerCrossing8();

    [Header("歩行者灯（従道路側の横断・8）")]
    [SerializeField] PedestrianPerCrossing8 pedestrianSecondaryCrossing = new PedestrianPerCrossing8();

    [Header("マテリアル（車・歩行者ランプの Renderer に適用）")]
    [SerializeField] Material materialRed;
    [SerializeField] Material materialBlue;
    [SerializeField] Material materialYellow;
    [SerializeField] Material materialNormal;

    [Header("車両タイミング（各軸・フローチャート例: 21 / 2 / 2 秒）")]
    [Tooltip("車の青（進行）の秒数。")]
    [SerializeField] float vehicleBlueSeconds = 21f;
    [Tooltip("車の黄の秒数。")]
    [SerializeField] float vehicleYellowSeconds = 2f;
    [Tooltip("車の赤の秒数（次の軸の青の直前まで）。")]
    [SerializeField] float vehicleRedSeconds = 2f;

    [Header("歩行者タイミング（進めてよい側だけ・車の青0秒から。例: 12 / 7 秒）")]
    [Tooltip("進行中の半周期で「渡ってよい」側の歩行者の青（常灯）の秒数。")]
    [SerializeField] float pedestrianSolidBlueSeconds = 12f;
    [Tooltip("同じく青のみの点滅の秒数（赤ランプは点滅させません）。")]
    [SerializeField] float pedestrianBlinkBlueSeconds = 7f;
    [Tooltip("歩行者青点滅の切り替え間隔（秒）。")]
    [SerializeField] float pedestrianBlinkIntervalSeconds = 0.5f;

    [Header("開始オフセット")]
    [SerializeField] float cyclePhaseOffsetSeconds = 0f;

    VehicleHalfCycle _half = VehicleHalfCycle.PrimaryVehicleWindow;
    float _timer;

    /// <summary>主道路軸の車両状態。0=赤、1=青、2=黄（Shinngoukichenge と同じ）。</summary>
    public int PrimaryRoadVehicleState { get; private set; }

    /// <summary>従道路軸の車両状態。</summary>
    public int SecondaryRoadVehicleState { get; private set; }

    /// <summary>交差点の四辺（上・下・左・右）の車両信号。</summary>
    public enum CrosswalkSide
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// 指定した辺の「車両用」信号の状態（Shinngoukichenge.State と同じ: 0=赤、1=青、2=黄）。
    /// Primary Axis が上下なら上下が主道路軸、左右が従道路軸。Primary が左右なら逆。
    /// </summary>
    public int GetVehicleStateAtCrosswalkSide(CrosswalkSide side)
    {
        bool primaryIsUpDown = primaryAxis == RoadAxis.UpDown;
        bool sideIsUpDown = side == CrosswalkSide.Up || side == CrosswalkSide.Down;
        bool sideUsesPrimaryRoad = primaryIsUpDown == sideIsUpDown;
        return sideUsesPrimaryRoad ? PrimaryRoadVehicleState : SecondaryRoadVehicleState;
    }

    void OnValidate()
    {
        vehicleBlueSeconds = Mathf.Max(0.05f, vehicleBlueSeconds);
        vehicleYellowSeconds = Mathf.Max(0f, vehicleYellowSeconds);
        vehicleRedSeconds = Mathf.Max(0f, vehicleRedSeconds);
        pedestrianSolidBlueSeconds = Mathf.Max(0f, pedestrianSolidBlueSeconds);
        pedestrianBlinkBlueSeconds = Mathf.Max(0f, pedestrianBlinkBlueSeconds);
    }

    void Start()
    {
        _half = VehicleHalfCycle.PrimaryVehicleWindow;
        _timer = 0f;
        float cycle = FullCycleDuration();
        if (cycle > 0.0001f)
            _timer = Mathf.Repeat(cyclePhaseOffsetSeconds, cycle);
        ConsumeInitialOffset();
        ApplyVisuals();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        for (int guard = 0; guard < 24; guard++)
        {
            float need = CurrentHalfDuration();
            if (need <= 0f)
            {
                AdvanceHalf();
                continue;
            }

            if (_timer < need)
                break;
            _timer -= need;
            AdvanceHalf();
        }

        ApplyVisuals();
    }

    void ConsumeInitialOffset()
    {
        for (int guard = 0; guard < 128 && _timer > 0.0001f; guard++)
        {
            float need = CurrentHalfDuration();
            if (need <= 0f)
            {
                AdvanceHalf();
                continue;
            }

            if (_timer < need)
                break;
            _timer -= need;
            AdvanceHalf();
        }
    }

    float FullCycleDuration()
        => Mathf.Max(0.0001f, SegmentDuration()) * 2f;

    float SegmentDuration()
        => Mathf.Max(0.05f, vehicleBlueSeconds)
            + Mathf.Max(0f, vehicleYellowSeconds)
            + Mathf.Max(0f, vehicleRedSeconds);

    float CurrentHalfDuration()
        => SegmentDuration();

    void AdvanceHalf()
    {
        _half = _half == VehicleHalfCycle.PrimaryVehicleWindow
            ? VehicleHalfCycle.SecondaryVehicleWindow
            : VehicleHalfCycle.PrimaryVehicleWindow;
    }

    void ApplyVisuals()
    {
        ClearAllVehicleLamps();
        ClearAllPedestrianLamps();

        bool primaryIsUpDown = primaryAxis == RoadAxis.UpDown;
        bool primaryVehicleActive = _half == VehicleHalfCycle.PrimaryVehicleWindow;

        ApplyVehicles(_timer, primaryIsUpDown, primaryVehicleActive);
        ApplyPedestrianGroups(_timer, primaryVehicleActive);
    }

    void ApplyVehicles(float t, bool primaryIsUpDown, bool primaryVehicleActive)
    {
        float vb = Mathf.Max(0.05f, vehicleBlueSeconds);
        float vy = Mathf.Max(0f, vehicleYellowSeconds);
        float vr = Mathf.Max(0f, vehicleRedSeconds);

        bool greenPrimary = primaryVehicleActive;
        if (t < vb)
        {
            if (greenPrimary)
            {
                SetVehicleForAxis(primaryIsUpDown, blueOn: true, yellowOn: false, redOn: false, useRedWhenBlueOff: false);
                SetVehicleForAxis(!primaryIsUpDown, blueOn: false, yellowOn: false, redOn: true, useRedWhenBlueOff: true);
            }
            else
            {
                SetVehicleForAxis(!primaryIsUpDown, blueOn: true, yellowOn: false, redOn: false, useRedWhenBlueOff: false);
                SetVehicleForAxis(primaryIsUpDown, blueOn: false, yellowOn: false, redOn: true, useRedWhenBlueOff: true);
            }

            PrimaryRoadVehicleState = greenPrimary ? 1 : 0;
            SecondaryRoadVehicleState = greenPrimary ? 0 : 1;
            return;
        }

        if (t < vb + vy)
        {
            if (greenPrimary)
            {
                SetVehicleForAxis(primaryIsUpDown, blueOn: false, yellowOn: true, redOn: false, useRedWhenBlueOff: false);
                SetVehicleForAxis(!primaryIsUpDown, blueOn: false, yellowOn: false, redOn: true, useRedWhenBlueOff: true);
            }
            else
            {
                SetVehicleForAxis(!primaryIsUpDown, blueOn: false, yellowOn: true, redOn: false, useRedWhenBlueOff: false);
                SetVehicleForAxis(primaryIsUpDown, blueOn: false, yellowOn: false, redOn: true, useRedWhenBlueOff: true);
            }

            PrimaryRoadVehicleState = greenPrimary ? 2 : 0;
            SecondaryRoadVehicleState = greenPrimary ? 0 : 2;
            return;
        }

        // vb+vy ～ セグメント終わり: 両軸赤
        SetVehicleForAxis(primaryIsUpDown, blueOn: false, yellowOn: false, redOn: true, useRedWhenBlueOff: true);
        SetVehicleForAxis(!primaryIsUpDown, blueOn: false, yellowOn: false, redOn: true, useRedWhenBlueOff: true);
        PrimaryRoadVehicleState = 0;
        SecondaryRoadVehicleState = 0;
    }

    /// <summary>
    /// 主道路の車が動いている半周期では従道路横断（Secondary）だけ歩行者タイムライン、
    /// 従道路の車の半周期では主道路横断（Primary）だけ同じタイムライン。もう一方は常に赤。
    /// </summary>
    void ApplyPedestrianGroups(float t, bool primaryVehicleActive)
    {
        PedestrianPerCrossing8 releasing = primaryVehicleActive
            ? pedestrianSecondaryCrossing
            : pedestrianPrimaryCrossing;
        PedestrianPerCrossing8 holding = primaryVehicleActive
            ? pedestrianPrimaryCrossing
            : pedestrianSecondaryCrossing;

        ApplyPedestrianRedHold(holding);

        float solid = Mathf.Max(0f, pedestrianSolidBlueSeconds);
        float blink = Mathf.Max(0f, pedestrianBlinkBlueSeconds);
        float blinkEnd = solid + blink;

        if (t < solid)
        {
            ApplyPedestrianSolidWalk(releasing);
            return;
        }

        if (t < blinkEnd)
        {
            float blinkLocal = t - solid;
            bool blueOn = PedBlinkOn(blinkLocal, pedestrianBlinkIntervalSeconds);
            ApplyPedestrianBlinkBlueOnly(releasing, blueOn);
            return;
        }

        ApplyPedestrianRedHold(releasing);
    }

    void ApplyPedestrianSolidWalk(PedestrianPerCrossing8 p)
    {
        p.ForEachBlue(go => SetMaterial(go, materialBlue));
        p.ForEachRed(go => SetMaterial(go, materialNormal));
    }

    /// <summary>青だけが materialBlue と消灯を繰り返す。赤側は常に消灯（Normal）のまま。</summary>
    void ApplyPedestrianBlinkBlueOnly(PedestrianPerCrossing8 p, bool blueOn)
    {
        p.ForEachBlue(go => SetMaterial(go, blueOn ? materialBlue : materialNormal));
        p.ForEachRed(go => SetMaterial(go, materialNormal));
    }

    void ApplyPedestrianRedHold(PedestrianPerCrossing8 p)
    {
        ApplyPedestrianBlueGroup(p, false);
        ApplyPedestrianRedGroup(p, true);
    }

    static bool PedBlinkOn(float t, float interval)
    {
        float iv = Mathf.Max(0.05f, interval);
        return (Mathf.FloorToInt(t / iv) % 2) == 0;
    }

    void SetVehicleForAxis(bool isPrimaryAxis, bool blueOn, bool yellowOn, bool redOn, bool useRedWhenBlueOff)
    {
        bool useUpDown = primaryAxis == RoadAxis.UpDown ? isPrimaryAxis : !isPrimaryAxis;
        if (useUpDown)
        {
            ApplyDirPair(vehicleBlue.up, vehicleYellow.up, vehicleRed.up, blueOn, yellowOn, redOn, useRedWhenBlueOff);
            ApplyDirPair(vehicleBlue.down, vehicleYellow.down, vehicleRed.down, blueOn, yellowOn, redOn, useRedWhenBlueOff);
        }
        else
        {
            ApplyDirPair(vehicleBlue.left, vehicleYellow.left, vehicleRed.left, blueOn, yellowOn, redOn, useRedWhenBlueOff);
            ApplyDirPair(vehicleBlue.right, vehicleYellow.right, vehicleRed.right, blueOn, yellowOn, redOn, useRedWhenBlueOff);
        }
    }

    void ApplyDirPair(
        GameObject blueGo,
        GameObject yellowGo,
        GameObject redGo,
        bool blueOn,
        bool yellowOn,
        bool redOn,
        bool useRedWhenBlueOff)
    {
        bool showRed = redOn || (useRedWhenBlueOff && !blueOn && !yellowOn);
        SetMaterial(blueGo, blueOn ? materialBlue : materialNormal);
        SetMaterial(yellowGo, yellowOn ? materialYellow : materialNormal);
        SetMaterial(redGo, showRed ? materialRed : materialNormal);
    }

    void ClearAllVehicleLamps()
    {
        vehicleBlue.ForEach(go => SetMaterial(go, materialNormal));
        vehicleYellow.ForEach(go => SetMaterial(go, materialNormal));
        vehicleRed.ForEach(go => SetMaterial(go, materialNormal));
    }

    void ClearAllPedestrianLamps()
    {
        pedestrianPrimaryCrossing.ForEachBlue(go => SetMaterial(go, materialNormal));
        pedestrianPrimaryCrossing.ForEachRed(go => SetMaterial(go, materialNormal));
        pedestrianSecondaryCrossing.ForEachBlue(go => SetMaterial(go, materialNormal));
        pedestrianSecondaryCrossing.ForEachRed(go => SetMaterial(go, materialNormal));
    }

    void ApplyPedestrianBlueGroup(PedestrianPerCrossing8 p, bool blueLit)
    {
        p.ForEachBlue(go => SetMaterial(go, blueLit ? materialBlue : materialNormal));
    }

    void ApplyPedestrianRedGroup(PedestrianPerCrossing8 p, bool redLit)
    {
        p.ForEachRed(go => SetMaterial(go, redLit ? materialRed : materialNormal));
    }

    static void SetMaterial(GameObject go, Material mat)
    {
        if (go == null || mat == null)
            return;
        var r = go.GetComponent<Renderer>();
        if (r != null)
            r.material = mat;
    }

    [Serializable]
    public class Directional4
    {
        [Tooltip("上")] public GameObject up;
        [Tooltip("下")] public GameObject down;
        [Tooltip("左")] public GameObject left;
        [Tooltip("右")] public GameObject right;

        public void ForEach(Action<GameObject> action)
        {
            action?.Invoke(up);
            action?.Invoke(down);
            action?.Invoke(left);
            action?.Invoke(right);
        }
    }

    [Serializable]
    public class PedestrianPerCrossing8
    {
        [Tooltip("上・歩行者の青")] public GameObject upBlue;
        [Tooltip("上・歩行者の赤")] public GameObject upRed;
        [Tooltip("下・歩行者の青")] public GameObject downBlue;
        [Tooltip("下・歩行者の赤")] public GameObject downRed;
        [Tooltip("左・歩行者の青")] public GameObject leftBlue;
        [Tooltip("左・歩行者の赤")] public GameObject leftRed;
        [Tooltip("右・歩行者の青")] public GameObject rightBlue;
        [Tooltip("右・歩行者の赤")] public GameObject rightRed;

        public void ForEachBlue(Action<GameObject> action)
        {
            action?.Invoke(upBlue);
            action?.Invoke(downBlue);
            action?.Invoke(leftBlue);
            action?.Invoke(rightBlue);
        }

        public void ForEachRed(Action<GameObject> action)
        {
            action?.Invoke(upRed);
            action?.Invoke(downRed);
            action?.Invoke(leftRed);
            action?.Invoke(rightRed);
        }
    }
}
