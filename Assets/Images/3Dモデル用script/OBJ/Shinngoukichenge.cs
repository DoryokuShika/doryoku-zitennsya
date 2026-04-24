using UnityEngine;

/// <summary>
/// Vehicle: green (blue, with optional blinking blue at end) -> yellow -> long all-red.
/// While vehicle is all-red, walker2 (walkblue2/walkred2) runs red -> solid green -> blinking green -> red until the red window ends.
/// Walker1 (walkblue/walkred) is green only while the vehicle is green, otherwise red.
/// State: 0 = vehicle red, 1 = vehicle green, 2 = vehicle yellow (unchanged for ShingouMushi).
/// </summary>
public class Shinngoukichenge : MonoBehaviour
{
    enum Phase
    {
        VehicleGreen,
        VehicleYellow,
        VehicleRoadAllRed,
    }

    [Header("Vehicle")]
    [Tooltip("Vehicle green (blue lamp) duration.")]
    [SerializeField] float blueTime = 5f;

    [Tooltip("Last part of vehicle green: blue lamp blinks before yellow (0 = stay solid whole time).")]
    [SerializeField] float vehicleBlueBlinkSeconds = 3f;

    [Tooltip("Vehicle blue blink toggle interval (seconds).")]
    [SerializeField] float vehicleBlueBlinkIntervalSeconds = 0.5f;

    [Tooltip("Vehicle yellow duration.")]
    [SerializeField] float yellowTime = 1f;

    [Tooltip("How long the vehicle stack stays all-red. Walker2 cycle runs inside this window.")]
    [SerializeField] float vehicleRoadAllRedSeconds = 13f;

    [Header("Walker2 (during vehicle all-red)")]
    [Tooltip("Walker2 stays red at the start of the vehicle-red window.")]
    [SerializeField] float walker2LeadRedSeconds = 1f;

    [Tooltip("Walker2 solid walk (blue) after lead red.")]
    [SerializeField] float walker2GreenSolidSeconds = 4f;

    [Tooltip("Walker2 blinking walk (blue on/off).")]
    [SerializeField] float walker2GreenBlinkSeconds = 4f;

    [Tooltip("Blink toggle interval (seconds).")]
    [SerializeField] float walker2BlinkIntervalSeconds = 0.5f;

    [Header("Optional")]
    [Tooltip("Steady cycle start shift (seconds). Wraps by one steady cycle length.")]
    [SerializeField] float cyclePhaseOffsetSeconds = 0f;

    [SerializeField] Material red;
    [SerializeField] Material blue;
    [SerializeField] Material yellow;
    [SerializeField] Material nomal;

    [SerializeField] GameObject redModel;
    [SerializeField] GameObject blueModel;
    [SerializeField] GameObject yellowModel;

    [SerializeField] GameObject walkred;
    [SerializeField] GameObject walkblue;

    [SerializeField] GameObject walkred2;
    [SerializeField] GameObject walkblue2;

    Phase _phase = Phase.VehicleGreen;
    float _phaseTimer;

    public int State
    {
        get
        {
            switch (_phase)
            {
                case Phase.VehicleGreen:
                    return 1;
                case Phase.VehicleYellow:
                    return 2;
                default:
                    return 0;
            }
        }
    }

    void OnValidate()
    {
        float minRed = Mathf.Max(0f, walker2LeadRedSeconds)
            + Mathf.Max(0.05f, walker2GreenSolidSeconds)
            + Mathf.Max(0.05f, walker2GreenBlinkSeconds);
        if (vehicleRoadAllRedSeconds < minRed)
            vehicleRoadAllRedSeconds = minRed;
    }

    void Start()
    {
        _phase = Phase.VehicleGreen;
        _phaseTimer = 0f;
        float wrapped = Mathf.Repeat(cyclePhaseOffsetSeconds, SteadyCycleDuration());
        ApplyTimeOffsetIntoPhases(wrapped);
        ApplyVisuals();
    }

    float SteadyCycleDuration()
    {
        return Mathf.Max(0.05f, blueTime)
            + Mathf.Max(0.05f, yellowTime)
            + Mathf.Max(0.05f, vehicleRoadAllRedSeconds);
    }

    void ApplyTimeOffsetIntoPhases(float remainingSeconds)
    {
        _phaseTimer = Mathf.Max(0f, remainingSeconds);
        const float eps = 0.0001f;
        while (_phaseTimer >= GetPhaseDuration(_phase) - eps)
        {
            float d = GetPhaseDuration(_phase);
            _phaseTimer -= d;
            AdvancePhase();
        }
    }

