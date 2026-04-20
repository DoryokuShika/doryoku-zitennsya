using TMPro;
using UnityEngine.UI;

/// <summary>
/// 違反成立時に反則金などの文字列を TMP / uGUI Text にまとめて流し込みます。
/// </summary>
public static class ViolationFineAmountDisplay
{
    public static void SetFineText(
        string text,
        TMP_Text singleTmp,
        Text singleUi,
        TMP_Text[] extraTmp,
        Text[] extraUi)
    {
        if (singleTmp != null)
            singleTmp.text = text;
        if (singleUi != null)
            singleUi.text = text;
        if (extraTmp != null)
        {
            for (int i = 0; i < extraTmp.Length; i++)
            {
                if (extraTmp[i] != null)
                    extraTmp[i].text = text;
            }
        }
        if (extraUi != null)
        {
            for (int i = 0; i < extraUi.Length; i++)
            {
                if (extraUi[i] != null)
                    extraUi[i].text = text;
            }
        }
    }
}
