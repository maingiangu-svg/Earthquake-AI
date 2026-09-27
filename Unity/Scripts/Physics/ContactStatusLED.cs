using UnityEngine;

namespace EarthquakeAI.Physics
{
    /// <summary>
    /// ContactStatusLED điều khiển màu sắc và hiệu ứng phát sáng (Emission) của đèn LED chỉ báo
    /// dựa trên trạng thái tiếp xúc từ GroundContactDetector.
    /// Tuân thủ bảng màu Tactical Anti-Slop (Red / Yellow / Electric Cyan).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ContactStatusLED : MonoBehaviour
    {
        [Header("Target Ground Contact Detector")]
        [Tooltip("Tham chiếu tới script GroundContactDetector trên thiết bị đo.")]
        [SerializeField] private GroundContactDetector detector;

        [Header("Tactical Anti-Slop Color Palette")]
        [Tooltip("Màu LED khi ở trạng thái DISCONNECTED (Mất tiếp xúc / Lơ lửng).")]
        [SerializeField] private Color disconnectedColor = new Color(1.0f, 0.2f, 0.2f); // Crimson Red (#FF3333)

        [Tooltip("Màu LED khi ở trạng thái PARTIAL_CONTACT (Bấp bênh / Nghiêng lệch).")]
        [SerializeField] private Color partialContactColor = new Color(1.0f, 0.75f, 0.0f); // Amber Yellow (#FFC000)

        [Tooltip("Màu LED khi ở trạng thái CONTACT_ESTABLISHED (Tiếp xúc hoàn hảo).")]
        [SerializeField] private Color establishedColor = new Color(0.0f, 0.94f, 1.0f); // Electric Cyan (#00F0FF)

        [Header("Emission Settings")]
        [Tooltip("Bật hiệu ứng phát sáng (Emission) cho Material.")]
        [SerializeField] private bool useEmission = true;

        [Tooltip("Cường độ phát sáng Emission.")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float emissionIntensity = 2.0f;

        private Renderer targetRenderer;
        private MaterialPropertyBlock propBlock;
        private static readonly int BaseColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();

            // Tự động tìm detector trên cùng GameObject hoặc cha nếu chưa gán
            if (detector == null)
            {
                detector = GetComponentInParent<GroundContactDetector>();
            }
        }

        private void OnEnable()
        {
            if (detector != null)
            {
                detector.OnContactStateChanged += HandleContactStateChanged;
                // Cập nhật trạng thái màu sắc ngay khi bật Script
                UpdateLEDVisual(detector.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (detector != null)
            {
                detector.OnContactStateChanged -= HandleContactStateChanged;
            }
        }

        private void HandleContactStateChanged(ContactState newState)
        {
            UpdateLEDVisual(newState);
        }

        /// <summary>
        /// Cập nhật màu sắc LED sử dụng MaterialPropertyBlock để tối ưu hiệu năng (không tạo bản sao Material).
        /// </summary>
        public void UpdateLEDVisual(ContactState state)
        {
            Color targetColor = GetColorForState(state);

            targetRenderer.GetPropertyBlock(propBlock);

            // Cập nhật Base Color
            propBlock.SetColor(BaseColorProp, targetColor);

            // Cập nhật Emission Color nếu được bật
            if (useEmission)
            {
                Color finalEmission = targetColor * emissionIntensity;
                propBlock.SetColor(EmissionColorProp, finalEmission);
            }

            targetRenderer.SetPropertyBlock(propBlock);
        }

        private Color GetColorForState(ContactState state)
        {
            switch (state)
            {
                case ContactState.DISCONNECTED:
                    return disconnectedColor;
                case ContactState.PARTIAL_CONTACT:
                    return partialContactColor;
                case ContactState.CONTACT_ESTABLISHED:
                    return establishedColor;
                default:
                    return disconnectedColor;
            }
        }
    }
}
