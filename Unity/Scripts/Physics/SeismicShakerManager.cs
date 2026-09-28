using System;
using UnityEngine;

namespace EarthquakeAI.Physics
{
    /// <summary>
    /// SeismicShakerManager quản lý mô phỏng rung chấn địa chấn thời gian thực lên mặt đất (Ground Mesh).
    /// Tái tạo đặc tính sóng P (Sóng sơ cấp - tần số cao, biên độ nhỏ) và sóng S (Sóng thứ cấp - tần số thấp, biên độ lớn),
    /// kết hợp Perlin Noise để tạo hiệu ứng địa chấn chân thực.
    /// Đồng thời truyền lực tác động lên các Rigidbody mảnh vụn/sắt vụn (debris) xung quanh.
    /// </summary>
    public class SeismicShakerManager : MonoBehaviour
    {
        [Header("Ground & Environment Setup")]
        [Tooltip("Transform của mặt đất sẽ chịu tác động rung lắc.")]
        [SerializeField] private Transform groundTransform;

        [Tooltip("LayerMask đánh dấu các vật thể nhỏ/sắt vụn (debris) sẽ phản ứng với chấn động.")]
        [SerializeField] private LayerMask debrisLayer;

        [Tooltip("Bán kính vùng tác động lực rung chấn tới mảnh vỡ xung quanh (mét).")]
        [SerializeField] private float debrisInfluenceRadius = 25.0f;

        [Header("Seismic Wave Configurations")]
        [Tooltip("Gia tốc đỉnh mục tiêu PGA (g-force). Ví dụ: 0.2g (nhẹ), 0.6g (mạnh), 1.2g (cực mạnh).")]
        [Range(0.05f, 2.5f)]
        [SerializeField] private float targetPGA_g = 0.65f;

        [Tooltip("Tần số rung chấn cơ bản (Hz). Thường từ 1.0Hz đến 10.0Hz.")]
        [Range(0.5f, 15.0f)]
        [SerializeField] private float baseFrequencyHz = 3.5f;

        [Tooltip("Thời lượng tổng cộng của đợt rung chấn (giây).")]
        [Range(1.0f, 30.0f)]
        [SerializeField] private float durationSeconds = 6.0f;

        [Tooltip("Tỉ lệ thời gian sóng P xuất hiện trước sóng S (0.2 = 20% thời lượng đầu là sóng P).")]
        [Range(0.1f, 0.4f)]
        [SerializeField] private float pWaveRatio = 0.25f;

        [Header("Automatic / Trigger Settings")]
        [Tooltip("Tự động kích hoạt rung chấn khi nhấn Start (dành cho mục đích thử nghiệm).")]
        [SerializeField] private bool autoTriggerOnStart = false;

        [Header("Live Output Readouts")]
        [SerializeField] private bool isShaking = false;
        [SerializeField] private float currentShakeTime = 0f;
        [SerializeField] private Vector3 currentAccelerationG = Vector3.zero;
        [SerializeField] private Vector3 currentDisplacementMeters = Vector3.zero;

        // Sự kiện báo hiệu bắt đầu và kết thúc chấn động
        public event Action OnShakeStarted;
        public event Action OnShakeEnded;

        public bool IsShaking => isShaking;
        public Vector3 CurrentAccelerationG => currentAccelerationG;
        public Vector3 CurrentDisplacementMeters => currentDisplacementMeters;

        private Vector3 initialGroundPosition;
        private Quaternion initialGroundRotation;
        private Collider[] debrisBuffer = new Collider[64]; // Preallocated buffer tránh GC Alloc

        private void Awake()
        {
            if (groundTransform == null)
            {
                groundTransform = transform;
            }

            initialGroundPosition = groundTransform.position;
            initialGroundRotation = groundTransform.rotation;
        }

        private void Start()
        {
            if (autoTriggerOnStart)
            {
                TriggerSeismicEvent(targetPGA_g, durationSeconds);
            }
        }

        private void FixedUpdate()
        {
            if (!isShaking) return;

            currentShakeTime += Time.fixedDeltaTime;

            if (currentShakeTime >= durationSeconds)
            {
                StopSeismicEvent();
                return;
            }

            // Tính toán gia tốc và chuyển vị địa chấn thời gian thực
            CalculateSeismicMotion(currentShakeTime, out Vector3 displacement, out Vector3 accelG);

            currentDisplacementMeters = displacement;
            currentAccelerationG = accelG;

            // 1. Dịch chuyển mặt đất (Ground Displacement)
            groundTransform.position = initialGroundPosition + currentDisplacementMeters;

            // 2. Truyền lực gia tốc địa chấn tới các Rigidbody debris xung quanh
            ApplyPhysicsForcesToDebris(accelG);
        }

        /// <summary>
        /// Kích hoạt một đợt động đất mô phỏng với PGA và thời lượng tùy chỉnh.
        /// </summary>
        public void TriggerSeismicEvent(float pgaG, float durationSec)
        {
            targetPGA_g = Mathf.Max(0.01f, pgaG);
            durationSeconds = Mathf.Max(0.5f, durationSec);
            currentShakeTime = 0f;
            isShaking = true;

            OnShakeStarted?.Invoke();
        }

