using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainPlayer : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 5f;               // 移動速度
    [Header("回転設定")]
    public float mouseSensitivity = 2f;        // マウス感度

    Rigidbody rb;
    float yaw;
    Camera mainCamera;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        yaw = transform.eulerAngles.y;
        mainCamera = Camera.main;
    }

    void Update()
    {
        HandleRotation();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleRotation()
    {
        float mx = Input.GetAxis("Mouse X");
        // マウス入力を角度に変換
        yaw += mx * mouseSensitivity * 10f;

        Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);
        if (rb != null)
            rb.MoveRotation(targetRot);
        else
            transform.rotation = targetRot;
    }

    void HandleMovement()
    {
        // 常に前方に移動
        Vector3 moveDir = transform.forward;
        Vector3 displacement = moveDir * moveSpeed * Time.fixedDeltaTime;

        if (rb != null)
            rb.MovePosition(rb.position + displacement);
        else
            transform.position += displacement;
    }
}
