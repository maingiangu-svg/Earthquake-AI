using System;
using UnityEngine;

namespace EarthquakeAI.DTO
{
    /// <summary>
    /// DTO chứa thông tin metadata về trạng thái tiếp xúc của cảm biến.
    /// Khớp 100% với Schema JSON Request trong docs/data-flow.md.
    /// </summary>
    [Serializable]
    public class SensorMetadataDTO
    {
        public string device_id = "SEISMIC_PROBE_ALPHA_01";
        public bool ground_contact_established = true;
        public float contact_force_n = 0.0f;
        public float surface_alignment_deg = 0.0f;
        public string surface_material = "Compacted_Soil_Bed";
    }

    /// <summary>
    /// DTO chứa chuỗi thời gian gia tốc 3 trục (g-force).
    /// </summary>
    [Serializable]
    public class RawTelemetryDTO
    {
        public int sampling_rate_hz = 100;
        public int sample_count = 500;
        public float[] acceleration_x;
        public float[] acceleration_y;
        public float[] acceleration_z;
    }

    /// <summary>
    /// DTO chứa các chỉ số đặc trưng địa chấn đã tính toán tại Unity C#.
    /// </summary>
    [Serializable]
    public class ComputedMetricsDTO
    {
        public float peak_acceleration_g = 0.0f;
        public float rms_acceleration = 0.0f;
        public float dominant_frequency_hz = 0.0f;
        public float signal_duration_s = 0.0f;
        public float cumulative_absolute_velocity = 0.0f;
    }

    /// <summary>
    /// Payload tổng thể gửi từ Unity sang FastAPI Backend qua REST API POST.
    /// </summary>
    [Serializable]
    public class SeismicTelemetryPayloadDTO
    {
        public string session_id = Guid.NewGuid().ToString();
        public string timestamp = DateTime.UtcNow.ToString("o");
        public SensorMetadataDTO sensor_metadata = new SensorMetadataDTO();
        public RawTelemetryDTO raw_telemetry = new RawTelemetryDTO();
        public ComputedMetricsDTO computed_metrics = new ComputedMetricsDTO();

        /// <summary>
        /// Chuyển đổi DTO thành chuỗi JSON chuẩn hóa.
        /// </summary>
        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }
    }
}
