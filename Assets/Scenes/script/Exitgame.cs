using System.Collections;
using UnityEngine;

public class Exitgame : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;

    public void ExitGame()
    {
        if (audioSource != null)
        {
            // StartCoroutineで呼ぶ名前を下の関数名と合わせます
            StartCoroutine(PlaySoundAndQuit());
        }
        else
        {
            // 音源がない場合は即終了
            QuitGame();
        }
    }

    IEnumerator PlaySoundAndQuit()
    {
        // 1. 音を鳴らす
        audioSource.Play();

        // 2. 音の長さ分だけ待つ
        yield return new WaitForSeconds(audioSource.clip.length);

        // 3. ゲーム終了
        QuitGame();
    }

    public void QuitGame()
    {
        // 実際のゲーム（ビルド後）で終了させる
        Application.Quit();

        // Unityエディタ上では終了しないので、確認用にログを出す
        Debug.Log("ゲームを終了しました");
    }
}