# Lộ trình Triển khai Dự án: AI Earthquake Analysis System

Document Version: 1.0.0  
Last Updated: 2026-09-27  
Status: Project Plan Baseline

---

## 1. Chiến lược Triển khai (Execution Strategy)

Dự án được phân chia thành **6 Phase tuần tự**, triển khai theo nguyên tắc phát triển tăng dần (Incremental Development). Mỗi Phase đều có mục tiêu rõ ràng, danh sách công việc cụ thể và Tiêu chí Hoàn thành (Definition of Done - DoD) có thể kiểm thử định lượng.

Mốc bắt đầu bắt buộc phải đạt được là **MVP Contact Detection** ở Phase 1 trước khi mở rộng sang mô phỏng vật lý gia tốc, tích hợp ML và Gemini AI.

```
[Phase 1: MVP Contact Detection] 
       │
       ▼
[Phase 2: Vibration & Virtual Sensor Engine] 
       │
       ▼
[Phase 3: FastAPI & ML Quantitative Engine] 
       │
       ▼
[Phase 4: Gemini Natural Language AI Integration] 
       │
       ▼
[Phase 5: Anti-Slop Telemetry HUD & Integration] 
       │
       ▼
[Phase 6: Verification, Edge Cases & Polish]
```

---

## 2. Chi tiết Các Phase Triển khai

### Phase 1: MVP Ground Contact & Basic Physics (Unity)
**Mục tiêu**: Xây dựng nền tảng vật lý 3D trong Unity, cho phép người dùng tương tác với thiết bị đo động đất 3D và kích hoạt hệ thống phát hiện tiếp xúc mặt đất (Contact Detection System).

- **Nhiệm vụ 1.1**: Thiết lập Unity Scene baseline với mặt đất (Ground Mesh Terrain) và vật thể thiết bị đo 3D (Seismic Sensor Probe Object) chứa Rigidbody và Colliders.
- **Nhiệm vụ 1.2**: Triển khai `GroundContactDetector.cs` sử dụng kết hợp `OnCollisionStay` / `OnTriggerStay` và Multi-raycast Downward Check để xác định xem chân cảm biến có tiếp xúc vững chắc với mặt đất hay không.
- **Nhiệm vụ 1.3**: Đo đạc lực nén tiếp xúc $F_c$ (Contact Normal Force) và góc nghiêng tiếp xúc (Surface Normal Alignment Angle).
- **Nhiệm vụ 1.4**: Xây dựng đèn báo trạng thái đơn giản (Status Indicator LED Component) đổi màu dựa trên trạng thái `DISCONNECTED` (Đỏ) / `PARTIAL` (Vàng) / `ESTABLISHED` (Xanh Cyan).

- **Definition of Done (DoD)**:
  - Khi kéo/thả thiết bị chạm đất vuông góc: Trạng thái chuyển sang `CONTACT_ESTABLISHED` trong < 10ms.
  - Khi nghiêng thiết bị > 15 độ hoặc nhấc lơ lửng: Trạng thái tự động chuyển về `PARTIAL_CONTACT` hoặc `DISCONNECTED`.
  - Console Unity xuất log chính xác giá trị Contact Force (N) và Vector pháp tuyến mặt đất.

---

### Phase 2: Vibration Simulator & Virtual Telemetry Engine (Unity)
**Mục tiêu**: Tạo cơ chế mô phỏng rung chấn địa chấn thời gian thực lên mặt đất và vật thể nhỏ, đồng thời xây dựng bộ cảm biến ảo thu thập dữ liệu gia tốc 3 trục ($a_x, a_y, a_z$) và tính toán thông số kỹ thuật.

- **Nhiệm vụ 2.1**: Triển khai `SeismicShakerManager.cs` tạo chuyển động rung lắc mặt đất theo mẫu sóng địa chấn (Waveform Data Manager đọc file CSV hoặc sinh sóng Sin tổng hợp điều tần).
- **Nhiệm vụ 2.2**: Cấu hình phản ứng vật lý cho mảnh vỡ/sắt vụn (Debris Rigidbodies) trên mặt đất để chúng nảy, trượt và văng theo đúng chấn động địa chấn.
- **Nhiệm vụ 2.3**: Xây dựng `VirtualAccelerometer.cs` gắn trên thiết bị đo, thực hiện lấy mẫu gia tốc 3 trục ở tần số $f_s = 100\text{ Hz}$ vào một Ring Buffer tuần hoàn (cửa sổ 5 giây).
- **Nhiệm vụ 2.4**: Lập trình thuật toán C# tính toán các tham số địa chấn ngay tại Unity:
  - Peak Ground Acceleration (PGA)
  - Root Mean Square Acceleration (RMS)
  - Fast Fourier Transform (FFT) trích xuất Tần số thống trị ($f_d$)
  - Độ dài thời gian rung chấn ($T_d$)

