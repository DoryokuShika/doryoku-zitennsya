using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GemeOversound : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.playOnAwake = false;
        audioSource.Play();
    }
}
