using UnityEngine;

/// <summary>
/// 左手信号（マウス左ボタン押下）の状態を毎フレーム更新し、他スクリプトから参照できるようにします。
/// プレイヤーなどに付け、1 シーンに 1 つを想定します。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHandSignalState : MonoBehaviour
{
    public static PlayerHandSignalState Instance { get; private set; }

    /// <summary>左クリックを押し続けている間 true（左手信号を出している扱い）。</summary>
    public bool IsLeftHandSignalHeld { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerHandSignalState)}: 複数あります。{Instance.name} を残します。", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        IsLeftHandSignalHeld = Input.GetMouseButton(0);
    }
}
