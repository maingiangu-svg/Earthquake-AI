from typing import List, Optional
from pydantic import BaseModel, Field
from datetime import datetime

# ==========================================
# 1. REQUEST SCHEMAS (Unity -> FastAPI)
# ==========================================

class SensorMetadata(BaseModel):
    device_id: str = Field(default="SEISMIC_PROBE_ALPHA_01", description="Unique device identifier")
    ground_contact_established: bool = Field(default=True, description="Ground contact status flag")
    contact_force_n: float = Field(default=0.0, description="Contact normal force in Newtons")
    surface_alignment_deg: float = Field(default=0.0, description="Surface tilt angle in degrees")
    surface_material: str = Field(default="Compacted_Soil_Bed", description="Soil or ground material type")

class RawTelemetry(BaseModel):
    sampling_rate_hz: int = Field(default=100, description="Sampling rate in Hz")
    sample_count: int = Field(default=500, description="Number of samples in buffer")
    acceleration_x: List[float] = Field(default_factory=list, description="Tri-axial X acceleration time-series in g")
    acceleration_y: List[float] = Field(default_factory=list, description="Tri-axial Y acceleration time-series in g")
    acceleration_z: List[float] = Field(default_factory=list, description="Tri-axial Z acceleration time-series in g")

class ComputedMetrics(BaseModel):
    peak_acceleration_g: float = Field(..., description="Peak Ground Acceleration (PGA) in g")
    rms_acceleration: float = Field(..., description="Root Mean Square (RMS) acceleration in g")
    dominant_frequency_hz: float = Field(..., description="Dominant frequency in Hz from FFT")
    signal_duration_s: float = Field(..., description="Duration of vibration exceeding noise floor in seconds")
    cumulative_absolute_velocity: float = Field(..., description="Cumulative Absolute Velocity (CAV)")

class SeismicTelemetryRequest(BaseModel):
    session_id: str = Field(..., description="Unique UUID for the telemetry session")
    timestamp: str = Field(..., description="ISO-8601 UTC timestamp")
    sensor_metadata: SensorMetadata
    raw_telemetry: RawTelemetry
    computed_metrics: ComputedMetrics

# ==========================================
# 2. RESPONSE SCHEMAS (FastAPI -> Unity)
# ==========================================

class QuantitativeAnalysis(BaseModel):
    estimated_magnitude_mw: float = Field(..., description="Estimated Moment Magnitude Scale (Mw)")
    mmi_intensity_class: str = Field(..., description="Modified Mercalli Intensity Class (Roman numerals I - XII)")
    mmi_intensity_label: str = Field(..., description="Descriptive MMI label (e.g. 'Strong Shaking')")
    peak_ground_acceleration_g: float = Field(..., description="PGA in g-force")
    structural_risk_index_percent: float = Field(..., description="Structural Damage Risk Index percentage (0 - 100%)")
    hazard_level: str = Field(..., description="Hazard Risk Level: LOW, MODERATE, HIGH, CRITICAL")

class AIExplanation(BaseModel):
    executive_summary: str = Field(..., description="High-level summary of the seismic event")
    wave_behavior_analysis: str = Field(..., description="Detailed wave physics and spectral interpretation")
    structural_impact_assessment: str = Field(..., description="Structural dynamics & building damage analysis")
    emergency_advisory: str = Field(..., description="Actionable safety recommendations")

class TelemetryQuality(BaseModel):
    contact_integrity: str = Field(..., description="Contact status: OPTIMAL, DEGRADED, INVALID")
    signal_noise_ratio_db: float = Field(..., description="Estimated Signal-to-Noise Ratio (dB)")
    confidence_score: float = Field(..., description="ML prediction confidence score (0.0 to 1.0)")

class SeismicAnalysisResponse(BaseModel):
    analysis_id: str = Field(..., description="Unique UUID for this analysis response")
    session_id: str = Field(..., description="Session ID matching the request")
    timestamp: str = Field(..., description="ISO-8601 UTC timestamp of analysis completion")
    status: str = Field(default="SUCCESS", description="Response status: SUCCESS, WARNING_CONTACT_DEGRADED, ERROR")
    execution_time_ms: float = Field(..., description="Backend execution time in milliseconds")
    quantitative_analysis: QuantitativeAnalysis
    ai_explanation: AIExplanation
    telemetry_quality: TelemetryQuality

class ErrorResponse(BaseModel):
    error_code: str = Field(..., description="Machine-readable error code")
    message: str = Field(..., description="Human-readable error description")
    details: Optional[dict] = Field(default=None, description="Additional error context")
    timestamp: str = Field(..., description="ISO-8601 UTC timestamp")
