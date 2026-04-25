using UnityEngine;

/// <summary>
/// パトカーと自転車の距離に応じて、指定 AudioSource のループ音量を変化させます。
/// </summary>
[DisallowMultipleComponent]
public class DistanceBasedLoopVolume : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("距離計算に使うパトカー側の Transform")]
    [SerializeField] Transform policeCar;
    [Tooltip("距離計算に使う自転車（プレイヤー）側の Transform")]
    [SerializeField] Transform bicycle;

    [Header("Loop audio")]
    [Tooltip("音量を制御する AudioSource（Loop ON 推奨）")]
    [SerializeField] AudioSource loopAudioSource;
    [Tooltip("開始時に loopAudioSource.playOnAwake を上書きせず再生開始する")]
    [SerializeField] bool playOnStartIfNotPlaying = true;

    [Header("Distance -> Volume")]
    [Tooltip("この距離以内で最大音量になります")]
    [SerializeField] float minDistance = 6f;
    [Tooltip("この距離以上で最小音量になります")]
    [SerializeField] float maxDistance = 80f;
    [Range(0f, 1f)]
    [SerializeField] float volumeAtMinDistance = 1f;
    [Range(0f, 1f)]
    [SerializeField] float volumeAtMaxDistance = 0f;
    [Tooltip("音量変化のカーブ（0=近い, 1=遠い）")]
    [SerializeField] AnimationCurve volumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("音量の追従速度（大きいほどすぐ追従）")]
    [SerializeField] float volumeLerpSpeed = 8f;
    [Tooltip("XZ平面距離だけで判定（Y差を無視）")]
    [SerializeField] bool horizontalDistanceOnly = true;

    void Awake()
    {
        if (loopAudioSource == null)
            loopAudioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (loopAudioSource != null && playOnStartIfNotPlaying && !loopAudioSource.isPlaying)
            loopAudioSource.Play();
    }

    void Update()
    {
        if (loopAudioSource == null || policeCar == null || bicycle == null)
            return;

        float d = Distance(policeCar.position, bicycle.position, horizontalDistanceOnly);
        float t = InverseLerpClamped(minDistance, maxDistance, d); // 0=近い,1=遠い
        float shaped = Mathf.Clamp01(volumeCurve.Evaluate(t));
        float targetVolume = Mathf.Lerp(volumeAtMinDistance, volumeAtMaxDistance, 1f - shaped);
        loopAudioSource.volume = Mathf.Lerp(loopAudioSource.volume, targetVolume, Mathf.Max(0f, volumeLerpSpeed) * Time.deltaTime);
    }

    static float Distance(Vector3 a, Vector3 b, bool horizontalOnly)
    {
        if (horizontalOnly)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
        return Vector3.Distance(a, b);
    }

    static float InverseLerpClamped(float min, float max, float value)
    {
        if (max <= min)
            return value <= min ? 0f : 1f;
        return Mathf.Clamp01((value - min) / (max - min));
    }
}
