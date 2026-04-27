using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Eキーを一定時間長押ししたら Start シーンへ戻す。
/// </summary>
public class HoldEToReturnStart : MonoBehaviour
{
    [Header("遷移先")]
    [SerializeField] string startSceneName = "Start";

    [Header("入力")]
    [SerializeField] KeyCode holdKey = KeyCode.E;
    [SerializeField] float holdSeconds = 1.5f;
    [Tooltip("オン: Time.timeScale の影響を受けずに長押し判定する")]
    [SerializeField] bool useUnscaledTime = true;

    [Header("遷移前の状態調整")]
    [SerializeField] bool resetTimeScaleBeforeLoad = true;
    [SerializeField] float timeScaleBeforeLoad = 1f;
    [SerializeField] bool unlockCursorBeforeLoad = true;

    float _holdTimer;
    bool _loading;

    void Update()
    {
        if (_loading)
            return;

        if (Input.GetKey(holdKey))
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _holdTimer += dt;

            if (_holdTimer >= Mathf.Max(0.05f, holdSeconds))
                LoadStartScene();
            return;
        }

        _holdTimer = 0f;
    }

    void LoadStartScene()
    {
        if (_loading)
            return;
        _loading = true;

        if (resetTimeScaleBeforeLoad)
            Time.timeScale = timeScaleBeforeLoad;

        if (unlockCursorBeforeLoad)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogWarning("[HoldEToReturnStart] 遷移先シーン名が空です。", this);
            _loading = false;
            return;
        }

        string scene = startSceneName.Trim();
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[HoldEToReturnStart] シーン '{scene}' が Build Settings にありません。", this);
            _loading = false;
            return;
        }

        SceneManager.LoadScene(scene);
    }
}
