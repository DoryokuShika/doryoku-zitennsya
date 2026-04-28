using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BestTime : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI bestTimeText;

    void Start()
    {
        // ���ۑ����ꂽ�x�X�g�^�C�����擾���ĕ\��
        float best = ScoreManager.GetBestTime();
        int minutes = (int)(best / 60);
        int seconds = (int)(best % 60); 
        bestTimeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
