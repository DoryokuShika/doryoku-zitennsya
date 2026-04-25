using System.Collections;
using UnityEngine;

public class Exitgame : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;

    [Tooltip("オン: 効果音を待たずに即座に終了します")]
    [SerializeField] bool quitImmediately = true;

    [Tooltip("オフ時、効果音待ちの最大秒数（長すぎるクリップ対策）")]
    [SerializeField] float maxWaitSecondsBeforeQuit = 2f;

    [Tooltip("Windows ビルドで Application.Quit が効かないとき、追い打ちでプロセスを強制終了")]
    [SerializeField] bool forceKillProcessOnWindowsBuild = true;

    [Tooltip("強制終了までの遅延秒（小さくするほど即座に閉じる）")]
    [SerializeField] float forceKillDelaySeconds = 0.2f;

    static bool _quitRequested;

    public void ExitGame()
    {
        if (_quitRequested)
            return;

        Debug.Log("[Exitgame] ExitGame() called.");

        if (!quitImmediately && audioSource != null && audioSource.clip != null)
        {
            // GameObject が破棄されてもコルーチン継続させたいので
            // DontDestroyOnLoad に移してから再生・終了する
            DontDestroyOnLoad(gameObject);
            StartCoroutine(PlaySoundAndQuit());
        }
        else
        {
            QuitGame();
        }
    }

    IEnumerator PlaySoundAndQuit()
    {
        if (audioSource != null)
            audioSource.Play();

        float wait = audioSource != null && audioSource.clip != null
            ? Mathf.Min(audioSource.clip.length, Mathf.Max(0f, maxWaitSecondsBeforeQuit))
            : 0f;
        yield return new WaitForSecondsRealtime(wait);

        QuitGame();
    }

    public void QuitGame()
    {
        if (_quitRequested)
            return;
        _quitRequested = true;

        Debug.Log("[Exitgame] QuitGame() invoked. Calling Application.Quit().");

        // 1) まず Unity に終了をリクエスト（クリーンアップを走らせる）
        Application.Quit();

#if UNITY_EDITOR
        // エディタではプロセスを殺さず、再生モードを止める
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE_WIN
        // 2) Windows ビルドでは Application.Quit が効かないことがあるので
        //    確実にプロセスを終了させる。コルーチンに頼らず
        //    Invoke を使うことで、シーン遷移や GameObject 破棄の影響を受けにくくする。
        if (forceKillProcessOnWindowsBuild)
        {
            // GameObject が破棄されても、グローバルなランナーから呼ぶ
            ExitgameForceKillRunner.Schedule(Mathf.Max(0f, forceKillDelaySeconds));
        }
#elif UNITY_STANDALONE
        // 他プラットフォームの standalone でも保険
        if (forceKillProcessOnWindowsBuild)
            ExitgameForceKillRunner.Schedule(Mathf.Max(0f, forceKillDelaySeconds));
#endif
    }
}

/// <summary>
/// シーン遷移や GameObject 破棄に影響されないグローバルなランナー。
/// Application.Quit() が効かなかった場合のフェイルセーフとしてプロセスを強制終了する。
/// </summary>
internal class ExitgameForceKillRunner : MonoBehaviour
{
    static ExitgameForceKillRunner _instance;
    float _killAt;
    bool _scheduled;

    public static void Schedule(float delaySeconds)
    {
        if (_instance == null)
        {
            var go = new GameObject("[ExitgameForceKillRunner]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ExitgameForceKillRunner>();
        }
        _instance._killAt = Time.realtimeSinceStartup + delaySeconds;
        _instance._scheduled = true;
        Debug.Log($"[Exitgame] Force-kill scheduled in {delaySeconds:F2}s.");
    }

    void Update()
    {
        if (!_scheduled)
            return;
        if (Time.realtimeSinceStartup < _killAt)
            return;

        _scheduled = false;
        Debug.Log("[Exitgame] Force-killing process now.");

        try
        {
            // 念のためもう一度
            Application.Quit();
        }
        catch { /* ignore */ }

        try
        {
            // 確実に終了させる二段構え
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        catch
        {
            // 権限等で Kill が失敗してもここで Environment.Exit に倒す
            System.Environment.Exit(0);
        }
    }
}
