using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BestTime : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI bestTimeText;

    void Start()
    {
        // ★保存されたベストタイムを取得して表示
        float best = ScoreManager.GetBestTime();
        int minutes = (int)(best / 60);
        int seconds = (int)(best % 60); 
        bestTimeText.text = string.Format("Best Time: {0:00}:{1:00}", minutes, seconds);
    }
}
