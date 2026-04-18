using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Gemeover : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            GameOver("GameOverScene");
        }
        else if (collision.gameObject.CompareTag("Car"))
        {
            GameOver("DiedScene");
        }
        else if (collision.gameObject.CompareTag("Walker"))
        {
            GameOver("GameOverScene");
        }
    }

    void GameOver(string sceneName)
    {
        ShowCursor();
        SceneManager.LoadScene(sceneName);
    }

    void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}