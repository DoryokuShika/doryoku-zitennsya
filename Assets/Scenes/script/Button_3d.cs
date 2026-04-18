using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Button_3d : MonoBehaviour
{

    [SerializeField] string nextSceneName;
    [SerializeField] AudioSource audioSource;
    public void LoadMainScene()
    {
        
        if (audioSource != null)
        {
            StartCoroutine(PlaySoundAndLoadScene());
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    IEnumerator PlaySoundAndLoadScene()
    {       
        audioSource.Play();
        yield return new WaitForSeconds(audioSource.clip.length);
        SceneManager.LoadScene(nextSceneName);
    }
}