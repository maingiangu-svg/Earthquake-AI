# Hướng dẫn Thiết lập Unity Scene (Phase 1: MVP Contact Detection)

Tài liệu này hướng dẫn chi tiết các bước thiết lập Scene 3D trong Unity Editor để vận hành hệ thống phát hiện tiếp xúc mặt đất (**Ground Contact Detection**) theo Phase 1.

---

## 1. Thiết lập Layers & Materials

1. Mở Unity Editor $\rightarrow$ Chọn **Edit** $\rightarrow$ **Project Settings** $\rightarrow$ **Tags and Layers**.
2. Thêm Layer mới tên là `Ground` (ví dụ: Layer 8).
3. (Tùy chọn) Tạo một **Physic Material** (hoặc `Physics Material 3D`) đặt tên là `FrictionlessGround`:
   - Dynamic Friction: `0.6`
   - Static Friction: `0.8`
   - Bounciness: `0.1`

---

## 2. Thiết lập Đối tượng Mặt đất (Ground Environment)

1. Trong cửa sổ **Hierarchy**, tạo đối tượng 3D Mặt đất (ví dụ: **3D Object** $\rightarrow$ **Plane** hoặc **Cube**).
2. Đặt tên đối tượng: `Ground_Surface`.
3. Đặt Layer của đối tượng này thành `Ground`.
4. Trong Component **Box Collider** (hoặc `Mesh Collider`):
   - Đảm bảo **Is Trigger** = `Unchecked (False)`.
   - Gán `Physics Material` vừa tạo vào mục Material.

---

## 3. Thiết lập Đối tượng Cảm biến 3D (Sensor Probe Object)

1. Trong cửa sổ **Hierarchy**, tạo đối tượng đại diện cho Cảm biến (ví dụ: **3D Object** $\rightarrow$ **Cylinder** hoặc Prefab thiết bị 3D).
2. Đặt tên đối tượng: `Seismic_Sensor_Probe`.
3. Đặt vị trí: $X=0, Y=1.0, Z=0$ (nằm lơ lửng phía trên mặt đất một khoảng ngắn).
4. Thêm / Cấu hình Component **Rigidbody**:
   - **Mass**: `5.0` (kg)
   - **Drag**: `0.05`
   - **Angular Drag**: `0.05`
   - **Use Gravity**: `Checked (True)`
   - **Is Kinematic**: `Unchecked (False)`
   - **Collision Detection**: `Continuous` (để tránh hiện tượng xuyên mesh khi rung lắc).
5. Thêm / Cấu hình Component **Capsule Collider** hoặc **Box Collider**:
   - Điều chỉnh Collider vừa vặn với phần chân đế cảm biến.
   - **Is Trigger**: `Unchecked (False)`.

---

## 4. Thiết lập Đèn LED Chỉ báo (Contact Status LED Indicator)

1. Tạo một đối tượng con nằm phía trên cảm biến: Chuột phải vào `Seismic_Sensor_Probe` $\rightarrow$ **3D Object** $\rightarrow$ **Sphere** (hoặc Cylinder nhỏ).
2. Đặt tên: `Status_LED_Indicator`.
3. Thu nhỏ kích thước: Scale $= (0.1, 0.1, 0.1)$.
4. Xóa Component **Collider** trên `Status_LED_Indicator` (không cần thiết va chạm).
5. Tạo một Material mới tên là `Mat_StatusLED` sử dụng Shader `Standard` (bật tích chọn **Emission**), gán vào `Mesh Renderer` của `Status_LED_Indicator`.

---

## 5. Gán Script & Cấu hình Parameters

### A. Cấu hình `GroundContactDetector.cs`
1. Chọn đối tượng `Seismic_Sensor_Probe`.
2. Kéo thả hoặc chọn **Add Component** $\rightarrow$ `GroundContactDetector`.
3. Trong Inspector của `GroundContactDetector`, cấu hình các thông số:
   - **Ground Layer**: Select chọn Layer `Ground`.
   - **Max Tilt Angle Deg**: `15` (độ nghiêng tối đa).
   - **Min Contact Force N**: `5.0` (Lực nén tối thiểu ~ 5 Newton).
   - **Ray Count**: `4` (4 tia dò xung quanh viền).
   - **Probe Radius**: `0.25` (Bán kính chân đế).
   - **Ray Distance**: `0.15` (Độ sâu dò raycast).

### B. Cấu hình `ContactStatusLED.cs`
1. Chọn đối tượng `Status_LED_Indicator`.
2. Kéo thả hoặc chọn **Add Component** $\rightarrow$ `ContactStatusLED`.
3. Trong Inspector của `ContactStatusLED`:
   - Gán ô **Detector**: Kéo đối tượng cha `Seismic_Sensor_Probe` vào đây.
   - **Disconnected Color**: Đỏ Crimson (`#FF3333`).
   - **Partial Contact Color**: Vàng Amber (`#FFC000`).
   - **Established Color**: Xanh Cyan Electric (`#00F0FF`).
   - **Use Emission**: `Checked (True)`.
   - **Emission Intensity**: `2.0`.

---

## 6. Kiểm thử Trạng thái Tiếp xúc (Testing & Verification)

1. Nhấn **Play** trong Unity Editor.
2. Thả tự do cảm biến rơi xuống mặt đất:
   - Khi lơ lửng: Đèn LED chuyển màu **Đỏ** (`DISCONNECTED`).
   - Khi bắt đầu chạm đất nhưng bị nảy hoặc nghiêng: Đèn LED chuyển màu **Vàng** (`PARTIAL_CONTACT`).
   - Khi đứng vững vuông góc trên mặt đất: Đèn LED ngay lập tức chuyển sang màu **Xanh Cyan** (`CONTACT_ESTABLISHED`).
3. Dùng công cụ Transform (Rotate / Move) trong Scene view nghiêng cảm biến quá 15 độ $\rightarrow$ Đèn lập tức đổi sang màu **Vàng**.
