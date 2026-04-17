using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    public Transform Camera;
    public float PlayerSpeed;
    public float RotationSpeed;
    Vector3 startPosition;

    void Start()
    {
       startPosition = transform.position;
    }

    void Update()
    {
        move();
        RotatePlayer();
        CameraFollow();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            transform.position = startPosition; // プレイヤーを初期位置に戻す
        }
    }

    void move()
    {
        var speed = Vector3.zero;

        if (Input.GetKey(KeyCode.S))
        {
            speed.z -= PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.W))
        {
            speed.z += PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.D))
        {
            speed.x += PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.A))
        {
            speed.x -= PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            speed.y += PlayerSpeed;
        }
        transform.Translate(speed * Time.deltaTime);
    }

    void RotatePlayer()
    {
        float rotationY = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            rotationY -= RotationSpeed;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            rotationY += RotationSpeed;
        }

        transform.Rotate(0f, rotationY * Time.deltaTime, 0f);  // プレイヤー自体を回転
    }

    void CameraFollow()
    {
        Camera.position = transform.position;
        Camera.rotation = transform.rotation;
    }

    
}
