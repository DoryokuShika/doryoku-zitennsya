using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShingouMushi : MonoBehaviour
{
    [Tooltip("Collider for mushi check. If empty, uses this GameObject.")]
    [SerializeField] GameObject MushiDecision;

    [Tooltip("Traffic signal script. If this slot is None, use Shinngoukired Object below.")]
    [SerializeField] Shinngoukichenge shinngoukired;

    [Tooltip("GameObject that has Shinngoukichenge (used when component ref above is None).")]
    [SerializeField] GameObject shinngoukiredObject;

    Collider decisionCollider;

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

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player")) // ??F?G?????????Tag?t????
        {
            

            if (ViolationTimes.isShingouMushi == true)
            {
                Timer.isRunning = false;
                ShowCursor();
                SceneManager.LoadScene("GameOverScene");
            }
            else
            {
                ViolationTimes.isShingouMushi = true;
            }

            if (ViolationTimes.IsAllViolationsComplete())
            {
                Timer.isRunning = false;
                ScoreManager.SaveBestTime(Timer.timer);
                ClearTime = Timer.timer;
                ShowCursor();
                SceneManager.LoadScene("Clear");
            }

        }
    }

    void ShowCursor()
    {
        Cursor.visible = true; // ?J?[?\???\??
        Cursor.lockState = CursorLockMode.None; // ???b?N????
    }


}
