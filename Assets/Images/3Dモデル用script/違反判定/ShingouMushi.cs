using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ?????????????????????????????????????
/// ????????????????????????????????????????????
/// </summary>
[DefaultExecutionOrder(25)]
public class ShingouMushi : MonoBehaviour
{
    [Tooltip("Collider for mushi check. If empty, uses this GameObject.")]
    [SerializeField] GameObject MushiDecision;

    [Tooltip("Traffic signal script. If this slot is None, use Shinngoukired Object below.")]
    [SerializeField] Shinngoukichenge shinngoukired;

    [Tooltip("GameObject that has Shinngoukichenge (used when component ref above is None).")]
    [SerializeField] GameObject shinngoukiredObject;

    [Header("????????????????")]
    [SerializeField] TMP_Text signalViolationCompleteTmp;
    [SerializeField] Text signalViolationCompleteUi;
    [SerializeField] string signalViolationCompleteLabel = "??";
    [SerializeField] Color signalViolationCompleteTextColor = Color.red;

    [SerializeField] TMP_Text[] additionalTmpTurnRedOnComplete;
    [SerializeField] Text[] additionalUiTurnRedOnComplete;

    [Header("?????????????????")]
    [SerializeField] string fineAmountText = "6000?";
    [SerializeField] TMP_Text fineAmountDisplayTmp;
    [SerializeField] Text fineAmountDisplayUi;
    [SerializeField] TMP_Text[] additionalFineAmountTmp;
    [SerializeField] Text[] additionalFineAmountUi;

    [Header("??????????????")]
    [Tooltip("???????????????????????????? TMP")]
    [SerializeField] TMP_Text completedWhenNotSpottedTmp;
    [Tooltip("???????????????????????????? uGUI Text")]
    [SerializeField] Text completedWhenNotSpottedUi;
    [SerializeField] string completedWhenNotSpottedLabel = "??";
    [SerializeField] Color completedWhenNotSpottedTextColor = Color.white;

    [SerializeField] TMP_Text[] additionalTmpOnNotSpottedComplete;
    [SerializeField] Text[] additionalUiOnNotSpottedComplete;
    [SerializeField] Color additionalNotSpottedTextColor = Color.white;

    [Header("??????????????")]
    [Tooltip("???????????????????????????? UI ?????????")]
    [SerializeField] bool showCatchUiWhenPoliceSeePlayerOnMushiComplete = true;

    Collider decisionCollider;
    bool _playerInsideMushiZone;
    bool _pendingResolutionAfterEnter;

    public bool IsActiveSignalViolationNow()
    {
        if (shinngoukired == null || decisionCollider == null)
            return false;
        if (!_playerInsideMushiZone)
            return false;
        return shinngoukired.State != 1;
    }

    void Awake()
    {
        if (MushiDecision == null)
            MushiDecision = gameObject;

        if (shinngoukired == null && shinngoukiredObject != null)
            shinngoukired = shinngoukiredObject.GetComponent<Shinngoukichenge>();

        if (MushiDecision != null)
            decisionCollider = MushiDecision.GetComponent<Collider>();
    }

    void Update()
    {
        if (shinngoukired == null || decisionCollider == null)
            return;

        decisionCollider.enabled = shinngoukired.State != 1;
    }

    public static float ClearTime = 0;

    void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
            return;
        _playerInsideMushiZone = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Player"))
            return;

        _playerInsideMushiZone = true;

        if (ViolationTimes.SignalViolationComplete)
            return;

        _pendingResolutionAfterEnter = true;
    }

    void LateUpdate()
    {
        if (!_pendingResolutionAfterEnter)
            return;

        if (!_playerInsideMushiZone)
        {
            _pendingResolutionAfterEnter = false;
            return;
        }

        if (ViolationTimes.SignalViolationComplete)
        {
            _pendingResolutionAfterEnter = false;
            return;
        }

        _pendingResolutionAfterEnter = false;

        bool policeSeesPlayerOnMushiFrame = PoliceLineOfSightState.IsTargetInPoliceSightNow;

        ViolationTimes.NotifySignalViolationComplete();
        ApplyCompletedVisualsAfterMushi();

        if (showCatchUiWhenPoliceSeePlayerOnMushiComplete && policeSeesPlayerOnMushiFrame)
        {
            ViolationFineAmountDisplay.SetFineText(
                fineAmountText,
                fineAmountDisplayTmp,
                fineAmountDisplayUi,
                additionalFineAmountTmp,
                additionalFineAmountUi);
            PlayerViolationState.NotifySignalViolationMoment(1f);
            PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice(PoliceCatchViolationKind.Signal);
        }
    }

    void ApplyCompletedVisualsAfterMushi()
    {
        if (completedWhenNotSpottedTmp != null)
        {
            completedWhenNotSpottedTmp.text = completedWhenNotSpottedLabel;
            completedWhenNotSpottedTmp.color = completedWhenNotSpottedTextColor;
        }
        if (completedWhenNotSpottedUi != null)
        {
            completedWhenNotSpottedUi.text = completedWhenNotSpottedLabel;
            completedWhenNotSpottedUi.color = completedWhenNotSpottedTextColor;
        }

        if (signalViolationCompleteTmp != null)
        {
            signalViolationCompleteTmp.text = completedWhenNotSpottedLabel;
            signalViolationCompleteTmp.color = completedWhenNotSpottedTextColor;
        }
        if (signalViolationCompleteUi != null)
        {
            signalViolationCompleteUi.text = completedWhenNotSpottedLabel;
            signalViolationCompleteUi.color = completedWhenNotSpottedTextColor;
        }

        if (additionalTmpTurnRedOnComplete != null)
        {
            foreach (var t in additionalTmpTurnRedOnComplete)
            {
                if (t != null)
                {
                    t.text = completedWhenNotSpottedLabel;
                    t.color = completedWhenNotSpottedTextColor;
                }
            }
        }
        if (additionalUiTurnRedOnComplete != null)
        {
            foreach (var t in additionalUiTurnRedOnComplete)
            {
                if (t != null)
                {
                    t.text = completedWhenNotSpottedLabel;
                    t.color = completedWhenNotSpottedTextColor;
                }
            }
        }

        if (additionalTmpOnNotSpottedComplete != null)
        {
            foreach (var t in additionalTmpOnNotSpottedComplete)
            {
                if (t != null)
                {
                    t.text = completedWhenNotSpottedLabel;
                    t.color = additionalNotSpottedTextColor;
                }
            }
        }
        if (additionalUiOnNotSpottedComplete != null)
        {
            foreach (var t in additionalUiOnNotSpottedComplete)
            {
                if (t != null)
                {
                    t.text = completedWhenNotSpottedLabel;
                    t.color = additionalNotSpottedTextColor;
                }
            }
        }
    }
}
