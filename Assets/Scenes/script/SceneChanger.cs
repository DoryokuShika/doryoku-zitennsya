using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // ���̃V�[����
    [SerializeField] private float waitTime = 3.0f; // �҂����ԁi�b�j

    void Start()
    {
        // �Q�[���J�n���ɃJ�E���g�_�E�����J�n
        StartCoroutine(WaitAndChangeScene());
    }

    IEnumerator WaitAndChangeScene()
    {
        // �w�肵���b�������ҋ@
        yield return new WaitForSeconds(waitTime);

        // �V�[����؂�ւ���
        SceneManager.LoadScene(nextSceneName);
    }
}