- **Definition of Done (DoD)**:
  - Kích hoạt chấn động mô phỏng: Mặt đất rung lắc, debris di chuyển tự nhiên theo vật lý.
  - Telemetry Engine ghi nhận chuỗi dữ liệu $a_x, a_y, a_z$ ổn định ở 100Hz mà không gây giật frame (FPS $\ge 60$).
  - Thuật toán C# tính toán chính xác PGA, RMS, $f_d$ phù hợp với sóng địa chấn đầu vào.

---

### Phase 3: FastAPI Backend & ML Quantitative Engine (Python)
**Mục tiêu**: Xây dựng REST API Backend với FastAPI và huấn luyện mô hình Machine Learning định lượng địa chấn từ dữ liệu cảm biến ảo.

- **Nhiệm vụ 3.1**: Khởi tạo cấu trúc dự án Python FastAPI chuẩn:
  ```
  Backend/
  ├── app/
  │   ├── main.py
  │   ├── api/v1/endpoints/seismic.py
  │   ├── schemas/seismic_schema.py
  │   ├── services/ml_service.py
  │   └── services/gemini_service.py
  └── requirements.txt
  ```
- **Nhiệm vụ 3.2**: Xây dựng Pydantic Schemas V2 cho dữ liệu Request/Response chuẩn hóa giữa Unity và FastAPI.
- **Nhiệm vụ 3.3**: Thu thập/Tổng hợp dataset địa chấn mẫu (từ USGS/PEER hoặc sinh tự động bằng script NumPy) và huấn luyện các mô hình scikit-learn:
  - `RandomForestRegressor`: Ước tính độ lớn Động đất (Moment Magnitude $M_w$).
  - `RandomForestClassifier`: Phân loại cấp độ chấn động Mercalli (MMI Class I - XII).
  - `StructuralRiskModel`: Tính toán chỉ số rủi ro công trình (Structural Risk Index 0% - 100%).
- **Nhiệm vụ 3.4**: Đóng gói các model đã huấn luyện (`.joblib`) và viết `MLService` trong FastAPI để thực hiện inference online.

- **Definition of Done (DoD)**:
  - Endpoint `POST /api/v1/seismic/analyze` tiếp nhận thành công JSON request chứa thông số telemetry từ Unity.
  - ML Engine trả về kết quả dự đoán $M_w$, MMI Class và Risk Score với thời gian phản hồi < 50ms.
  - Viết đầy đủ Unit Test cho API endpoints đạt code coverage > 85%.

---

### Phase 4: Gemini Natural Language AI Integration (Python Backend)
**Mục tiêu**: Khai thác Gemini API thông qua Google GenAI SDK để sinh văn bản giải thích tự nhiên, chính xác theo phong cách báo cáo địa chấn học chuyên nghiệp.

- **Nhiệm vụ 4.1**: Cấu hình `google-genai` SDK trong `GeminiService` với API key bảo mật qua environment variable `GEMINI_API_KEY`.
- **Nhiệm vụ 4.2**: Thiết kế **Structured System Instruction & Prompt Template**:
  - Định hình Gemini làm Chuyên gia Địa chấn học.
  - Đưa vào ngữ cảnh: Dữ liệu cảm biến ảo + Kết quả định lượng từ ML Model + Trạng thái tiếp xúc đất.
  - Ép kiểu định dạng đầu ra chuẩn JSON (hoặc Markdown có cấu trúc) phân chia 4 mục chính.
- **Nhiệm vụ 4.3**: Thiết lập tham số điều khiển `temperature = 0.2` để giữ nhận định chính xác, tính nhất quán khoa học cao.
- **Nhiệm vụ 4.4**: Xây dựng cơ chế **Fallback Generator**: Khi Gemini API bị rớt mạng hoặc quá tải, hệ thống tự động sinh nhận định dựa trên rule-based template mà không làm sập API Backend.

- **Definition of Done (DoD)**:
  - Backend kết hợp thành công output định lượng của ML và nhận xét tự nhiên từ Gemini API vào cùng một response JSON.
  - Nhận xét từ Gemini mạch lạc, chính xác về mặt vật lý chấn động, đưa ra khuyến nghị an toàn phù hợp với cấp độ chấn động.
  - Cơ chế Fallback hoạt động mượt mà khi ngắt kết nối Internet của server.

---

### Phase 5: Anti-Slop Scientific Telemetry UI & Unity-Backend Integration
**Mục tiêu**: Phát triển giao diện Telemetry Dashboard chuẩn khoa học sắc nét trong Unity (tuân thủ nguyên tắc Anti-Slop Design) và hoàn thiện luồng tích hợp End-to-End từ Unity đến Backend.

