using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class Gemeover : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            SceneManager.LoadScene("GameOverScene");
        }
        if (collision.gameObject.CompareTag("Car"))
        {
            SceneManager.LoadScene("DiedScene");
        }
        if (collision.gameObject.CompareTag("Walker"))
        {
            SceneManager.LoadScene("GameOverScene");
        }
    }
}
