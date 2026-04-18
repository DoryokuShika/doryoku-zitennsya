using UnityEngine;
using UnityEngine.SceneManagement;

public class Button_3d : MonoBehaviour
{

    [SerializeField] string nextSceneName;
    public void LoadMainScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}