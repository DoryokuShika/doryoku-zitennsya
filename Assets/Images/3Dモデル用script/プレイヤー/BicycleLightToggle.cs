using UnityEngine;

/// <summary>
/// マウス中ボタン（ホイール押し）で、インスペクターに指定したオブジェクトの表示 ON/OFF を切り替えます（自転車ライト用）。
/// カーソルがロックされているときだけ反応します。
/// </summary>
[DisallowMultipleComponent]
public class BicycleLightToggle : MonoBehaviour
{
    [Tooltip("中クリックのたびにまとめて ON/OFF する GameObject（ライト用メッシュの親など）")]
    [SerializeField] GameObject[] lightTargets;

    [Tooltip("開始時の ON 状態を、最初の要素の activeSelf から取る。オフなら最初のトグルで ON になる")]
    [SerializeField] bool syncInitialStateFromFirstTarget = true;

    [Header("Bell sound (mouse wheel)")]
    [Tooltip("Input.GetAxis(\"Mouse ScrollWheel\") の絶対値がこの以上でベル音を鳴らす")]
    [SerializeField] float bellScrollTriggerAbs = 0.02f;
    [Tooltip("未指定ならこのオブジェクトの AudioSource を使用")]
    [SerializeField] AudioSource bellAudioSource;
    [Tooltip("ホイール回転時に鳴らすベル音")]
    [SerializeField] AudioClip bellScrollClip;
    [Range(0f, 1f)]
    [SerializeField] float bellVolume = 1f;

    [Header("Light toggle SFX")]
    [Tooltip("ライト切り替え時に効果音を鳴らす")]
    [SerializeField] bool playLightToggleSfx = true;
    [Tooltip("未指定なら bellAudioSource を流用し、さらに未指定ならこのオブジェクトの AudioSource を使用")]
    [SerializeField] AudioSource lightToggleSfxSource;
    [SerializeField] AudioClip lightToggleOnClip;
    [SerializeField] AudioClip lightToggleOffClip;
    [Range(0f, 1f)]
    [SerializeField] float lightToggleSfxVolume = 1f;

    bool _lightsOn = true;

    void Start()
    {
        if (syncInitialStateFromFirstTarget && lightTargets != null && lightTargets.Length > 0 && lightTargets[0] != null)
            _lightsOn = lightTargets[0].activeSelf;

        if (bellAudioSource == null)
            bellAudioSource = GetComponent<AudioSource>();
        if (lightToggleSfxSource == null)
            lightToggleSfxSource = bellAudioSource != null ? bellAudioSource : GetComponent<AudioSource>();
    }

    void Update()
    {
        if (MobTrafficPause.IsFrozen)
            return;
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (Input.GetMouseButtonDown(2))
            ToggleLights();

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) >= bellScrollTriggerAbs)
            PlayBellSound();
    }

    void ToggleLights()
    {
        _lightsOn = !_lightsOn;
        if (lightTargets == null)
            return;
        for (int i = 0; i < lightTargets.Length; i++)
        {
            if (lightTargets[i] != null)
                lightTargets[i].SetActive(_lightsOn);
        }

        PlayLightToggleSfx();
    }

    void PlayBellSound()
    {
        if (bellAudioSource == null || bellScrollClip == null)
            return;
        bellAudioSource.PlayOneShot(bellScrollClip, bellVolume);
    }

    void PlayLightToggleSfx()
    {
        if (!playLightToggleSfx)
            return;
        if (lightToggleSfxSource == null)
            return;

        AudioClip clip = _lightsOn ? lightToggleOnClip : lightToggleOffClip;
        if (clip == null)
            return;
        lightToggleSfxSource.PlayOneShot(clip, lightToggleSfxVolume);
    }

    /// <summary>他スクリプトから参照用（将来ホイールと連動する場合など）</summary>
    public bool AreLightsOn => _lightsOn;
}
