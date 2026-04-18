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
        bestTimeText.text = "Best Time: " + best.ToString("F2") + "s";
    }
}
