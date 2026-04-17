using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 指定したポイント（キューブ）にぶつかると回転して周回するスクリプト。
/// Inspector上でポイント数・回転方向・速度などを自由に設定できます。
/// 
/// 【使い方】
/// 1. このスクリプトを人形（動かしたいオブジェクト）にアタッチ
/// 2. 人形とポイントのキューブ両方に Collider と Rigidbody が必要
///    - 人形: Rigidbody（Use Gravity = お好み）+ Collider
///    - キューブ: Collider（Is Trigger = ON）
/// 3. Inspector の「Waypoints」リストにポイントを追加
///    - Point: キューブをドラッグ＆ドロップ
///    - Turn Direction: 回転方向（Left = 左90°、Right = 右90°、TurnAround = 180°）
/// 4. Move Speed で前進速度を調整
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PatrolMover : MonoBehaviour
{
    // ============================================================
    // 回転方向の選択肢
    // ============================================================
    public enum TurnDirection
    {
        Left,       // 左に90度
        Right,      // 右に90度
        TurnAround  // 180度回転（Uターン）
    }

    // ============================================================
    // 各ポイントの設定（Inspectorに表示される）
    // ============================================================
    [System.Serializable]
    public class Waypoint
    {
        [Tooltip("ぶつかるポイントのキューブ（GameObjectをドラッグ＆ドロップ）")]
        public GameObject point;

        [Tooltip("このポイントにぶつかった時の回転方向")]
        public TurnDirection turnDirection = TurnDirection.Left;
    }

    // ============================================================
    // Inspector で設定するパラメータ
    // ============================================================
    [Header("== 移動設定 ==")]
    [Tooltip("前進する速度")]
    public float moveSpeed = 3f;

    [Tooltip("回転する速さ（度/秒）。大きいほど素早く回転")]
    public float rotationSpeed = 360f;

    [Header("== ポイント設定 ==")]
    [Tooltip("周回するポイントのリスト。順番通りに巡回します。\n＋ボタンでポイントを追加できます。")]
    public List<Waypoint> waypoints = new List<Waypoint>();

    [Header("== 周回設定 ==")]
    [Tooltip("何周するか（0 = 無限に周回）")]
    public int maxLaps = 0;

    [Header("== デバッグ表示 ==")]
    [Tooltip("ONにすると現在の状態をコンソールに表示")]
    public bool showDebugLog = false;

    // ============================================================
    // 内部変数
    // ============================================================
    private int currentWaypointIndex = 0;  // 次にぶつかるべきポイントの番号
    private int completedLaps = 0;         // 完了した周回数
    private bool isRotating = false;       // 回転中かどうか
    private float targetYAngle;            // 目標の回転角度
    private bool isActive = true;          // 動作中かどうか
    private Rigidbody rb;

    // ============================================================
    // Unity ライフサイクル
    // ============================================================
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Rigidbody の設定（回転を物理で制御しない）
        rb.freezeRotation = true;

        if (waypoints.Count == 0)
        {
            Debug.LogWarning("[PatrolMover] ポイントが設定されていません！Inspectorでポイントを追加してください。");
            isActive = false;
        }

        // ポイントのキューブに Collider がない場合は警告
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i].point == null)
            {
                Debug.LogWarning($"[PatrolMover] ポイント {i + 1} が未設定です！");
                continue;
            }
            
            Collider col = waypoints[i].point.GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogWarning($"[PatrolMover] '{waypoints[i].point.name}' に Collider がありません！");
            }
            else if (!col.isTrigger)
            {
                Debug.LogWarning($"[PatrolMover] '{waypoints[i].point.name}' の Collider は Is Trigger = ON にしてください。");
            }
        }
    }

    void Update()
    {
        if (!isActive) return;

        if (isRotating)
        {
            // スムーズに回転
            float currentY = transform.eulerAngles.y;
            float newY = Mathf.MoveTowardsAngle(currentY, targetYAngle, rotationSpeed * Time.deltaTime);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, newY, transform.eulerAngles.z);

            // 回転完了チェック
            if (Mathf.Abs(Mathf.DeltaAngle(newY, targetYAngle)) < 0.5f)
            {
                // ぴったりの角度にスナップ
                transform.eulerAngles = new Vector3(transform.eulerAngles.x, targetYAngle, transform.eulerAngles.z);
                isRotating = false;

                if (showDebugLog)
                    Debug.Log($"[PatrolMover] 回転完了。次のポイント: {GetCurrentPointName()}");
            }
        }
        else
        {
            // 前進
            transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
        }
    }

    // ============================================================
    // トリガー衝突検知（Is Trigger = ON のキューブ用）
    // ============================================================
    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (isRotating) return; // 回転中は無視
        if (waypoints.Count == 0) return;

        // 現在の目標ポイントと一致するかチェック
        Waypoint currentWaypoint = waypoints[currentWaypointIndex];
        if (currentWaypoint.point == null) return;

        if (other.gameObject == currentWaypoint.point)
        {
            // 回転方向を計算
            float turnAngle = GetTurnAngle(currentWaypoint.turnDirection);
            targetYAngle = transform.eulerAngles.y + turnAngle;

            // 角度を0-360に正規化
            targetYAngle = ((targetYAngle % 360f) + 360f) % 360f;

            isRotating = true;

            if (showDebugLog)
                Debug.Log($"[PatrolMover] '{currentWaypoint.point.name}' に到達！ → {currentWaypoint.turnDirection} に {Mathf.Abs(turnAngle)}° 回転");

            // 次のポイントへ
            currentWaypointIndex++;

            // リストの最後まで来たら最初に戻る（1周完了）
            if (currentWaypointIndex >= waypoints.Count)
            {
                currentWaypointIndex = 0;
                completedLaps++;

                if (showDebugLog)
                    Debug.Log($"[PatrolMover] {completedLaps} 周完了！");

                // 周回数制限チェック
                if (maxLaps > 0 && completedLaps >= maxLaps)
                {
                    if (showDebugLog)
                        Debug.Log($"[PatrolMover] {maxLaps} 周完了。停止します。");
                    isActive = false;
                }
            }
        }
    }

    // ============================================================
    // 通常の衝突検知（Is Trigger = OFF のキューブ用）
    // ============================================================
    void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;
        if (isRotating) return;
        if (waypoints.Count == 0) return;

        Waypoint currentWaypoint = waypoints[currentWaypointIndex];
        if (currentWaypoint.point == null) return;

        if (collision.gameObject == currentWaypoint.point)
        {
            float turnAngle = GetTurnAngle(currentWaypoint.turnDirection);
            targetYAngle = transform.eulerAngles.y + turnAngle;
            targetYAngle = ((targetYAngle % 360f) + 360f) % 360f;

            isRotating = true;

            if (showDebugLog)
                Debug.Log($"[PatrolMover] '{currentWaypoint.point.name}' に衝突！ → {currentWaypoint.turnDirection} に {Mathf.Abs(turnAngle)}° 回転");

            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                currentWaypointIndex = 0;
                completedLaps++;

                if (maxLaps > 0 && completedLaps >= maxLaps)
                {
                    isActive = false;
                }
            }
        }
    }

    // ============================================================
    // ユーティリティ
    // ============================================================
    float GetTurnAngle(TurnDirection direction)
    {
        switch (direction)
        {
            case TurnDirection.Left:      return -90f;
            case TurnDirection.Right:     return 90f;
            case TurnDirection.TurnAround: return 180f;
            default: return -90f;
        }
    }

    string GetCurrentPointName()
    {
        if (waypoints.Count == 0 || waypoints[currentWaypointIndex].point == null)
            return "（未設定）";
        return waypoints[currentWaypointIndex].point.name;
    }

    // ============================================================
    // エディタ上でのギズモ表示（経路を可視化）
    // ============================================================
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i].point == null) continue;

            // ポイントの位置に色付き球を表示
            switch (waypoints[i].turnDirection)
            {
                case TurnDirection.Left:
                    Gizmos.color = Color.cyan;
                    break;
                case TurnDirection.Right:
                    Gizmos.color = Color.yellow;
                    break;
                case TurnDirection.TurnAround:
                    Gizmos.color = Color.red;
                    break;
            }

            Gizmos.DrawWireSphere(waypoints[i].point.transform.position, 0.5f);

            // ポイント間の線を描画
            int nextIndex = (i + 1) % waypoints.Count;
            if (waypoints[nextIndex].point != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    waypoints[i].point.transform.position,
                    waypoints[nextIndex].point.transform.position
                );
            }
        }
    }
}
