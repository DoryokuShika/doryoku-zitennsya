using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shinngoukichenge : MonoBehaviour
{
    [SerializeField] float blueTime = 5f;
    [SerializeField] float yellowTime = 2f;
    [SerializeField] bool useStartDelay = true;
    float startTime;
    float redTime;


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


        redTime = blueTime + yellowTime;
        startTime = redTime;
        timer += 3f;
        if (useStartDelay)
        {
            timer = -startTime;
        }
        else
        {
            timer = 0f;


        }
    }
    void Update()
    {

        timer += Time.deltaTime;

        float currentTime = 0f;

        if (state == 0) currentTime = redTime;
        else if (state == 1) currentTime = blueTime;
        else if (state == 2) currentTime = yellowTime;

        if (timer > currentTime)
        {
            chenge();
            timer = 0f;
        }

    }
    
    void chenge()
    {

        // 状態を先に進める
        if (state == 0) state = 1;
        else if (state == 1) state = 2;
        else if (state == 2) state = 0;

        // 全部リセット
        redModel.GetComponent<Renderer>().material = nomal;
        blueModel.GetComponent<Renderer>().material = nomal;
        yellowModel.GetComponent<Renderer>().material = nomal;

        walkred.GetComponent<Renderer>().material = nomal;
        walkblue.GetComponent<Renderer>().material = nomal;
        walkred2.GetComponent<Renderer>().material = nomal;
        walkblue2.GetComponent<Renderer>().material = nomal;

        // 状態に応じて表示
        if (state == 0) // 赤
        {
            redModel.GetComponent<Renderer>().material = red;

            walkred.GetComponent<Renderer>().material = red;
            walkred2.GetComponent<Renderer>().material = red;
        }
        else if (state == 1) // 青
        {
            blueModel.GetComponent<Renderer>().material = blue;

            walkblue.GetComponent<Renderer>().material = blue;
            walkblue2.GetComponent<Renderer>().material = blue;
        }
        else if (state == 2) // 黄
        {
            yellowModel.GetComponent<Renderer>().material = yellow;

            walkred.GetComponent<Renderer>().material = red;
            walkred2.GetComponent<Renderer>().material = red;
        }

    }
}
