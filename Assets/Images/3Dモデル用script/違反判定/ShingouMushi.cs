using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShingouMushi : MonoBehaviour
{

    [SerializeField] GameObject MushiDecision;
    [SerializeField] Shinngoukichenge shinngoukired;
    // Start is called before the first frame update

    private Collider decisionCollider;

    void Start()
    {
        decisionCollider = MushiDecision.GetComponent<Collider>();
        //MushiDecision.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        if(shinngoukired.State == 1)
        {
            decisionCollider.enabled = false;

        }
        else 
        {
            decisionCollider.enabled = true;
        }
    }
    public static float ClearTime = 0;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player")) // 例：触れたい相手にTag付ける
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
        Cursor.visible = true; // カーソル表示
        Cursor.lockState = CursorLockMode.None; // ロック解除
    }


}
