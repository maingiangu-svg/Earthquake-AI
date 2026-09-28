import time
import uuid
from datetime import datetime, timezone
from fastapi import APIRouter, HTTPException, status

from app.schemas.seismic_schema import (
    SeismicTelemetryRequest,
    SeismicAnalysisResponse,
    ErrorResponse
)
from app.services.ml_service import ml_service

router = APIRouter()

@router.post(
    "/analyze",
    response_model=SeismicAnalysisResponse,
    status_code=status.HTTP_200_OK,
    summary="Phân tích dữ liệu địa chấn từ Unity",
    description="Tiếp nhận telemetry gia tốc 3 trục từ cảm biến ảo Unity, tính toán dự đoán định lượng ML (Mw, MMI, Structural Risk) và trả về kết quả phân tích.",
    responses={
        400: {"model": ErrorResponse, "description": "Dữ liệu payload không hợp lệ"},
        500: {"model": ErrorResponse, "description": "Lỗi nội bộ server"}
    }
)
async def analyze_seismic_telemetry(request: SeismicTelemetryRequest) -> SeismicAnalysisResponse:
    start_time = time.perf_counter()
    
    try:
        # 1. Validation kiểm tra tính hợp lệ cơ bản
        if request.computed_metrics.peak_acceleration_g < 0:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Peak Ground Acceleration (PGA) không thể có giá trị âm."
            )
            
        # 2. Gọi ML Service xử lý suy luận định lượng (Quantitative ML Prediction)
        quant_analysis, quality_assessment, ai_explanation = ml_service.predict(
            metrics=request.computed_metrics,
            metadata=request.sensor_metadata
        )
        
        # 3. Tính toán thời gian thực thi (Execution Time in ms)
        execution_time_ms = round((time.perf_counter() - start_time) * 1000.0, 2)
        
        # Determine status string
        res_status = "SUCCESS"
        if not request.sensor_metadata.ground_contact_established:
            res_status = "WARNING_CONTACT_DEGRADED"
            
        # 4. Đóng gói Response
        response = SeismicAnalysisResponse(
            analysis_id=str(uuid.uuid4()),
            session_id=request.session_id,
            timestamp=datetime.now(timezone.utc).isoformat(),
            status=res_status,
            execution_time_ms=execution_time_ms,
            quantitative_analysis=quant_analysis,
            ai_explanation=ai_explanation,
            telemetry_quality=quality_assessment
        )
        
        return response
        
    except HTTPException as he:
        raise he
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Lỗi khi xử lý phân tích địa chấn: {str(e)}"
        )
