using System;
using UnityEngine;
using EarthquakeAI.DTO;
using EarthquakeAI.Physics;

namespace EarthquakeAI.Sensors
{
    /// <summary>
    /// VirtualAccelerometer đóng vai trò là cảm biến gia tốc 3 trục ảo (Tri-axial Accelerometer),
    /// thu thập gia tốc địa chấn tại tần số 100 Hz vào Ring Buffer tuần hoàn (cửa sổ 5 giây, 500 samples).
    /// Đảm bảo ZERO Garbage Collection Allocation trong suốt vòng lặp lấy mẫu.
    /// Tích hợp thuật toán C# tính toán tức thì các chỉ số PGA, RMS, Dominant Frequency (FFT) và Signal Duration.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VirtualAccelerometer : MonoBehaviour
    {
        [Header("Sensor Device Info")]
        [SerializeField] private string deviceId = "SEISMIC_PROBE_ALPHA_01";
        [SerializeField] private string surfaceMaterial = "Compacted_Soil_Bed";

        [Header("Sampling Configuration")]
        [Tooltip("Tần số lấy mẫu cảm biến (Hz). Mặc định 100 Hz.")]
        [SerializeField] private int samplingRateHz = 100;

        [Tooltip("Độ dài cửa sổ thời gian bộ nhớ đệm (giây). Mặc định 5 giây.")]
        [SerializeField] private float windowDurationSeconds = 5.0f;

        [Tooltip("Ngưỡng Noise Floor (g) để xác định thời lượng chấn động Signal Duration.")]
        [SerializeField] private float noiseFloorThresholdG = 0.02f;

        [Header("Dependencies")]
        [SerializeField] private GroundContactDetector contactDetector;
        [SerializeField] private SeismicShakerManager shakerManager;

        [Header("Live Calculated Telemetry Readouts")]
        [SerializeField] private bool contactIntegrityWarning = false;
        [SerializeField] private float livePGA_g = 0f;
        [SerializeField] private float liveRMS_g = 0f;
        [SerializeField] private float liveDominantFreqHz = 0f;
        [SerializeField] private float liveSignalDurationS = 0f;
        [SerializeField] private float liveCAV = 0f;

        // Total Buffer Size = 100 Hz * 5s = 500 samples
        private int bufferSize;
        private float[] ringBufferX;
        private float[] ringBufferY;
        private float[] ringBufferZ;
        private float[] magnitudeBuffer;

        // Buffers dùng cho thuật toán FFT (kích thước lũy thừa của 2 = 512)
        private const int FFT_SIZE = 512;
        private float[] fftRealBuffer = new float[FFT_SIZE];
        private float[] fftImagBuffer = new float[FFT_SIZE];

        private int headIndex = 0;
        private int currentSampleCount = 0;
        private float sampleInterval;
        private float lastSampleTimer = 0f;

        private Rigidbody rb;
        private Vector3 lastVelocity = Vector3.zero;

        // Preallocated arrays cho xuất DTO (tránh GC Alloc khi export)
        private float[] exportArrayX;
        private float[] exportArrayY;
        private float[] exportArrayZ;

        public bool ContactIntegrityWarning => contactIntegrityWarning;
        public float LivePGA => livePGA_g;
        public float LiveRMS => liveRMS_g;
        public float LiveDominantFreq => liveDominantFreqHz;
        public float LiveSignalDuration => liveSignalDurationS;
        public float LiveCAV => liveCAV;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            if (contactDetector == null)
            {
                contactDetector = GetComponent<GroundContactDetector>();
            }

            if (shakerManager == null)
            {
                shakerManager = FindObjectOfType<SeismicShakerManager>();
            }

            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            sampleInterval = 1.0f / samplingRateHz;
            bufferSize = Mathf.RoundToInt(samplingRateHz * windowDurationSeconds);

            ringBufferX = new float[bufferSize];
            ringBufferY = new float[bufferSize];
            ringBufferZ = new float[bufferSize];
            magnitudeBuffer = new float[bufferSize];

            exportArrayX = new float[bufferSize];
            exportArrayY = new float[bufferSize];
            exportArrayZ = new float[bufferSize];

            headIndex = 0;
            currentSampleCount = 0;
        }

        private void FixedUpdate()
        {
            lastSampleTimer += Time.fixedDeltaTime;

            // Kiểm tra đến nhịp lấy mẫu (Sampling Interval = 0.01s cho 100Hz)
            if (lastSampleTimer >= sampleInterval)
            {
                lastSampleTimer -= sampleInterval;

                // 1. Đo đạc gia tốc hiện tại
                Vector3 currentAccelG = MeasureCurrentAcceleration();

                // 2. Kiểm tra tính toàn vẹn tiếp xúc mặt đất
                bool isContactEstablished = (contactDetector != null && contactDetector.IsContactEstablished);
                contactIntegrityWarning = !isContactEstablished;

                // Nếu mất tiếp xúc và không có Shaker -> đặt nhiễu hoặc đánh dấu warning
                if (!isContactEstablished)
                {
                    // Vẫn ghi nhận giá trị để phân tích nhiễu nhưng giữ cờ cảnh báo
                }

                // 3. Đẩy gia tốc vào Ring Buffer (Zero GC Alloc)
                PushSampleToBuffer(currentAccelG.x, currentAccelG.y, currentAccelG.z);

                // 4. Tính toán nhanh các chỉ số telemetry kỹ thuật
                CalculateMetrics();
            }
        }

