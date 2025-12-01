//! Basal rate rounding for different pump models

use crate::types::Profile;

/// Round basal rate according to pump model capabilities
///
/// Different pump models support different precision levels:
/// - Older Medtronic (5xx series): 0.05 U/hr increments
/// - Newer Medtronic (7xx series): 0.025 U/hr increments
/// - Omnipod: 0.05 U/hr increments
pub fn round_basal(rate: f64, profile: &Profile) -> f64 {
    let increment = get_pump_increment(profile);
    round_to_increment(rate, increment)
}

/// Round a value to the nearest increment
pub fn round_value(value: f64, digits: u32) -> f64 {
    let scale = 10_f64.powi(digits as i32);
    (value * scale).round() / scale
}

/// Get the basal increment for a pump model
fn get_pump_increment(profile: &Profile) -> f64 {
    match profile.model.as_deref() {
        // Newer Medtronic pumps support 0.025 increments
        Some(model) if is_newer_medtronic(model) => 0.025,
        // Most other pumps use 0.05 increments
        _ => 0.05,
    }
}

/// Check if this is a newer Medtronic pump model
fn is_newer_medtronic(model: &str) -> bool {
    // 5xx and 7xx series numbers that support 0.025 increments
    let newer_models = [
        "523", "723", "554", "754",
        "530G", "630G", "670G", "770G", "780G",
    ];

    newer_models.iter().any(|m| model.contains(m))
}

/// Round a rate to the specified increment
fn round_to_increment(rate: f64, increment: f64) -> f64 {
    // Special rounding rules for high rates (>10 U/hr)
    if rate > 10.0 {
        // Round to 0.1 for high rates
        (rate * 10.0).round() / 10.0
    } else {
        (rate / increment).round() * increment
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_round_basal_default() {
        let profile = Profile::default();

        // Should round to 0.05
        assert!((round_basal(0.83, &profile) - 0.85).abs() < 0.001);
        assert!((round_basal(0.86, &profile) - 0.85).abs() < 0.001);
        assert!((round_basal(0.025, &profile) - 0.05).abs() < 0.001);
    }

    #[test]
    fn test_round_basal_newer_medtronic() {
        let profile = Profile {
            model: Some("554".to_string()),
            ..Default::default()
        };

        // Should round to 0.025 increments
        assert!((round_basal(0.025, &profile) - 0.025).abs() < 0.001);
        assert!((round_basal(0.030, &profile) - 0.025).abs() < 0.001);
        assert!((round_basal(0.040, &profile) - 0.05).abs() < 0.001);
    }

    #[test]
    fn test_round_basal_high_rate() {
        let profile = Profile::default();

        // High rates (>10) round to 0.1
        assert!((round_basal(10.83, &profile) - 10.8).abs() < 0.001);
        assert!((round_basal(10.86, &profile) - 10.9).abs() < 0.001);
    }

    #[test]
    fn test_round_value() {
        assert!((round_value(1.2345, 2) - 1.23).abs() < 0.001);
        assert!((round_value(1.2355, 2) - 1.24).abs() < 0.001);
    }
}
