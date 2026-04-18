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
        timeText.text = Timer.timer.ToString("F2");
    }
}
