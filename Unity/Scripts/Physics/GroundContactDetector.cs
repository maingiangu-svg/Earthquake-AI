using System;
using UnityEngine;

namespace EarthquakeAI.Physics
{
    /// <summary>
    /// Các trạng thái tiếp xúc của cảm biến với mặt đất.
    /// Theo đặc tả trong docs/architecture.md và docs/project-plan.md.
    /// </summary>
    public enum ContactState
    {
        DISCONNECTED,       // Cảm biến lơ lửng, nhấc khỏi mặt đất
        PARTIAL_CONTACT,    // Cảm biến bấp bênh, nghiêng lệch vượt ngưỡng cho phép
        CONTACT_ESTABLISHED // Tiếp xúc hoàn toàn phẳng, ổn định, sẵn sàng đo đạc
    }

    /// <summary>
    /// GroundContactDetector đảm nhận việc phát hiện và xác thực trạng thái tiếp xúc
    /// giữa thiết bị đo 3D và mặt đất thông qua kết hợp Collision Impulse, Contact Normal
    /// và Multi-point Raycasting hướng xuống.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class GroundContactDetector : MonoBehaviour
    {
        [Header("Ground Detection Configuration")]
        [Tooltip("LayerMask định nghĩa các Collider được coi là mặt đất.")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Góc nghiêng tối đa cho phép (độ) giữa Vector pháp tuyến bề mặt và Vector thẳng đứng (Vector3.up).")]
        [Range(1f, 45f)]
        [SerializeField] private float maxTiltAngleDeg = 15.0f;

        [Tooltip("Lực nén tiếp xúc tối thiểu (Newton) để xác nhận tiếp xúc vững chắc.")]
        [SerializeField] private float minContactForceN = 5.0f;

        [Header("Multi-Point Raycast Probe Settings")]
        [Tooltip("Số lượng tia Raycast chân đế kiểm tra đa điểm xung quanh viền cảm biến.")]
        [Range(3, 8)]
        [SerializeField] private int rayCount = 4;

        [Tooltip("Bán kính chân đế cảm biến (mét) dùng để bố trí các điểm Raycast.")]
        [SerializeField] private float probeRadius = 0.25f;

        [Tooltip("Khoảng cách dò tia Raycast hướng xuống từ chân đế cảm biến (mét).")]
        [SerializeField] private float rayDistance = 0.15f;

        [Tooltip("Độ lệch dọc (vertical offset) của điểm xuất phát Raycast so với gốc tọa độ transform.")]
        [SerializeField] private float rayOriginHeightOffset = 0.05f;

        [Header("Telemetry & Debug Readouts")]
        [SerializeField] private ContactState currentState = ContactState.DISCONNECTED;
        [SerializeField] private float currentContactForceN = 0f;
        [SerializeField] private float currentSurfaceTiltAngleDeg = 0f;
        [SerializeField] private Vector3 currentContactNormal = Vector3.up;
        [SerializeField] private int validRayHitCount = 0;

        // Sự kiện báo hiệu khi trạng thái tiếp xúc thay đổi
        public event Action<ContactState> OnContactStateChanged;

        // Public Properties cung cấp dữ liệu cho Telemetry Engine và UI
        public ContactState CurrentState => currentState;
        public float ContactForceN => currentContactForceN;
        public float SurfaceTiltAngleDeg => currentSurfaceTiltAngleDeg;
        public Vector3 ContactNormal => currentContactNormal;
        public bool IsContactEstablished => currentState == ContactState.CONTACT_ESTABLISHED;

        private Rigidbody rb;
        private bool isCollidingWithGround = false;
        private Vector3 accumulatedCollisionImpulse = Vector3.zero;
        private Vector3 accumulatedContactNormal = Vector3.zero;
        private int collisionContactPointsCount = 0;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            // 1. Tính toán lực tiếp xúc nén (Contact Force) từ Collision Impulse và Trọng lực
            CalculateContactForce();

            // 2. Thực hiện Multi-Point Raycast xác nhận chân đế xúc chạm mặt đất
            EvaluateMultiPointRaycasts();

            // 3. Đánh giá trạng thái tổng hợp (State Machine Logic)
            EvaluateContactState();

            // Reset dữ liệu va chạm khung hình trước cho FixedUpdate tiếp theo
            ResetCollisionAccumulators();
        }

        private void OnCollisionStay(Collision collision)
        {
            // Kiểm tra xem đối tượng va chạm có thuộc groundLayer hay không
            if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            {
                isCollidingWithGround = true;

                // Tích lũy xung lực va chạm (Impulse) và pháp tuyến va chạm (Normal)
                accumulatedCollisionImpulse += collision.impulse;

                foreach (ContactPoint contact in collision.contacts)
                {
                    accumulatedContactNormal += contact.normal;
                    collisionContactPointsCount++;
                }
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            {
                isCollidingWithGround = false;
            }
        }

        /// <summary>
        /// Tính toán lực nén tiếp xúc tổng cộng (N) và pháp tuyến trung bình.
        /// </summary>
        private void CalculateContactForce()
        {
            if (isCollidingWithGround && Time.fixedDeltaTime > 0f)
            {
                // Lực từ va chạm cơ học: F = Impulse / deltaT
                float impulseForce = (accumulatedCollisionImpulse.magnitude) / Time.fixedDeltaTime;

                // Trọng lực nén tĩnh: F_gravity = m * g (theo phương thẳng đứng nếu nghiêng ít)
                float staticGravityForce = rb.mass * UnityEngine.Physics.gravity.magnitude;

                currentContactForceN = impulseForce + staticGravityForce;

                // Tính toán Vector pháp tuyến trung bình từ các điểm va chạm
                if (collisionContactPointsCount > 0)
                {
                    currentContactNormal = (accumulatedContactNormal / collisionContactPointsCount).normalized;
                }
                else
                {
                    currentContactNormal = Vector3.up;
                }
            }
            else
            {
                currentContactForceN = 0f;
                currentContactNormal = Vector3.up;
            }

            // Tính góc nghiêng giữa pháp tuyến tiếp xúc và phương thẳng đứng Vector3.up
            currentSurfaceTiltAngleDeg = Vector3.Angle(currentContactNormal, Vector3.up);
        }

        /// <summary>
        /// Kiểm tra đa điểm bằng Raycast bố trí hình vòng tròn dưới chân cảm biến.
        /// </summary>
        private void EvaluateMultiPointRaycasts()
        {
            validRayHitCount = 0;
            Vector3 originCenter = transform.position + Vector3.up * rayOriginHeightOffset;

            // Dò 1 tia ở chính giữa tâm chân đế
            if (UnityEngine.Physics.Raycast(originCenter, Vector3.down, out RaycastHit centerHit, rayDistance + rayOriginHeightOffset, groundLayer))
            {
                validRayHitCount++;
            }

            // Dò các tia xung quanh viền chân đế với bán kính probeRadius
            for (int i = 0; i < rayCount; i++)
            {
                float angleRad = (i * 360f / rayCount) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angleRad) * probeRadius, 0f, Mathf.Sin(angleRad) * probeRadius);
                Vector3 rayOrigin = originCenter + transform.TransformDirection(offset);

                if (UnityEngine.Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance + rayOriginHeightOffset, groundLayer))
                {
                    validRayHitCount++;
                }
            }
        }

        /// <summary>
        /// Xác định trạng thái tiếp xúc tổng hợp dựa trên Collision, Raycast và Lực/Góc nghiêng.
        /// </summary>
        private void EvaluateContactState()
        {
            ContactState newState;

            // Nếu không có va chạm vật lý VÀ số tia Raycast trúng mặt đất < 2 -> Ngắt kết nối
            if (!isCollidingWithGround && validRayHitCount < 2)
            {
                newState = ContactState.DISCONNECTED;
            }
            // Nếu có chạm nhưng góc nghiêng lớn hơn ngưỡng hoặc lực nén quá nhỏ hoặc thiếu tia Raycast viền -> Bấp bênh
            else if (currentSurfaceTiltAngleDeg > maxTiltAngleDeg || currentContactForceN < minContactForceN || validRayHitCount < (rayCount / 2 + 1))
            {
                newState = ContactState.PARTIAL_CONTACT;
            }
            // Đạt đầy đủ tiêu chuẩn tiếp xúc chuẩn
            else
            {
                newState = ContactState.CONTACT_ESTABLISHED;
            }

            // Cập nhật và phát Event nếu trạng thái thay đổi
            if (newState != currentState)
            {
                currentState = newState;
                OnContactStateChanged?.Invoke(currentState);
            }
        }

        private void ResetCollisionAccumulators()
        {
            accumulatedCollisionImpulse = Vector3.zero;
            accumulatedContactNormal = Vector3.zero;
            collisionContactPointsCount = 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Hiển thị tia Raycast và Gizmos trong Unity Editor Scene view để trực quan hóa debug.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 originCenter = transform.position + Vector3.up * rayOriginHeightOffset;

            // Vẽ Gizmo tâm
            Gizmos.color = isCollidingWithGround ? Color.green : Color.red;
            Gizmos.DrawRay(originCenter, Vector3.down * (rayDistance + rayOriginHeightOffset));

            // Vẽ các Gizmo xung quanh chân đế
            for (int i = 0; i < rayCount; i++)
            {
                float angleRad = (i * 360f / rayCount) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angleRad) * probeRadius, 0f, Mathf.Sin(angleRad) * probeRadius);
                Vector3 rayOrigin = originCenter + transform.TransformDirection(offset);

                Gizmos.color = (currentState == ContactState.CONTACT_ESTABLISHED) ? Color.cyan : Color.yellow;
                Gizmos.DrawRay(rayOrigin, Vector3.down * (rayDistance + rayOriginHeightOffset));
                Gizmos.DrawWireSphere(rayOrigin, 0.02f);
            }
        }
#endif
    }
}