    void Update()
    {
        _phaseTimer += Time.deltaTime;
        float dur = GetPhaseDuration(_phase);
        if (_phaseTimer >= dur)
        {
            _phaseTimer -= dur;
            AdvancePhase();
        }

        ApplyVisuals();
    }

    float GetPhaseDuration(Phase p)
    {
        return p switch
        {
            Phase.VehicleGreen => Mathf.Max(0.05f, blueTime),
            Phase.VehicleYellow => Mathf.Max(0.05f, yellowTime),
            Phase.VehicleRoadAllRed => Mathf.Max(0.05f, vehicleRoadAllRedSeconds),
            _ => 0.05f,
        };
    }

    void AdvancePhase()
    {
        _phase = _phase switch
        {
            Phase.VehicleGreen => Phase.VehicleYellow,
            Phase.VehicleYellow => Phase.VehicleRoadAllRed,
            Phase.VehicleRoadAllRed => Phase.VehicleGreen,
            _ => Phase.VehicleGreen,
        };
    }

    void ApplyVisuals()
    {
        SetMaterial(redModel, nomal);
        SetMaterial(blueModel, nomal);
        SetMaterial(yellowModel, nomal);
        SetMaterial(walkred, nomal);
        SetMaterial(walkblue, nomal);
        SetMaterial(walkred2, nomal);
        SetMaterial(walkblue2, nomal);

        switch (_phase)
        {
            case Phase.VehicleGreen:
                ApplyVehicleGreenBlueLamp(_phaseTimer);
                SetMaterial(walkred2, red);
                SetMaterial(walkblue2, nomal);
                break;

            case Phase.VehicleYellow:
                SetMaterial(yellowModel, yellow);
                SetMaterial(walkred, red);
                SetMaterial(walkred2, red);
                break;

            case Phase.VehicleRoadAllRed:
                SetMaterial(redModel, red);
                SetMaterial(walkred, red);
                ApplyWalker2DuringVehicleRed(_phaseTimer);
                break;
        }
    }

    void ApplyVehicleGreenBlueLamp(float tInPhase)
    {
        float totalGreen = Mathf.Max(0.05f, blueTime);
        float blinkDur = Mathf.Clamp(
            Mathf.Max(0f, vehicleBlueBlinkSeconds),
            0f,
            Mathf.Max(0f, totalGreen - 0.05f));
        float solidEnd = totalGreen - blinkDur;

        bool solidOn = blinkDur <= 0f || tInPhase < solidEnd;
        if (solidOn)
        {
            SetMaterial(blueModel, blue);
            SetMaterial(walkblue, blue);
            return;
        }

        float blinkLocal = tInPhase - solidEnd;
        float interval = Mathf.Max(0.05f, vehicleBlueBlinkIntervalSeconds);
        bool lampOn = (Mathf.FloorToInt(blinkLocal / interval) % 2) == 0;
        SetMaterial(blueModel, lampOn ? blue : nomal);
        // Keep walkblue solid; it may be a different-facing / road-side lamp.
        SetMaterial(walkblue, blue);
    }

    void ApplyWalker2DuringVehicleRed(float t)
    {
        float lead = Mathf.Max(0f, walker2LeadRedSeconds);
        float g = Mathf.Max(0.05f, walker2GreenSolidSeconds);
        float b = Mathf.Max(0.05f, walker2GreenBlinkSeconds);
        float tGreenStart = lead;
        float tBlinkStart = lead + g;
        float tBlinkEnd = tBlinkStart + b;

        if (t < tGreenStart)
        {
            SetMaterial(walkred2, red);
            SetMaterial(walkblue2, nomal);
            return;
        }

        if (t < tBlinkStart)
        {
            SetMaterial(walkred2, nomal);
            SetMaterial(walkblue2, blue);
            return;
        }

        if (t < tBlinkEnd)
        {
            SetMaterial(walkred2, nomal);
            float blinkLocal = t - tBlinkStart;
            float interval = Mathf.Max(0.05f, walker2BlinkIntervalSeconds);
            bool on = (Mathf.FloorToInt(blinkLocal / interval) % 2) == 0;
            SetMaterial(walkblue2, on ? blue : nomal);
            return;
        }

        SetMaterial(walkred2, red);
        SetMaterial(walkblue2, nomal);
    }

    static void SetMaterial(GameObject go, Material mat)
    {
        if (go == null || mat == null)
            return;
        var r = go.GetComponent<Renderer>();
        if (r != null)
            r.material = mat;
    }
}
