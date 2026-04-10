using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OperationBicycle : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 5f;               // 前後左右の移動速度
    [Header("回転設定")]
    public float mouseSensitivity = 2f;        // マウス感度（回転）
    public bool holdRightMouseToRotate = true; // 右クリックを押している間だけマウスで回転する

    Rigidbody rb;
    float yaw;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        yaw = transform.eulerAngles.y;
    }

    void Update()
    {
        HandleRotation();
        HandleMovement();
    }

    void HandleRotation()
    {
        // 回転のオン/オフ制御
        bool rotate = !holdRightMouseToRotate || Input.GetMouseButton(1);

        if (rotate)
        {
            if (Input.GetMouseButtonDown(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            float mx = Input.GetAxis("Mouse X");
            // マウス入力を角度に変換
            yaw += mx * mouseSensitivity * 10f;

            Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);
            if (rb != null)
                rb.MoveRotation(targetRot);
            else
                transform.rotation = targetRot;
        }
        else
        {
            if (Input.GetMouseButtonUp(1))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal"); // A/D, ←/→
        float v = Input.GetAxis("Vertical");   // W/S, ↑/↓

        Vector3 dir = (transform.forward * v + transform.right * h);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 displacement = dir * moveSpeed * Time.deltaTime;

        if (rb != null)
            rb.MovePosition(rb.position + displacement);
        else
            transform.position += displacement;
    }
}
