from fastapi import APIRouter
from app.api.v1.endpoints import seismic

api_router = APIRouter()
api_router.include_router(seismic.router, prefix="/seismic", tags=["seismic"])
