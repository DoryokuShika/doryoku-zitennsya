using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シーンに1つ置き、ベル退避（<see cref="PatrolWaypointsBranchRandom"/>）が成立するたびに
/// 開始時は「残りN人」、ベル退避が成立するたびに N を 1 ずつ減らして表示し、0 になったら「完了」です。
/// </summary>
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public class PedestrianBellObjectiveUi : MonoBehaviour
{
    public static PedestrianBellObjectiveUi Instance { get; private set; }

    [Header("Objective")]
    [Tooltip("ベルで退避させるべき歩行者の人数（初期の「残り」表示の N）")]
    [SerializeField] [Min(1)] int pedestriansToBellDismissTotal = 6;

    [Tooltip("String.Format: {0} = 1人退避したあとの残り人数（6人開始なら 5,4,...,1 と減る）")]
    [SerializeField] string remainingCountFormat = "\u6b8b\u308a{0}\u4eba";

    [Header("UI — progress (remaining)")]
    [SerializeField] TMP_Text bellProgressTmp;
    [SerializeField] Text bellProgressUi;
    [SerializeField] Color bellProgressCountingColor = Color.white;

    [SerializeField] TMP_Text[] additionalTmpWhenCounting;
    [SerializeField] Text[] additionalUiWhenCounting;
    [SerializeField] Color additionalCountingColor = Color.white;

    [Header("UI — complete")]
    [SerializeField] TMP_Text bellCompleteTmp;
    [SerializeField] Text bellCompleteUi;
    [SerializeField] string bellCompleteLabel = "\u5b8c\u4e86";
    [SerializeField] Color bellCompleteTextColor = Color.red;

    [SerializeField] TMP_Text[] additionalTmpOnComplete;
    [SerializeField] Text[] additionalUiOnComplete;
    [SerializeField] Color additionalCompleteColor = Color.red;

    int _remaining = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PedestrianBellObjectiveUi)}: Multiple instances. Keeping {Instance.name}, destroying {name}.", this);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        _remaining = Mathf.Max(1, pedestriansToBellDismissTotal);
        ApplyProgressUi(string.Format(remainingCountFormat, _remaining));
    }

    /// <summary>
    /// ベル退避が1件成立したときに <see cref="PatrolWaypointsBranchRandom"/> から呼びます。
    /// </summary>
    public static void NotifyBellDismissed()
    {
        if (Instance == null)
            return;
        Instance.ApplyBellDismissedStep();
    }

    /// <summary>残りを満タンに戻し、UI を「残りN人」にします（リトライ用など）。クリア条件のベル目標も未完了に戻します。</summary>
    public void ResetObjectiveToFull()
    {
        ViolationTimes.ResetPedestrianBellObjectiveComplete();
        _remaining = Mathf.Max(1, pedestriansToBellDismissTotal);
        ApplyProgressUi(string.Format(remainingCountFormat, _remaining));
    }

    void ApplyBellDismissedStep()
    {
        if (_remaining == 0)
            return;

        if (_remaining < 0)
            _remaining = Mathf.Max(1, pedestriansToBellDismissTotal);

        // 歩行者が1人消えた分だけ残りを 1 減らし、その数を表示する
        _remaining--;
        if (_remaining > 0)
            ApplyProgressUi(string.Format(remainingCountFormat, _remaining));
        else
        {
            ApplyCompleteUi();
            ViolationTimes.NotifyPedestrianBellObjectiveComplete();
        }
    }

    void ApplyProgressUi(string text)
    {
        if (bellProgressTmp != null)
        {
            bellProgressTmp.text = text;
            bellProgressTmp.color = bellProgressCountingColor;
        }

        if (bellProgressUi != null)
        {
            bellProgressUi.text = text;
            bellProgressUi.color = bellProgressCountingColor;
        }

        if (additionalTmpWhenCounting != null)
        {
            foreach (var t in additionalTmpWhenCounting)
            {
                if (t != null)
                {
                    t.text = text;
                    t.color = additionalCountingColor;
                }
            }
        }

        if (additionalUiWhenCounting != null)
        {
            foreach (var t in additionalUiWhenCounting)
            {
                if (t != null)
                {
                    t.text = text;
                    t.color = additionalCountingColor;
                }
            }
        }
    }

    void ApplyCompleteUi()
    {
        if (bellCompleteTmp != null)
        {
            bellCompleteTmp.text = bellCompleteLabel;
            bellCompleteTmp.color = bellCompleteTextColor;
        }

        if (bellCompleteUi != null)
        {
            bellCompleteUi.text = bellCompleteLabel;
            bellCompleteUi.color = bellCompleteTextColor;
        }

        if (additionalTmpOnComplete != null)
        {
            foreach (var t in additionalTmpOnComplete)
            {
                if (t != null)
                {
                    t.text = bellCompleteLabel;
                    t.color = additionalCompleteColor;
                }
            }
        }

        if (additionalUiOnComplete != null)
        {
            foreach (var t in additionalUiOnComplete)
            {
                if (t != null)
                {
                    t.text = bellCompleteLabel;
                    t.color = additionalCompleteColor;
                }
            }
        }
    }

    void OnValidate()
    {
        pedestriansToBellDismissTotal = Mathf.Max(1, pedestriansToBellDismissTotal);
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticInstance()
    {
        Instance = null;
    }
#endif
}
