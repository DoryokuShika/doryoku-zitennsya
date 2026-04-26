using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimeText : MonoBehaviour
{
    [SerializeField] Text timeText;
   
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        int minutes = (int)(Timer.timer / 60);
        int seconds = (int)(Timer.timer % 60);
        timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
