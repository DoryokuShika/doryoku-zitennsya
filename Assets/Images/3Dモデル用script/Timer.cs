using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Timer : MonoBehaviour
{
    public static float timer = 0;
    public static bool isRunning = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticRuntimeState()
    {
        timer = 0f;
        isRunning = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        timer = 0;
        isRunning = true;
    }
    
    // Update is called once per frame
    void Update()
    {
        if (isRunning) // ƒtƒ‰ƒO‚ªtrue‚Ì‚Æ‚«‚¾‚¯ŠÔ‚ği‚ß‚é
        {
            timer += Time.deltaTime;
        }
    }
}
