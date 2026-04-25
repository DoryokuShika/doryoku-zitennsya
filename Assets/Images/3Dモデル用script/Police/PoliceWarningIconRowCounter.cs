using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PoliceLineOfSightCatch の警告回数に応じて、
/// インスペクターで指定した RawImage を順番に非表示→表示へ切り替えます。
/// </summary>
[DisallowMultipleComponent]
public class PoliceWarningIconRowCounter : MonoBehaviour
{
    [Header("表示対象（順番に 1,2,3 ... として扱う）")]
    [Tooltip("警告1回目で0番、2回目で1番、3回目で2番... を表示します。")]
    [SerializeField] RawImage[] warningSlots;

    [Header("リセット")]
    [Tooltip("OnEnable で回数を0に戻し、全 RawImage を非表示にする")]
    [SerializeField] bool resetOnEnable = true;

    [Header("青切符2枚到達時の表示")]
    [Tooltip("青切符2枚以上になったら表示するパネル")]
    [SerializeField] GameObject twoTicketsWarningPanel;
    [Tooltip("青切符2枚以上になったら表示する TMP テキスト")]
    [SerializeField] TMP_Text twoTicketsWarningTmp;
    [Tooltip("青切符2枚以上になったら表示する uGUI Text")]
    [SerializeField] Text twoTicketsWarningUi;
    [SerializeField] string twoTicketsWarningMessage = "青切符2枚です。運転中に警察に見られるとゲームオーバーになります。";

    [Header("青切符2枚到達後の遷移")]
    [Tooltip("2枚の状態で警告UIを閉じた瞬間に読み込むシーン名（Build Settings に登録）")]
    [SerializeField] string twoTicketsPoliceGameOverSceneName = "PoliceOver";

    int _warningCount;
    bool _gameOverTriggered;

    void OnEnable()
    {
        PoliceLineOfSightCatch.PoliceCatchShown += OnPoliceCatchShown;
        PoliceLineOfSightCatch.PoliceCatchClosed += OnPoliceCatchClosed;
        if (resetOnEnable)
            ResetCountAndIcons();
        ApplyTwoTicketsWarningText();
    }

    void OnDisable()
    {
        PoliceLineOfSightCatch.PoliceCatchShown -= OnPoliceCatchShown;
        PoliceLineOfSightCatch.PoliceCatchClosed -= OnPoliceCatchClosed;
    }

    void TriggerTwoTicketsPoliceGameOver()
    {
        Timer.isRunning = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (!string.IsNullOrWhiteSpace(twoTicketsPoliceGameOverSceneName))
            SceneManager.LoadScene(twoTicketsPoliceGameOverSceneName.Trim());
    }

    void OnPoliceCatchShown(PoliceCatchViolationKind _)
    {
        _warningCount++;
        int index = _warningCount - 1;
        if (warningSlots == null || index < 0 || index >= warningSlots.Length)
        {
            ApplyTwoTicketsWarningText();
            return;
        }

        if (warningSlots[index] != null)
            warningSlots[index].gameObject.SetActive(true);

        ApplyTwoTicketsWarningText();
    }

    void OnPoliceCatchClosed()
    {
        if (_gameOverTriggered || _warningCount < 2)
            return;
        _gameOverTriggered = true;
        TriggerTwoTicketsPoliceGameOver();
    }

    public void ResetCountAndSlots()
    {
        _warningCount = 0;
        _gameOverTriggered = false;
        if (warningSlots == null)
        {
            ApplyTwoTicketsWarningText();
            return;
        }

        for (int i = 0; i < warningSlots.Length; i++)
        {
            if (warningSlots[i] != null)
                warningSlots[i].gameObject.SetActive(false);
        }

        ApplyTwoTicketsWarningText();
    }

    // 旧メソッド名を残して既存の UnityEvent 参照を壊しにくくする
    public void ResetCountAndIcons() => ResetCountAndSlots();

    void ApplyTwoTicketsWarningText()
    {
        bool active = _warningCount >= 2;
        if (twoTicketsWarningPanel != null)
            twoTicketsWarningPanel.SetActive(active);

        if (twoTicketsWarningTmp != null)
        {
            twoTicketsWarningTmp.gameObject.SetActive(active);
            if (active)
                twoTicketsWarningTmp.text = twoTicketsWarningMessage;
        }
        if (twoTicketsWarningUi != null)
        {
            twoTicketsWarningUi.gameObject.SetActive(active);
            if (active)
                twoTicketsWarningUi.text = twoTicketsWarningMessage;
        }
    }

}
