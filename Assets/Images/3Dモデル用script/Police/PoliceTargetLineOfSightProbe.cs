using UnityEngine;

/// <summary>
/// 警察オブジェクトに付け、対象が視野角・距離・遮蔽を通過するかだけを計算し
/// <see cref="PoliceLineOfSightState"/> に報告します。捕獲 UI や違反種別は扱いません。
/// </summary>
[DefaultExecutionOrder(18)]
[DisallowMultipleComponent]
public class PoliceTargetLineOfSightProbe : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("視界判定の対象（自転車＝プレイヤーなど）。未設定のとき PoliceLineOfSightCatch の target と同期できます。")]
    [SerializeField] Transform targetCharacter;

    [Header("視点（未指定ならこのオブジェクトの位置＋オフセット）")]
    [SerializeField] Transform eyeTransform;
    [SerializeField] Vector3 eyeLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("視野")]
    [Tooltip("左右合わせた水平視野角（度）。大きいほど横に広い。例: 110")]
    [SerializeField] float horizontalViewAngleDegrees = 110f;
    [Tooltip("奥行きの最大距離（m）。大きいほど遠くまで見える。")]
    [SerializeField] float maxViewDistance = 42f;
    [Tooltip("遮蔽として扱うレイヤー（壁など）")]
    [SerializeField] LayerMask obstacleLayers = ~0;

    public Transform TargetCharacter => targetCharacter;

    /// <summary>PoliceLineOfSightCatch から target を流し込む用。</summary>
    public void SetTargetCharacterIfUnset(Transform t)
    {
        if (targetCharacter == null && t != null)
            targetCharacter = t;
    }

    void LateUpdate()
    {
        PoliceLineOfSightState.ProbeReportsInSight(IsTargetInSight());
    }

    /// <summary>このプローブ単体の視界判定（静的状態には触れません）。</summary>
    public bool IsTargetInSight()
    {
        if (targetCharacter == null)
            return false;

        Vector3 origin = GetEyeWorldPosition();
        Vector3 targetPoint = GetTargetSamplePoint(targetCharacter);
        Vector3 toTarget = targetPoint - origin;
        float dist = toTarget.magnitude;
        if (dist < 0.01f || dist > maxViewDistance)
            return false;

        Vector3 forward = transform.forward;
        float halfAngle = Mathf.Clamp(horizontalViewAngleDegrees * 0.5f, 0.1f, 179f);
        if (Vector3.Angle(forward, toTarget) > halfAngle)
            return false;

        Vector3 dir = toTarget / dist;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleLayers, QueryTriggerInteraction.Ignore))
            return IsTransformPartOfTarget(hit.transform, targetCharacter);

        return true;
    }

    static bool IsTransformPartOfTarget(Transform t, Transform targetRoot)
    {
        if (t == null || targetRoot == null)
            return false;
        return t == targetRoot || t.IsChildOf(targetRoot);
    }

    Vector3 GetEyeWorldPosition()
    {
        if (eyeTransform != null)
            return eyeTransform.position;
        return transform.TransformPoint(eyeLocalOffset);
    }

    static Vector3 GetTargetSamplePoint(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        var col = target.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return target.position;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = eyeTransform != null ? eyeTransform.position : transform.TransformPoint(eyeLocalOffset);
        float half = Mathf.Clamp(horizontalViewAngleDegrees * 0.5f, 1f, 89f);
        Vector3 f = transform.forward * maxViewDistance;
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        Gizmos.DrawRay(origin, f);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-half, transform.up) * f);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(half, transform.up) * f);
    }
#endif
}
