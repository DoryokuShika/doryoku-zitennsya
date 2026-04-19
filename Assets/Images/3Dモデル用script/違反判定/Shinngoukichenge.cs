using UnityEngine;

/// <summary>
/// 信号の切り替え（コミット 04958ba より復元）。シーンの GUID 3751e857… と互換。
/// ShingouMushi 用に <see cref="State"/> は「元の state が 0（赤）のとき 1」。
/// </summary>
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

    /// <summary>元の state が 0（赤）のとき 1。ShingouMushi が参照。</summary>
    public int State => state == 0 ? 1 : 0;

    void Start()
    {
        redTime = blueTime + yellowTime;
        startTime = redTime;
        timer += 3f;
        if (useStartDelay)
            timer = -startTime;
        else
            timer = 0f;
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
            Chenge();
            timer = 0f;
        }
    }

    void Chenge()
    {
        if (state == 0) state = 1;
        else if (state == 1) state = 2;
        else if (state == 2) state = 0;

        redModel.GetComponent<Renderer>().material = nomal;
        blueModel.GetComponent<Renderer>().material = nomal;
        yellowModel.GetComponent<Renderer>().material = nomal;

        walkred.GetComponent<Renderer>().material = nomal;
        walkblue.GetComponent<Renderer>().material = nomal;
        walkred2.GetComponent<Renderer>().material = nomal;
        walkblue2.GetComponent<Renderer>().material = nomal;

        if (state == 0)
        {
            redModel.GetComponent<Renderer>().material = red;

            walkred.GetComponent<Renderer>().material = red;
            walkred2.GetComponent<Renderer>().material = red;
        }
        else if (state == 1)
        {
            blueModel.GetComponent<Renderer>().material = blue;

            walkblue.GetComponent<Renderer>().material = blue;
            walkblue2.GetComponent<Renderer>().material = blue;
        }
        else if (state == 2)
        {
            yellowModel.GetComponent<Renderer>().material = yellow;

            walkred.GetComponent<Renderer>().material = red;
            walkred2.GetComponent<Renderer>().material = red;
        }
    }
}
