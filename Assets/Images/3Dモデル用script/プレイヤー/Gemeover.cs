using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class Gemeover : MonoBehaviour
{
    /// <summary>
    /// 歩行者（Walker）接触と同じゲームオーバー。スクリプトから明示的に呼ぶ場合に使用します。
    /// </summary>
    public static void TriggerWalkerCollisionGameOver()
    {
        Timer.isRunning = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene("GameOverScene");
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Timer.isRunning = false;
            ShowCursor();
            SceneManager.LoadScene("GameOverScene");
        }
        if (collision.gameObject.CompareTag("Car"))
        {
            Timer.isRunning = false;
            ShowCursor();
            SceneManager.LoadScene("DiedScene");
        }
        if (collision.gameObject.CompareTag("Walker"))
            TriggerWalkerCollisionGameOver();
    }

    void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
