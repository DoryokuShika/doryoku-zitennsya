using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShingouMushi : MonoBehaviour
{
    [Tooltip("Collider for mushi check. If empty, uses this GameObject.")]
    [SerializeField] GameObject MushiDecision;

    [Tooltip("Traffic signal script. If this slot is None, use Shinngoukired Object below.")]
    [SerializeField] Shinngoukichenge shinngoukired;

    [Tooltip("GameObject that has Shinngoukichenge (used when component ref above is None).")]
    [SerializeField] GameObject shinngoukiredObject;

    [Header("????????????")]
    [SerializeField] TMP_Text signalViolationCompleteTmp;
    [SerializeField] Text signalViolationCompleteUi;
    [SerializeField] string signalViolationCompleteLabel = "????";
    [SerializeField] Color signalViolationCompleteTextColor = Color.red;

    [Header("?????????????????????????????")]
    [SerializeField] TMP_Text[] additionalTmpTurnRedOnComplete;
    [SerializeField] Text[] additionalUiTurnRedOnComplete;

    Collider decisionCollider;
    bool _playerInsideMushiZone;

    /// <summary>?????????????????????????????????</summary>
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

        if (ViolationTimes.isShingouMushi)
        {
            Timer.isRunning = false;
            ShowCursor();
            SceneManager.LoadScene("GameOverScene");
            return;
        }

        ViolationTimes.isShingouMushi = true;
        ApplySignalViolationCompleteText();
        ViolationTimes.NotifySignalViolationComplete();
        PlayerViolationState.NotifySignalViolationMoment(1f);
    }

    void ApplySignalViolationCompleteText()
    {
        if (signalViolationCompleteTmp != null)
        {
            signalViolationCompleteTmp.text = signalViolationCompleteLabel;
            signalViolationCompleteTmp.color = signalViolationCompleteTextColor;
        }
        if (signalViolationCompleteUi != null)
        {
            signalViolationCompleteUi.text = signalViolationCompleteLabel;
            signalViolationCompleteUi.color = signalViolationCompleteTextColor;
        }

        if (additionalTmpTurnRedOnComplete != null)
        {
            foreach (var t in additionalTmpTurnRedOnComplete)
            {
                if (t != null)
                    t.color = signalViolationCompleteTextColor;
            }
        }
        if (additionalUiTurnRedOnComplete != null)
        {
            foreach (var t in additionalUiTurnRedOnComplete)
            {
                if (t != null)
                    t.color = signalViolationCompleteTextColor;
            }
        }
    }

    void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }


}
