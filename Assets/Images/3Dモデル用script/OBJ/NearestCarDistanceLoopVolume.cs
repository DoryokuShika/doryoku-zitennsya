using UnityEngine;

/// <summary>
/// 複数の車のうち「自転車に最も近い1台」の距離で、ループSE音量を制御します。
/// </summary>
[DisallowMultipleComponent]
public class NearestCarDistanceLoopVolume : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("距離判定の基準になる自転車（プレイヤー）")]
    [SerializeField] Transform bicycle;
    [Tooltip("監視する車（5台など）をここに登録")]
    [SerializeField] Transform[] cars;

    [Header("Loop audio")]
    [Tooltip("音量を制御する AudioSource（Loop ON 推奨）")]
    [SerializeField] AudioSource loopAudioSource;
    [Tooltip("開始時に再生されていなければ自動再生する")]
    [SerializeField] bool playOnStartIfNotPlaying = true;

    [Header("Distance -> Volume")]
    [Tooltip("最短距離がこの値以下なら最大音量")]
    [SerializeField] float minDistance = 6f;
    [Tooltip("最短距離がこの値以上なら最小音量")]
    [SerializeField] float maxDistance = 80f;
    [Range(0f, 1f)]
    [SerializeField] float volumeAtMinDistance = 1f;
    [Range(0f, 1f)]
    [SerializeField] float volumeAtMaxDistance = 0f;
    [Tooltip("0=近い、1=遠いのカーブ")]
    [SerializeField] AnimationCurve volumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("音量追従速度")]
    [SerializeField] float volumeLerpSpeed = 8f;
    [Tooltip("XZ平面距離で判定（Y差を無視）")]
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
        if (loopAudioSource == null || bicycle == null || cars == null || cars.Length == 0)
            return;

        if (!TryGetNearestDistance(out float nearest))
            return;

        float t = InverseLerpClamped(minDistance, maxDistance, nearest); // 0=近い, 1=遠い
        float shaped = Mathf.Clamp01(volumeCurve.Evaluate(t));
        float targetVolume = Mathf.Lerp(volumeAtMinDistance, volumeAtMaxDistance, 1f - shaped);
        loopAudioSource.volume = Mathf.Lerp(
            loopAudioSource.volume,
            targetVolume,
            Mathf.Max(0f, volumeLerpSpeed) * Time.deltaTime);
    }

    bool TryGetNearestDistance(out float nearestDistance)
    {
        nearestDistance = float.PositiveInfinity;
        bool found = false;

        Vector3 b = bicycle.position;
        for (int i = 0; i < cars.Length; i++)
        {
            Transform c = cars[i];
            if (c == null)
                continue;

            float d = Distance(b, c.position, horizontalDistanceOnly);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                found = true;
            }
        }

        return found;
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
