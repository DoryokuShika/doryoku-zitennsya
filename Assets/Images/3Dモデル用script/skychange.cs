using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skychange : MonoBehaviour
{

    [SerializeField] float chengeTime_morning = 2f;
    [SerializeField] float chengeTime_daytime = 2f;
    [SerializeField] float chengeTime_night = 2f;



    [SerializeField] Material morning_empty;
    [SerializeField] Material daytime_empty;
    [SerializeField] Material night_empty;
    //[SerializeField] Material nomal;

    float totalTime_daytime;
    float totalTime_night;
    // Start is called before the first frame update
    void Start()
    {
         totalTime_daytime = chengeTime_morning + chengeTime_daytime;
         totalTime_night = chengeTime_morning + chengeTime_daytime + chengeTime_night;
    }
    float timer = 0f;
    //int state = 0;
    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= totalTime_night)
        {
            timer = 0f;

        }
            changeTimesky();
       

    }
     void changeTimesky()
    {
        if (timer >= totalTime_daytime)
        {
            RenderSettings.skybox = night_empty;
        }
        else if (timer >= chengeTime_morning)
        {
            RenderSettings.skybox = daytime_empty;
        }
        else
        {
            RenderSettings.skybox = morning_empty;
        }
    }
        
}
