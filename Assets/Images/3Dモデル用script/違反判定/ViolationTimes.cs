using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViolationTimes : MonoBehaviour
{
    public static bool isShingouMushi = false;
    // ‚à‚µ‘¼‚É‚à‘‚â‚µ‚½‚¢‚È‚çA‚±‚¤‚â‚Á‚Ä’Ç‰Á‚µ‚Ä‚¢‚¯‚Ü‚·
    // public static bool isSpeedViolation = false;

    // ‚·‚×‚Ä‚Ìˆá”½‚ª true ‚©‚Ç‚¤‚©‚ğ”»’è‚·‚é
    public static bool IsAllViolationsComplete()
    {
        // ‚·‚×‚Ä‚Ì€–Ú‚ğ && (‚©‚Â) ‚Å‚Â‚È‚®
        if (isShingouMushi)
        {
            return true;
        }
        return false;
    }
    public static void ResetAll()
    {
        isShingouMushi = false;
    }
}
