using UnityEngine;

public class ResetOutMap : MonoBehaviour
{
    [Header("マップ外処理")]
    [SerializeField] private float MinZ = -10f;
    [SerializeField] private float MaxZ = 10f;
    [SerializeField] private float MinX = -10f;
    [SerializeField] private float MaxX = 10f;
    [SerializeField] private float MinY = -10f;
    [SerializeField] private float MaxY = 10f;
    [SerializeField] private float resetHeight;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Vector3 startposition;
    private Rigidbody rb;

    void Start()
    {
        startposition = transform.position;
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (IsOutOfMap())
        {
            Debug.Log("Out of Map! Resetting position.");
            ResetPosition();
        }
    }

    bool IsOutOfMap()
    {
        Vector3 pos = transform.position;
        return pos.x < MinX || pos.x > MaxX ||
                pos.y < MinY || pos.y > MaxY ||
                pos.z < MinZ || pos.z > MaxZ;
    }

    void ResetPosition()
    {
        transform.position = startposition;
        //GravityInversion.isActive = false; // 重力反転をリセット
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // エディタのシーンビューで箱を描画する
    void OnDrawGizmosSelected()
    {
        // すべて大文字（MaxYなど）に統一して計算
        float centerX = (MinX + MaxX) / 2f;
        float centerY = (MinY + MaxY) / 2f;
        float centerZ = (MinZ + MaxZ) / 2f;
        Vector3 center = new Vector3(centerX, centerY, centerZ);

        float sizeX = MaxX - MinX;
        float sizeY = MaxY - MinY;
        float sizeZ = MaxZ - MinZ;
        Vector3 size = new Vector3(sizeX, sizeY, sizeZ);

        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}