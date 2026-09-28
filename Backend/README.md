# Backend REST API Service: AI Earthquake Analysis System

Tầng Backend REST API được phát triển bằng **Python FastAPI**, kết hợp với **scikit-learn** và **NumPy** để cung cấp dịch vụ phân tính định lượng địa chấn cho Unity 3D Client.

---

## 1. Yêu cầu Môi trường & Thư viện

- **Python**: 3.10+
- **Dependencies**: `fastapi`, `uvicorn`, `pydantic>=2.0`, `scikit-learn`, `numpy`, `scipy`, `joblib`.

Cài đặt thư viện:
```bash
pip install -r requirements.txt
```

---

## 2. Hướng dẫn Chạy Server

Chạy server FastAPI uvicorn trên máy local (mặc định host `0.0.0.0`, port `8000`):

```bash
cd Backend
uvicorn app.main:app --reload --port 8000
```

Giao diện tài liệu tương tác API Swagger UI sẵn sàng tại:
- **Swagger UI**: `http://localhost:8000/docs`
- **ReDoc**: `http://localhost:8000/redoc`

---

## 3. Danh sách Endpoints API

### A. Health Check
- **GET** `/health`
- Trả về trạng thái hoạt động của FastAPI server và kiểm tra tính sẵn sàng của mô hình ML.

### B. Seismic Telemetry Analysis
- **POST** `/api/v1/seismic/analyze`
- Tiếp nhận payload chứa thông số tiếp xúc và dữ liệu gia tốc 3 trục từ Unity.
- Thực hiện dự đoán định lượng địa chấn bằng Random Forest:
  - Moment Magnitude ($M_w$)
  - Modified Mercalli Intensity (Cấp MMI I - XII)
  - Chỉ số Rủi ro Kết cấu Công trình (Structural Risk Index 0% - 100%)
  - Đánh giá chất lượng tín hiệu cảm biến (SNR, Contact Integrity)
