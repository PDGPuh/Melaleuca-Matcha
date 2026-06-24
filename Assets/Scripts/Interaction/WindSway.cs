using UnityEngine;

namespace RungTramTraSu
{
    public class WindSway : MonoBehaviour
    {
        [Header("Wind Sway Settings")]
        [SerializeField] private float swaySpeed = 0.8f;      // Tốc độ đung đưa
        [SerializeField] private float swayAngleX = 1.0f;     // Góc đung đưa tối đa trục X (độ)
        [SerializeField] private float swayAngleZ = 1.2f;     // Góc đung đưa tối đa trục Z (độ)

        private float randomOffset;
        private Quaternion initialRotation;

        private void Start()
        {
            // Lưu góc xoay ban đầu của cây
            initialRotation = transform.rotation;
            // Tạo lệch pha ngẫu nhiên để các cây trong rừng không chuyển động đều tăm tắp cùng nhau
            randomOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float timeVal = Time.time * swaySpeed + randomOffset;

            // Tính toán góc lệch đung đưa ngẫu nhiên
            float angleOffsetX = Mathf.Sin(timeVal) * swayAngleX;
            float angleOffsetZ = Mathf.Cos(timeVal * 0.9f) * swayAngleZ;

            // Áp dụng góc xoay mới bằng cách nhân với góc quay ban đầu
            Quaternion targetSway = Quaternion.Euler(angleOffsetX, 0, angleOffsetZ);
            transform.rotation = initialRotation * targetSway;
        }
    }
}
