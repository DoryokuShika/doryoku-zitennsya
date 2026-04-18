using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BestTimeReset : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    public void ResetScore()
    {

        if (audioSource != null)
        {
            StartCoroutine(PlaySoundAndResetScore());
        }
        else
        {

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }

    IEnumerator PlaySoundAndResetScore()
    {
        audioSource.Play();
        yield return new WaitForSeconds(audioSource.clip.length);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
