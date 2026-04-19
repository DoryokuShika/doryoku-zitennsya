using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // 次のシーン名
    [SerializeField] private float waitTime = 3.0f; // 待ち時間（秒）

    void Start()
    {
        // ゲーム開始時にカウントダウンを開始
        StartCoroutine(WaitAndChangeScene());
    }

    IEnumerator WaitAndChangeScene()
    {
        // 指定した秒数だけ待機
        yield return new WaitForSeconds(waitTime);

        // シーンを切り替える
        SceneManager.LoadScene(nextSceneName);
    }
}
