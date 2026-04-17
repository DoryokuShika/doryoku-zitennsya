using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 奥に行くほど視野が広がるコーンキャスト（錐体型当たり判定）
/// 使い方：このコンポーネントをキャラクターにアタッチして使用する
/// </summary>
public class ConeCast : MonoBehaviour
{
    [Header("コーン設定")]
    [Tooltip("コーンの最大距離")]
    public float maxDistance = 10f;

    [Tooltip("コーンの半開き角度（度）。大きいほど広がる")]
    [Range(1f, 90f)]
    public float halfAngle = 30f;

    [Tooltip("検出対象のレイヤー")]
    public LayerMask detectionLayer = Physics.DefaultRaycastLayers;

    [Header("監視対象オブジェクト")]
    [Tooltip("視野に入ったか監視するオブジェクト（Inspector で設定）")]
    public GameObject targetObject;

    [Header("デバッグ表示")]
    [Tooltip("Sceneビューでコーンを表示する")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0f, 1f, 0.5f, 0.3f);

    // 前フレームの検出状態（Enter/Exit 判定用）
    bool _wasDetected = false;

    // ─────────────────────────────────────────
    // MonoBehaviour（毎フレーム検出）
    // ─────────────────────────────────────────
    void Update()
    {
        if (targetObject == null) return;

        bool isDetected = IsTargetInCone(targetObject);

        if (isDetected && !_wasDetected)
        {
            // 視野に入った瞬間
            Debug.Log($"[ConeCast] 視野に入った: {targetObject.name}");
        }
        else if (!isDetected && _wasDetected)
        {
            // 視野から出た瞬間
            Debug.Log($"[ConeCast] 視野から出た: {targetObject.name}");
        }

        _wasDetected = isDetected;
    }

    /// <summary>
    /// 指定オブジェクトが現在コーン内にいるか確認する
    /// </summary>
    bool IsTargetInCone(GameObject target)
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        Vector3 toTarget = target.transform.position - origin;
        float depth = Vector3.Dot(toTarget, direction);

        if (depth <= 0f || depth > maxDistance)
            return false;

        Vector3 axisComponent = direction * depth;
        float perpDist = (toTarget - axisComponent).magnitude;
        float allowedRadius = depth * Mathf.Tan(halfAngle * Mathf.Deg2Rad);

