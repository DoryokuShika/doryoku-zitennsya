using UnityEngine;

/// <summary>
/// Hold LMB: hides <see cref="hiddenWhileLeftHeld"/>.
/// Hold RMB: hides <see cref="hiddenWhileRightHeld"/>.
/// Release: each object becomes visible again (if the other button does not keep it hidden).
/// </summary>
public class MouseHoldHideObjects : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Hidden while the left mouse button is held.")]
    GameObject hiddenWhileLeftHeld;

    [SerializeField]
    [Tooltip("Hidden while the right mouse button is held.")]
    GameObject hiddenWhileRightHeld;

    void Update()
    {
        if (hiddenWhileLeftHeld != null)
            hiddenWhileLeftHeld.SetActive(!Input.GetMouseButton(0));

        if (hiddenWhileRightHeld != null)
            hiddenWhileRightHeld.SetActive(!Input.GetMouseButton(1));
    }
}