        /// <summary>
        /// Đo đạc gia tốc 3 trục tổng hợp từ Rigidbody motion và SeismicShakerManager (tính theo đơn vị g).
        /// </summary>
        private Vector3 MeasureCurrentAcceleration()
        {
            Vector3 rawAccelMs2 = Vector3.zero;

            if (shakerManager != null && shakerManager.IsShaking)
            {
                // Ưu tiên gia tốc trực tiếp từ mô phỏng rung chấn
                rawAccelMs2 = shakerManager.CurrentAccelerationG * 9.81f;
            }
            else
            {
                // Lấy gia tốc biến thiên từ Rigidbody delta velocity: a = (v - v_last) / dt
                Vector3 currentVel = rb.velocity;
                rawAccelMs2 = (currentVel - lastVelocity) / Time.fixedDeltaTime;
                lastVelocity = currentVel;
            }

            // Thêm nhiễu cảm biến nhỏ giả lập (Sensor Noise Floor ~ 0.002g)
            float noiseX = (UnityEngine.Random.value - 0.5f) * 0.004f;
            float noiseY = (UnityEngine.Random.value - 0.5f) * 0.004f;
            float noiseZ = (UnityEngine.Random.value - 0.5f) * 0.004f;

            // Đổi sang đơn vị g (1g = 9.81 m/s^2)
            Vector3 accelG = new Vector3(
                (rawAccelMs2.x / 9.81f) + noiseX,
                (rawAccelMs2.y / 9.81f) + noiseY,
                (rawAccelMs2.z / 9.81f) + noiseZ
            );

            return accelG;
        }

        /// <summary>
        /// Ghi sample gia tốc vào Ring Buffer tuần hoàn. Không khởi tạo bộ nhớ mới.
        /// </summary>
        private void PushSampleToBuffer(float ax, float ay, float az)
        {
            ringBufferX[headIndex] = ax;
            ringBufferY[headIndex] = ay;
            ringBufferZ[headIndex] = az;
            magnitudeBuffer[headIndex] = Mathf.Sqrt(ax * ax + ay * ay + az * az);

            headIndex = (headIndex + 1) % bufferSize;

            if (currentSampleCount < bufferSize)
            {
                currentSampleCount++;
            }
        }

        /// <summary>
        /// Tính toán ngay các đặc trưng địa chấn: PGA, RMS, Signal Duration, CAV và FFT Dominant Frequency.
        /// </summary>
        private void CalculateMetrics()
        {
            if (currentSampleCount == 0) return;

            float maxMagSq = 0f;
            float sumMagSq = 0f;
            float sumAbsMag = 0f;
            int activeOverThresholdCount = 0;

            for (int i = 0; i < currentSampleCount; i++)
            {
                float mag = magnitudeBuffer[i];
                float magSq = mag * mag;

                if (magSq > maxMagSq)
                {
                    maxMagSq = magSq;
                }

                sumMagSq += magSq;
                sumAbsMag += mag;

                if (mag >= noiseFloorThresholdG)
                {
                    activeOverThresholdCount++;
                }
            }

            // 1. Peak Ground Acceleration (PGA)
            livePGA_g = Mathf.Sqrt(maxMagSq);

            // 2. Root Mean Square Acceleration (RMS)
            liveRMS_g = Mathf.Sqrt(sumMagSq / currentSampleCount);

            // 3. Signal Duration (Td) = Số sample vượt ngưỡng * khoảng thời gian lấy mẫu
            liveSignalDurationS = activeOverThresholdCount * sampleInterval;

            // 4. Cumulative Absolute Velocity (CAV) = Sum(|a|) * dt * g_to_ms2
            liveCAV = sumAbsMag * sampleInterval * 9.81f;

            // 5. Tính Tần số Thống trị (Dominant Frequency) qua FFT
            liveDominantFreqHz = CalculateFFTDominantFrequency();
        }

