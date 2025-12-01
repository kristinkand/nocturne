//! Glucose prediction calculations

use crate::types::{GlucoseStatus, IOBData, MealData, Profile};

/// Predict future glucose values
pub fn predict_glucose(
    glucose_status: &GlucoseStatus,
    iob_data: &IOBData,
    profile: &Profile,
) -> Vec<f64> {
    let mut predictions = Vec::new();
    let bg = glucose_status.glucose;
    let sens = profile.sens;

    // Simple prediction: BG + delta extrapolation - IOB effect
    for i in 0..48 {
        // 5-minute intervals out to 4 hours
        let minutes = i as f64 * 5.0;

        // Decay IOB over time (simplified)
        let iob_factor = (-minutes / 60.0).exp();
        let predicted_iob_effect = iob_data.iob * iob_factor * sens;

        // Extrapolate delta (with decay)
        let delta_factor = (-minutes / 30.0).exp();
        let predicted_delta_effect = glucose_status.delta * (minutes / 5.0) * delta_factor;

        let predicted = bg + predicted_delta_effect - predicted_iob_effect;
        predictions.push(predicted.max(39.0)); // Floor at 39
    }

    predictions
}

/// Calculate eventual BG (where BG is heading)
pub fn calculate_eventual_bg(
    glucose_status: &GlucoseStatus,
    iob_data: &IOBData,
    profile: &Profile,
) -> f64 {
    let bg = glucose_status.glucose;
    let sens = profile.sens;

    // Eventual BG = current BG - (IOB * sens)
    // This assumes all current IOB will eventually affect BG
    let iob_effect = iob_data.iob * sens;

    // Also account for current trend
    let trend_effect = glucose_status.delta * 12.0; // ~1 hour extrapolation

    (bg + trend_effect - iob_effect).max(0.0)
}

/// Calculate expected delta (how fast BG should be changing)
pub fn calculate_expected_delta(
    target_bg: f64,
    eventual_bg: f64,
    bgi: f64,
) -> f64 {
    // We expect BG to rise/fall at the rate of BGI,
    // adjusted by the rate at which BG would need to rise/fall
    // to get eventualBG to target over 2 hours
    let five_min_blocks = (2.0 * 60.0) / 5.0; // 24 blocks
    let target_delta = target_bg - eventual_bg;

    (bgi + (target_delta / five_min_blocks) * 10.0).round() / 10.0
}

/// Calculate Blood Glucose Impact (BGI)
pub fn calculate_bgi(
    activity: f64,
    sens: f64,
) -> f64 {
    // BGI = -(activity * sens * 5)
    // This is how much BG is expected to change per 5 minutes
    // based on current insulin activity
    (-activity * sens * 5.0 * 100.0).round() / 100.0
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bgi_calculation() {
        // Activity of 0.01 U/min with sens of 50 mg/dL/U
        let bgi = calculate_bgi(0.01, 50.0);

        // Expected: -0.01 * 50 * 5 = -2.5
        assert!((bgi - (-2.5)).abs() < 0.1);
    }

    #[test]
    fn test_eventual_bg() {
        let glucose_status = GlucoseStatus {
            glucose: 150.0,
            delta: 0.0,
            short_avgdelta: 0.0,
            long_avgdelta: 0.0,
            date: 0,
            noise: None,
        };

        let iob_data = IOBData {
            iob: 2.0,
            activity: 0.01,
            ..Default::default()
        };

        let profile = Profile {
            sens: 50.0,
            ..Default::default()
        };

        let eventual = calculate_eventual_bg(&glucose_status, &iob_data, &profile);

        // Expected: 150 - (2 * 50) = 50
        assert!((eventual - 50.0).abs() < 1.0);
    }

    #[test]
    fn test_prediction_decreases_with_iob() {
        let glucose_status = GlucoseStatus::new(150.0, 0.0);

        let iob_data = IOBData {
            iob: 2.0,
            activity: 0.01,
            ..Default::default()
        };

        let profile = Profile {
            sens: 50.0,
            ..Default::default()
        };

        let predictions = predict_glucose(&glucose_status, &iob_data, &profile);

        // With 2U IOB and sens of 50, first prediction accounts for IOB effect
        // predictions[0] = 150 - (2 * 50) = 50 mg/dL
        assert!(predictions[0] < 150.0); // Should be lower due to IOB
        assert!(predictions[0] > 0.0);   // Should still be positive

        // Later predictions should show recovery as IOB decays
        // predictions[10] is at 50 minutes, IOB has decayed significantly
    }

    #[test]
    fn test_expected_delta() {
        // Target 100, eventual 150, no BGI
        let expected = calculate_expected_delta(100.0, 150.0, 0.0);

        // Should be negative (need to come down)
        assert!(expected < 0.0);
    }
}
