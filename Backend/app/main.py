import time
from datetime import datetime, timezone
from fastapi import FastAPI, Request, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError

from app.core.config import settings
from app.api.v1.api import api_router
from app.services.ml_service import ml_service

app = FastAPI(
    title=settings.PROJECT_NAME,
    version=settings.VERSION,
    openapi_url=f"{settings.API_V1_STR}/openapi.json",
    description="REST API Gateway cho AI Earthquake Analysis System. Xử lý telemetry từ Unity 3D và suy luận định lượng địa chấn."
)

# Set up CORS Middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Custom Validation Error Handler matching ErrorResponse Schema
@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content={
            "error_code": "UNPROCESSABLE_ENTITY",
            "message": "Dữ liệu JSON payload từ Unity không đúng định dạng Schema quy định.",
            "details": {"errors": exc.errors()},
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    )

# Health Check Endpoint
@app.get(
    "/health",
    tags=["healthcheck"],
    summary="Kiểm tra sức khỏe Backend Service",
    description="Endpoint trả về trạng thái của FastAPI Server và khả năng sẵn sàng của ML Quantitative Engine."
)
async def health_check():
    ml_ready = (ml_service.magnitude_model is not None) and (ml_service.mmi_model is not None)
    return {
        "status": "HEALTHY",
        "service": settings.PROJECT_NAME,
        "version": settings.VERSION,
        "ml_engine_ready": ml_ready,
        "timestamp": datetime.now(timezone.utc).isoformat()
    }

# Include API v1 Router
app.include_router(api_router, prefix=settings.API_V1_STR)

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