- **Nhiệm vụ 5.1**: Thiết kế và lập trình **Anti-Slop Scientific Telemetry HUD** trong Unity:
  - Font chữ: Nối kết JetBrains Mono / Space Mono vào Unity Font Asset (TextMeshPro).
  - Color Palette: Lưới màu tối khoa học (`#0A0D12` Dark Slate, `#00F0FF` Cyan active, `#FF3366` Alert Crimson, `#161B22` Card Slate). Loại bỏ hoàn toàn gradient AI tím/hồng rập khuôn.
  - Grid Layout: Thiết lập khung lưới 1px Hairline phân chia 4 khu vực: (1) Device Telemetry & Contact Status, (2) Waveform Real-time Graph, (3) Quantitative ML Indicators, (4) Gemini AI Natural Language Insight Panel.
- **Nhiệm vụ 5.2**: Xây dựng **World-Space 3D UI Panel** trên thân cảm biến 3D hiển thị tức thì trạng thái tiếp xúc và chỉ số PGA.
- **Nhiệm vụ 5.3**: Triển khai `SeismicApiClient.cs` sử dụng `UnityWebRequest` bất đồng bộ (`async/await` qua `TaskCompletionSource` hoặc `UniTask`) để gửi telemetry data tới Backend và nhận response.
- **Nhiệm vụ 5.4**: Render dữ liệu kết quả từ FastAPI (ML Metrics + Gemini Text) trực tiếp lên HUD và 3D UI Panel.

- **Definition of Done (DoD)**:
  - Giao diện Telemetry HUD trong Unity hiển thị sắc nét, tối giản, trực quan khoa học, không bị hiện tượng rập khuôn slop.
  - Thực hiện thành công chu trình khép kín: Tương tác cảm biến 3D chạm đất -> Kích hoạt Rung chấn -> Thu thập Telemetry -> Gửi REST API -> Backend xử lý ML & Gemini -> Render kết quả lên HUD Unity trong thời gian < 1.5s (bao gồm thời gian gọi Gemini API).

---

### Phase 6: Optimization, Verification & Edge-Case Polish
**Mục tiêu**: Kiểm thử toàn diện hệ thống, xử lý các trường hợp ngoại lệ và tối ưu hóa hiệu năng vận hành.

- **Nhiệm vụ 6.1**: Xử lý các edge-case vật lý:
  - Mất tiếp xúc đất đột ngột khi chấn động quá mạnh (`ground_contact_established` rơi về `false`).
  - Nhiễu gia tốc cực lớn do va chạm trực tiếp của mảnh vỡ vào thiết bị (Spike Noise Filtering).
- **Nhiệm vụ 6.2**: Tối ưu hiệu năng:
  - Đảm bảo Unity duy trì 60+ FPS trong suốt quá trình thu thập và hiển thị sóng gia tốc.
  - Tối ưu bộ nhớ Ring Buffer C# tránh tạo rác Garbage Collector (GC Alloc = 0 trong `Update()`).
- **Nhiệm vụ 6.3**: Kiểm thử chịu tải (Stress Testing) đường truyền API giữa Unity và FastAPI Backend.

- **Definition of Done (DoD)**:
  - Hệ thống vận hành ổn định trong các kịch bản rung chấn liên tục 10 phút.
  - Giao diện HUD cập nhật mượt mà, thông báo rõ ràng trường hợp nhiễu tiếp xúc.
  - Hoàn thiện tài liệu nghiệm thu hệ thống.

---

## 3. Bảng Phân bổ Thời gian Triển khai (Timeline Estimate)

| Phase | Nội dung công việc chính | Thời lượng ước tính | Module chính |
| :--- | :--- | :---: | :--- |
| **Phase 1** | MVP Ground Contact & Basic Physics | 3 Ngày | Unity C# Physics |
| **Phase 2** | Vibration Simulator & Virtual Telemetry Engine | 4 Ngày | Unity C# Sensor Engine |
| **Phase 3** | FastAPI Backend & ML Quantitative Engine | 4 Ngày | Python / scikit-learn |
| **Phase 4** | Gemini Natural Language AI Integration | 3 Ngày | Python / Gemini API |
| **Phase 5** | Anti-Slop Telemetry HUD & Integration | 5 Ngày | Unity UI / REST Client |
| **Phase 6** | Verification, Edge Cases & Polish | 3 Ngày | Full-stack Review |
| **Tổng** | **Hoàn thành toàn bộ hệ thống Prototype** | **22 Ngày** | **All Modules** |