        /// <summary>
        /// Thực hiện Radix-2 Cooley-Tukey FFT trên tín hiệu biên độ gia tốc để trích xuất tần số thống trị.
        /// </summary>
        private float CalculateFFTDominantFrequency()
        {
            if (currentSampleCount < 16) return 0f;

            // 1. Copy dữ liệu vào FFT Real Buffer (kèm cửa sổ Hanning Window để giảm rò rỉ phổ)
            for (int i = 0; i < FFT_SIZE; i++)
            {
                if (i < currentSampleCount)
                {
                    int index = (headIndex - currentSampleCount + i + bufferSize) % bufferSize;
                    float window = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (currentSampleCount - 1)));
                    fftRealBuffer[i] = magnitudeBuffer[index] * window;
                }
                else
                {
                    fftRealBuffer[i] = 0f; // Zero Padding
                }
                fftImagBuffer[i] = 0f;
            }

            // 2. Thực hiện FFT Cooley-Tukey In-Place
            ComputeFFT(fftRealBuffer, fftImagBuffer, FFT_SIZE);

            // 3. Tìm Peak Magnitude trong phổ tần số (Bỏ qua thành phần DC i = 0)
            int maxBin = 1;
            float maxSpectrumMag = 0f;
            int halfSize = FFT_SIZE / 2;

            for (int i = 1; i < halfSize; i++)
            {
                float mag = fftRealBuffer[i] * fftRealBuffer[i] + fftImagBuffer[i] * fftImagBuffer[i];
                if (mag > maxSpectrumMag)
                {
                    maxSpectrumMag = mag;
                    maxBin = i;
                }
            }

            // Tần số tương ứng = maxBin * (f_s / N_fft)
            float dominantFreq = maxBin * ((float)samplingRateHz / FFT_SIZE);
            return dominantFreq;
        }

        /// <summary>
        /// Giải thuật Cooley-Tukey Radix-2 FFT in-place tối ưu hiệu năng C#.
        /// </summary>
        private void ComputeFFT(float[] real, float[] imag, int n)
        {
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float tempR = real[i]; real[i] = real[j]; real[j] = tempR;
                    float tempI = imag[i]; imag[i] = imag[j]; imag[j] = tempI;
                }
                int k = n >> 1;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                float ang = -2f * Mathf.PI / len;
                float wlenR = Mathf.Cos(ang);
                float wlenI = Mathf.Sin(ang);

                for (int i = 0; i < n; i += len)
                {
                    float wR = 1f;
                    float wI = 0f;
                    for (int m = 0; m < len / 2; m++)
                    {
                        int u = i + m;
                        int v = i + m + len / 2;

                        float uR = real[u], uI = imag[u];
                        float vR = real[v] * wR - imag[v] * wI;
                        float vI = real[v] * wI + imag[v] * wR;

                        real[u] = uR + vR; imag[u] = uI + vI;
                        real[v] = uR - vR; imag[v] = uI - vI;

                        float nextWR = wR * wlenR - wI * wlenI;
                        wI = wR * wlenI + wI * wlenR;
                        wR = nextWR;
                    }
                }
            }
        }

        /// <summary>
        /// Đóng gói dữ liệu Telemetry thành DTO sẵn sàng cho JSON Serialization.
        /// </summary>
        public SeismicTelemetryPayloadDTO GetTelemetryPayload()
        {
            SeismicTelemetryPayloadDTO payload = new SeismicTelemetryPayloadDTO();

            // 1. Metadata
            payload.sensor_metadata.device_id = deviceId;
            payload.sensor_metadata.ground_contact_established = (contactDetector != null && contactDetector.IsContactEstablished);
            payload.sensor_metadata.contact_force_n = contactDetector != null ? contactDetector.ContactForceN : 0f;
            payload.sensor_metadata.surface_alignment_deg = contactDetector != null ? contactDetector.SurfaceTiltAngleDeg : 0f;
            payload.sensor_metadata.surface_material = surfaceMaterial;

            // 2. Raw Telemetry
            payload.raw_telemetry.sampling_rate_hz = samplingRateHz;
            payload.raw_telemetry.sample_count = currentSampleCount;

            // Sắp xếp chuỗi thời gian từ cũ nhất tới mới nhất
            for (int i = 0; i < currentSampleCount; i++)
            {
                int ringIndex = (headIndex - currentSampleCount + i + bufferSize) % bufferSize;
                exportArrayX[i] = ringBufferX[ringIndex];
                exportArrayY[i] = ringBufferY[ringIndex];
                exportArrayZ[i] = ringBufferZ[ringIndex];
            }

            payload.raw_telemetry.acceleration_x = exportArrayX;
            payload.raw_telemetry.acceleration_y = exportArrayY;
            payload.raw_telemetry.acceleration_z = exportArrayZ;

            // 3. Computed Metrics
            payload.computed_metrics.peak_acceleration_g = livePGA_g;
            payload.computed_metrics.rms_acceleration = liveRMS_g;
            payload.computed_metrics.dominant_frequency_hz = liveDominantFreqHz;
            payload.computed_metrics.signal_duration_s = liveSignalDurationS;
            payload.computed_metrics.cumulative_absolute_velocity = liveCAV;

            return payload;
        }
    }
}
