using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private const string BEST_TIME_KEY = "BestTime";

    // ベストタイムを保存する関数（今のタイムがベストより速ければ更新）
    public static void SaveBestTime(float currentTime)
    {
        // 過去のベストを取得（保存されていなければ非常に大きな値をデフォルトにする）
        float bestTime = PlayerPrefs.GetFloat(BEST_TIME_KEY, 9999f);

        if (currentTime < bestTime)
        {
            PlayerPrefs.SetFloat(BEST_TIME_KEY, currentTime);
            PlayerPrefs.Save();
            Debug.Log("ベストタイム更新！: " + currentTime);
        }
    }

    // ベストタイムを読み込む関数
    public static float GetBestTime()
    {
        return PlayerPrefs.GetFloat(BEST_TIME_KEY, 9999f);
    }

    // 保存データをリセットしたい時に使う関数（デバッグ用）
    public static void ResetScore()
    {
        PlayerPrefs.DeleteKey(BEST_TIME_KEY);
        Debug.Log("スコアをリセットしました");
    }
}