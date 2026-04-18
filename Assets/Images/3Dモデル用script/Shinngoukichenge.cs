using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shinngoukichenge : MonoBehaviour
{
    [SerializeField] float chengeTime =2f;
    [SerializeField] float startTime = 8f;


    [SerializeField] Material red;
    [SerializeField] Material blue;
    [SerializeField] Material yellow;
    [SerializeField] Material nomal;


    [SerializeField] GameObject redModel;
    [SerializeField] GameObject blueModel;
    [SerializeField] GameObject yellowModel;
    [SerializeField] GameObject walkred;
    [SerializeField] GameObject walkblue;
    [SerializeField] GameObject walkred2;
    [SerializeField] GameObject walkblue2;
    int state = 0;
    float timer = 0f;
   
    void Start()
    {
        timer = -startTime +chengeTime;

    }
    void Update()
    {
        timer += Time.deltaTime;
        if (timer > chengeTime)
        {
            chenge();
            if (state == 0)
            {
                timer = 0f;
                timer += 2f;
            }
            else
            {
                timer = 0f;
            }
        }
    }
    
    void chenge()
    {
       
           if(state == 0)
            {
                yellowModel.GetComponent<Renderer>().material = nomal;
                redModel.GetComponent<Renderer>().material = red;
                
                
            state = 1;
            }
            else if(state == 1)
            {
                redModel.GetComponent<Renderer>().material = nomal;
                blueModel.GetComponent<Renderer>().material = blue;

                walkred.GetComponent<Renderer>().material = nomal;
                walkblue.GetComponent<Renderer>().material = blue;
                
                walkred2.GetComponent<Renderer>().material = nomal;
                walkblue2.GetComponent<Renderer>().material = blue;
            state = 2;
            }
            else if(state == 2)
            {
                blueModel.GetComponent<Renderer>().material = nomal;
                yellowModel.GetComponent<Renderer>().material = yellow;


                walkblue.GetComponent<Renderer>().material = nomal;
                walkred.GetComponent<Renderer>().material = red;

                walkblue2.GetComponent<Renderer>().material = nomal;
                walkred2.GetComponent<Renderer>().material = red;


            state = 0;
            }
            
    }
}
