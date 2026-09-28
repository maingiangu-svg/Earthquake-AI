import os
import math
import joblib
import numpy as np
from typing import Tuple
from sklearn.ensemble import RandomForestRegressor, RandomForestClassifier

from app.core.config import settings
from app.schemas.seismic_schema import (
    ComputedMetrics,
    SensorMetadata,
    QuantitativeAnalysis,
    TelemetryQuality,
    AIExplanation
)

MMI_LABELS = {
    "I": "Not Felt",
    "II": "Weak Shaking",
    "III": "Weak Shaking",
    "IV": "Light Shaking",
    "V": "Moderate Shaking",
    "VI": "Strong Shaking",
    "VII": "Very Strong Shaking",
    "VIII": "Severe Shaking",
    "IX": "Violent Shaking",
    "X": "Extreme Shaking",
    "XI": "Extreme Shaking",
    "XII": "Catastrophic Shaking"
}

class SeismicMLService:
    def __init__(self):
        self.model_dir = settings.MODEL_DIR
        os.makedirs(self.model_dir, exist_ok=True)
        
        self.regressor_path = os.path.join(self.model_dir, "magnitude_rf_regressor.joblib")
        self.classifier_path = os.path.join(self.model_dir, "mmi_rf_classifier.joblib")
        
        self.magnitude_model = None
        self.mmi_model = None
        
        # Self-initialize and train models if not existing
        self._ensure_models_loaded()

    def _ensure_models_loaded(self):
        """Loads models if saved, otherwise generates dataset and trains them."""
        if os.path.exists(self.regressor_path) and os.path.exists(self.classifier_path):
            try:
                self.magnitude_model = joblib.load(self.regressor_path)
                self.mmi_model = joblib.load(self.classifier_path)
                return
            except Exception:
                pass # Re-train on load failure
        
        self._train_and_save_models()

    def _train_and_save_models(self):
        """Generates realistic synthetic seismic dataset and trains scikit-learn models."""
        np.random.seed(42)
        n_samples = 2500
        
        # Generate synthetic physical metrics
        # Magnitude Mw from 2.0 to 8.5
        mw = np.random.uniform(2.0, 8.5, n_samples)
        
        # PGA (g) correlates logarithmically with Mw + random attenuation dispersion
        log_pga = 0.65 * mw - 3.8 + np.random.normal(0, 0.25, n_samples)
        pga = np.clip(10 ** log_pga, 0.001, 2.5)
        
        # RMS is roughly 0.3 to 0.5 of PGA
        rms = pga * (0.35 + np.random.normal(0, 0.05, n_samples))
        
        # Dominant frequency (Hz) - larger earthquakes have lower dominant frequencies
        freq = np.clip(12.0 - 1.2 * mw + np.random.normal(0, 1.0, n_samples), 0.5, 15.0)
        
        # Duration (s) increases with Mw
        duration = np.clip(1.5 * mw + np.random.normal(0, 1.5, n_samples), 1.0, 30.0)
        
        # CAV correlates with PGA * duration
        cav = pga * duration * (0.8 + np.random.normal(0, 0.1, n_samples))
        
        # MMI classification based on Wald et al. (1999) PGA thresholds
        mmi_classes = []
        for p in pga:
            if p < 0.0017: mmi_classes.append("I")
            elif p < 0.005: mmi_classes.append("II")
            elif p < 0.014: mmi_classes.append("III")
            elif p < 0.039: mmi_classes.append("IV")
            elif p < 0.092: mmi_classes.append("V")
            elif p < 0.18: mmi_classes.append("VI")
            elif p < 0.34: mmi_classes.append("VII")
            elif p < 0.65: mmi_classes.append("VIII")
            elif p < 1.24: mmi_classes.append("IX")
            else: mmi_classes.append("X")

        X = np.column_stack([pga, rms, freq, duration, cav])
        
        # 1. Train Magnitude Regressor
        self.magnitude_model = RandomForestRegressor(n_estimators=60, max_depth=12, random_state=42)
        self.magnitude_model.fit(X, mw)
        
        # 2. Train MMI Classifier
        self.mmi_model = RandomForestClassifier(n_estimators=60, max_depth=12, random_state=42)
        self.mmi_model.fit(X, mmi_classes)
        
        # Save models to disk
        joblib.dump(self.magnitude_model, self.regressor_path)
        joblib.dump(self.mmi_model, self.classifier_path)

    def _calculate_structural_risk(self, pga_g: float, freq_hz: float, duration_s: float) -> Tuple[float, str]:
        """
        Calculates Structural Risk Index (0-100%) considering structural resonance 
        between ground vibration frequency and 3-8 story building resonant band (2.0 - 5.0 Hz).
        """
        # Base risk from PGA (PGA >= 0.8g -> 80% base risk)
        pga_factor = min(1.0, pga_g / 0.8) * 70.0
        
        # Resonance factor: Peak resonance in 2.0Hz - 4.5Hz range
        resonance_multiplier = 1.0
        if 2.0 <= freq_hz <= 4.5:
            resonance_multiplier = 1.45
        elif 1.0 <= freq_hz <= 6.0:
            resonance_multiplier = 1.2
            
        # Duration accumulation factor
        duration_factor = min(1.3, 1.0 + (duration_s / 20.0) * 0.3)
        
        risk_index = min(100.0, pga_factor * resonance_multiplier * duration_factor)
        
        if risk_index < 25.0:
            hazard = "LOW"
        elif risk_index < 50.0:
            hazard = "MODERATE"
        elif risk_index < 75.0:
            hazard = "HIGH"
        else:
            hazard = "CRITICAL"
            
        return round(risk_index, 1), hazard

    def predict(
        self, 
        metrics: ComputedMetrics, 
        metadata: SensorMetadata
    ) -> Tuple[QuantitativeAnalysis, TelemetryQuality, AIExplanation]:
        """
        Performs online quantitative prediction in < 20ms using trained scikit-learn models.
        """
        features = np.array([[
            metrics.peak_acceleration_g,
            metrics.rms_acceleration,
            metrics.dominant_frequency_hz,
            metrics.signal_duration_s,
            metrics.cumulative_absolute_velocity
        ]])
        
        # ML Inference
        pred_mw = float(self.magnitude_model.predict(features)[0])
        pred_mmi = str(self.mmi_model.predict(features)[0])
        mmi_label = MMI_LABELS.get(pred_mmi, "Moderate Shaking")
        
        risk_percent, hazard_level = self.calculate_risk = self._calculate_structural_risk(
            metrics.peak_acceleration_g,
            metrics.dominant_frequency_hz,
            metrics.signal_duration_s
        )
        
        quant_res = QuantitativeAnalysis(
            estimated_magnitude_mw=round(pred_mw, 1),
            mmi_intensity_class=pred_mmi,
            mmi_intensity_label=mmi_label,
            peak_ground_acceleration_g=round(metrics.peak_acceleration_g, 3),
            structural_risk_index_percent=risk_percent,
            hazard_level=hazard_level
        )
        
        # Telemetry Quality Assessment
        is_contact_ok = metadata.ground_contact_established and metadata.surface_alignment_deg <= 15.0
        contact_status = "OPTIMAL" if is_contact_ok else ("DEGRADED" if metadata.ground_contact_established else "INVALID")
        
        # Estimate SNR (dB)
        snr_db = round(20.0 * math.log10(max(0.001, metrics.peak_acceleration_g / 0.004)), 1)
        confidence = 0.95 if is_contact_ok else 0.60
        
        quality_res = TelemetryQuality(
            contact_integrity=contact_status,
            signal_noise_ratio_db=snr_db,
            confidence_score=confidence
        )
        
        # Rule-based placeholder AI Explanation (will be replaced by Gemini in Phase 4)
        ai_res = AIExplanation(
            executive_summary=f"Ghi nhận trận động đất độ lớn ước tính {round(pred_mw, 1)} Mw với gia tốc đỉnh PGA = {round(metrics.peak_acceleration_g, 3)}g. Mức độ chấn động cấp {pred_mmi} ({mmi_label}).",
            wave_behavior_analysis=f"Tần số thống trị thu được qua phân tích phổ FFT đạt {round(metrics.dominant_frequency_hz, 2)} Hz. Thời lượng chấn động duy trì vượt ngưỡng là {round(metrics.signal_duration_s, 2)} giây.",
            structural_impact_assessment=f"Chỉ số rủi ro kết cấu công trình đạt {risk_percent}% ({hazard_level}). Tần số {round(metrics.dominant_frequency_hz, 2)} Hz tác động trực tiếp tới các kết cấu xây dựng xung quanh.",
            emergency_advisory="Khuyến cáo duy trì quy trình kiểm tra an toàn kết cấu và sẵn sàng ứng phó với các đợt dư chấn địa chấn tiếp theo."
        )
        
        return quant_res, quality_res, ai_res

ml_service = SeismicMLService()
