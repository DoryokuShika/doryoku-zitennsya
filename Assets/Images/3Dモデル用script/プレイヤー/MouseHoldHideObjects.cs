using UnityEngine;

/// <summary>
/// マウス左ボタンを押している間だけ <see cref="hiddenWhileLeftHeld"/> を非表示にします（左手信号の見た目用）。
/// 右クリックによる操作は行いません。
/// </summary>
public class MouseHoldHideObjects : MonoBehaviour
{
    [SerializeField]
    [Tooltip("左クリックを押している間非表示にするオブジェクト（信号モデルなど）")]
    GameObject hiddenWhileLeftHeld;

    void Update()
    {
        if (hiddenWhileLeftHeld != null)
            hiddenWhileLeftHeld.SetActive(!Input.GetMouseButton(0));
    }
}