        return perpDist <= allowedRadius;
    }

    // ─────────────────────────────────────────
    // 静的ユーティリティメソッド
    // ─────────────────────────────────────────

    /// <summary>
    /// コーン内の全オブジェクトを返す
    /// </summary>
    /// <param name="origin">起点</param>
    /// <param name="direction">向き</param>
    /// <param name="halfAngle">半開き角度（度）</param>
    /// <param name="maxDistance">最大距離</param>
    /// <param name="layerMask">対象レイヤー</param>
    public static RaycastHit[] CastAll(
        Vector3 origin,
        Vector3 direction,
        float halfAngle,
        float maxDistance,
        int layerMask = Physics.DefaultRaycastLayers)
    {
        direction = direction.normalized;

        // SphereCastAll でコーン外周を包む球を使い候補を一括取得
        float maxRadius = maxDistance * Mathf.Tan(halfAngle * Mathf.Deg2Rad);
        RaycastHit[] candidates = Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, layerMask);

        var results = new List<RaycastHit>();

        foreach (var hit in candidates)
        {
            if (IsInsideCone(origin, direction, halfAngle, maxDistance, hit))
                results.Add(hit);
        }

        return results.ToArray();
    }

    /// <summary>
    /// コーン内の最初（最も近い）オブジェクトを返す。なければ false
    /// </summary>
    public static bool Cast(
        Vector3 origin,
        Vector3 direction,
        float halfAngle,
        float maxDistance,
        out RaycastHit closestHit,
        int layerMask = Physics.DefaultRaycastLayers)
    {
        RaycastHit[] hits = CastAll(origin, direction, halfAngle, maxDistance, layerMask);

        closestHit = default;
        float minDist = float.MaxValue;
        bool found = false;

        foreach (var hit in hits)
        {
            if (hit.distance < minDist)
            {
                minDist = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// コーン内に何かあるか確認するだけ（最速）
    /// </summary>
    public static bool Check(
        Vector3 origin,
        Vector3 direction,
        float halfAngle,
        float maxDistance,
        int layerMask = Physics.DefaultRaycastLayers)
    {
        return CastAll(origin, direction, halfAngle, maxDistance, layerMask).Length > 0;
    }

    // ─────────────────────────────────────────
    // 内部：コーン内判定
    // ─────────────────────────────────────────

    static bool IsInsideCone(
        Vector3 origin,
        Vector3 direction,
        float halfAngle,
        float maxDistance,
        RaycastHit hit)
    {
        // ヒット点またはコライダー中心へのベクトル
        Vector3 toTarget = (hit.point != Vector3.zero)
            ? hit.point - origin
            : hit.transform.position - origin;

        // 方向軸への投影距離（奥行き）
        float depth = Vector3.Dot(toTarget, direction);

        // 後方・距離超過は除外
        if (depth <= 0f || depth > maxDistance)
            return false;

        // 軸からの垂直距離
        Vector3 axisComponent = direction * depth;
        float perpDist = (toTarget - axisComponent).magnitude;

        // この深さでのコーン半径 = depth × tan(halfAngle)
        float allowedRadius = depth * Mathf.Tan(halfAngle * Mathf.Deg2Rad);

        return perpDist <= allowedRadius;
    }

    // ─────────────────────────────────────────
    // Gizmos（Sceneビューのデバッグ表示）
    // ─────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        DrawConeGizmo(transform.position, transform.forward, halfAngle, maxDistance, gizmoColor);
    }

    public static void DrawConeGizmo(
        Vector3 origin,
        Vector3 direction,
        float halfAngle,
        float distance,
        Color color)
    {
        direction = direction.normalized;
        float endRadius = distance * Mathf.Tan(halfAngle * Mathf.Deg2Rad);

        Vector3 tip = origin;
        Vector3 baseCenter = origin + direction * distance;

        // 垂直ベクトルを取得
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.99f
            ? Vector3.up
            : Vector3.right;

        Vector3 perpA = Vector3.Cross(direction, up).normalized;
        Vector3 perpB = Vector3.Cross(direction, perpA).normalized;

        Gizmos.color = color;

        // 先端 → 円周への4本ライン
        int lineCount = 16;
        for (int i = 0; i < lineCount; i++)
        {
            float rad = (2f * Mathf.PI / lineCount) * i;
            Vector3 edgePoint = baseCenter
                + perpA * Mathf.Cos(rad) * endRadius
                + perpB * Mathf.Sin(rad) * endRadius;
            Gizmos.DrawLine(tip, edgePoint);
        }

        // 底面の円
        DrawCircleGizmo(baseCenter, direction, endRadius, perpA, perpB);

        // 輪郭の4ライン（上下左右）
        Gizmos.color = new Color(color.r, color.g, color.b, 1f);
        Gizmos.DrawLine(tip, baseCenter + perpA * endRadius);
        Gizmos.DrawLine(tip, baseCenter - perpA * endRadius);
        Gizmos.DrawLine(tip, baseCenter + perpB * endRadius);
        Gizmos.DrawLine(tip, baseCenter - perpB * endRadius);
    }

    static void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius,
        Vector3 perpA, Vector3 perpB, int segments = 32)
    {
        Vector3 prev = center + perpA * radius;
        for (int i = 1; i <= segments; i++)
        {
            float rad = (2f * Mathf.PI / segments) * i;
            Vector3 next = center
                + perpA * Mathf.Cos(rad) * radius
                + perpB * Mathf.Sin(rad) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