        /// <summary>
        /// Dừng đợt động đất và đưa mặt đất về vị trí ban đầu.
        /// </summary>
        public void StopSeismicEvent()
        {
            isShaking = false;
            currentShakeTime = 0f;
            currentAccelerationG = Vector3.zero;
            currentDisplacementMeters = Vector3.zero;

            if (groundTransform != null)
            {
                groundTransform.position = initialGroundPosition;
                groundTransform.rotation = initialGroundRotation;
            }

            OnShakeEnded?.Invoke();
        }

        /// <summary>
        /// Thuật toán mô phỏng sóng địa chấn tổng hợp sóng P (Primary) + sóng S (Secondary) + Noise.
        /// </summary>
        private void CalculateSeismicMotion(float time, out Vector3 displacement, out Vector3 accelG)
        {
            float normalizedTime = time / durationSeconds;

            // Envelope Function (Arias Intensity Envelope): Tăng dần -> Đạt đỉnh -> Suy giảm dần
            float envelope = CalculateEnvelope(normalizedTime);

            // Phân chia giai đoạn Sóng P (P-Wave) và Sóng S (S-Wave)
            float pWaveEnd = durationSeconds * pWaveRatio;
            bool isPWavePhase = time < pWaveEnd;

            float pgaMs2 = targetPGA_g * 9.81f; // Chuyển đổi từ g sang m/s^2

            if (isPWavePhase)
            {
                // Sóng P: Nén theo phương dọc (Y), tần số cao hơn (2x), biên độ khoảng 30% PGA
                float freqP = baseFrequencyHz * 2.2f;
                float verticalAccMs2 = Mathf.Sin(2f * Mathf.PI * freqP * time) * pgaMs2 * 0.35f * envelope;

                // Tích phân 2 lần xấp xỉ gia tốc -> chuyển vị: d = -a / (omega^2)
                float omega = 2f * Mathf.PI * freqP;
                float verticalDisp = -verticalAccMs2 / (omega * omega);

                accelG = new Vector3(0f, verticalAccMs2 / 9.81f, 0f);
                displacement = new Vector3(0f, verticalDisp, 0f);
            }
            else
            {
                // Sóng S: Cắt ngang theo phương X & Z, tần số cơ bản, biên độ đạt 100% PGA
                float phaseX = time * baseFrequencyHz * 2f * Mathf.PI;
                float phaseZ = time * baseFrequencyHz * 1.8f * Mathf.PI + 1.2f; // Phân cực lệch pha

                // Thêm Perlin Noise biến đổi hỗn loạn
                float noiseX = (Mathf.PerlinNoise(time * 5f, 0f) - 0.5f) * 2f;
                float noiseZ = (Mathf.PerlinNoise(0f, time * 5f) - 0.5f) * 2f;

                float accXMs2 = (Mathf.Sin(phaseX) + noiseX * 0.3f) * pgaMs2 * envelope;
                float accZMs2 = (Mathf.Cos(phaseZ) + noiseZ * 0.3f) * pgaMs2 * envelope;
                float accYMs2 = (Mathf.Sin(phaseX * 1.5f) * 0.25f) * pgaMs2 * envelope; // Thành phần đứng nhỏ

                float omegaS = 2f * Mathf.PI * baseFrequencyHz;
                float dispX = -accXMs2 / (omegaS * omegaS);
                float dispZ = -accZMs2 / (omegaS * omegaS);
                float dispY = -accYMs2 / (omegaS * omegaS * 2f);

                accelG = new Vector3(accXMs2 / 9.81f, accYMs2 / 9.81f, accZMs2 / 9.81f);
                displacement = new Vector3(dispX, dispY, dispZ);
            }
        }

        /// <summary>
        /// Hàm bao Arias Envelope (Trapezoid / Exponential decay) tạo nhịp điệu sinh động cho chấn động.
        /// </summary>
        private float CalculateEnvelope(float tNorm)
        {
            if (tNorm < 0.15f)
            {
                // Giai đoạn tăng tốc kích hoạt (Ramp up)
                return Mathf.SmoothStep(0.0f, 1.0f, tNorm / 0.15f);
            }
            else if (tNorm < 0.65f)
            {
                // Giai đoạn chấn động mạnh nhất (Peak Plateau)
                return 1.0f;
            }
            else
            {
                // Giai đoạn suy giảm (Decay)
                return Mathf.Exp(-4.0f * (tNorm - 0.65f));
            }
        }

        /// <summary>
        /// Quét các Rigidbody trong vùng ảnh hưởng và tác dụng lực gia tốc F = m * a.
        /// Sử dụng OverlapSphereNonAlloc để đảm bảo Zero Memory Allocation per frame.
        /// </summary>
        private void ApplyPhysicsForcesToDebris(Vector3 accelG)
        {
            Vector3 accelMs2 = accelG * 9.81f;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(groundTransform.position, debrisInfluenceRadius, debrisBuffer, debrisLayer);

            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = debrisBuffer[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    // Tác dụng gia tốc thẳng lên mảnh vỡ: ForceMode.Acceleration
                    rb.AddForce(accelMs2, ForceMode.Acceleration);

                    // Thêm mô-men xoắn ngẫu nhiên tạo độ nảy xoay sinh động
                    if (accelG.magnitude > 0.2g)
                    {
                        Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * accelG.magnitude * 2.0f;
                        rb.AddTorque(randomTorque, ForceMode.Acceleration);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Vector3 center = groundTransform != null ? groundTransform.position : transform.position;
            Gizmos.DrawWireSphere(center, debrisInfluenceRadius);
        }
#endif
    }
}
