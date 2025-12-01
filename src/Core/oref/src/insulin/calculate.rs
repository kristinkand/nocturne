//! Insulin activity calculations
//!
//! Implements the bilinear and exponential insulin action curves
//! to calculate IOB contribution and activity from a single insulin dose.

use crate::types::IOBContrib;
use super::InsulinCurve;

/// Calculate IOB contribution from an insulin dose
///
/// # Arguments
/// * `insulin` - Insulin amount in units
/// * `mins_ago` - Minutes since the insulin was delivered
/// * `curve` - Type of insulin curve to use
/// * `dia` - Duration of insulin action in hours
/// * `peak` - Peak time in minutes (only used for exponential curves)
///
/// # Returns
/// IOBContrib containing iob_contrib and activity_contrib
pub fn calculate_iob_contrib(
    insulin: f64,
    mins_ago: f64,
    curve: InsulinCurve,
    dia: f64,
    peak: u32,
) -> IOBContrib {
    if insulin <= 0.0 || mins_ago < 0.0 {
        return IOBContrib::zero();
    }

    match curve {
        InsulinCurve::Bilinear => BilinearCurve::calculate(insulin, mins_ago, dia),
        InsulinCurve::RapidActing | InsulinCurve::UltraRapid => {
            ExponentialCurve::calculate(insulin, mins_ago, dia, peak)
        }
    }
}

/// Bilinear insulin action curve
///
/// This is the legacy model using a simple triangular shape:
/// - Ramps up linearly to peak at 75 minutes (scaled by DIA ratio)
/// - Ramps down linearly to 0 at 180 minutes (scaled by DIA ratio)
pub struct BilinearCurve;

impl BilinearCurve {
    /// Default DIA for the bilinear model (hours)
    const DEFAULT_DIA: f64 = 3.0;
    /// Peak time (minutes)
    const PEAK: f64 = 75.0;
    /// End time (minutes)
    const END: f64 = 180.0;

    /// Calculate IOB contribution using bilinear model
    pub fn calculate(insulin: f64, mins_ago: f64, dia: f64) -> IOBContrib {
        // Enforce minimum DIA of 3 hours
        let dia = dia.max(3.0);

        // Scale minsAgo by the ratio of the default DIA / the user's DIA
        let time_scalar = Self::DEFAULT_DIA / dia;
        let scaled_mins_ago = time_scalar * mins_ago;

        let mut activity_contrib = 0.0;
        let mut iob_contrib = 0.0;

        // Calculate percent of insulin activity at peak
        // Based on area of triangle: (length * height) / 2 = 1
        // Therefore height (activityPeak) = 2 / length (dia in minutes)
        let activity_peak = 2.0 / (dia * 60.0);
        let slope_up = activity_peak / Self::PEAK;
        let slope_down = -activity_peak / (Self::END - Self::PEAK);

        if scaled_mins_ago < Self::PEAK {
            // Before peak: linear rise
            activity_contrib = insulin * (slope_up * scaled_mins_ago);

            // IOB calculation using quadratic coefficients
            // Coefficients were estimated based on 5 minute increments
            let x1 = (scaled_mins_ago / 5.0) + 1.0;
            iob_contrib = insulin * ((-0.001852 * x1 * x1) + (0.001852 * x1) + 1.0);
        } else if scaled_mins_ago < Self::END {
            // After peak: linear decline
            let mins_past_peak = scaled_mins_ago - Self::PEAK;
            activity_contrib = insulin * (activity_peak + (slope_down * mins_past_peak));

            // IOB calculation using quadratic coefficients
            let x2 = (scaled_mins_ago - Self::PEAK) / 5.0;
            iob_contrib = insulin * ((0.001323 * x2 * x2) + (-0.054233 * x2) + 0.555560);
        }
        // After END: both are 0 (initialized values)

        IOBContrib::new(iob_contrib, activity_contrib)
    }
}

/// Exponential insulin action curve
///
/// Uses a more physiologically accurate exponential model based on:
/// https://github.com/LoopKit/Loop/issues/388#issuecomment-317938473
///
/// This model takes both DIA and peak time as parameters.
pub struct ExponentialCurve;

impl ExponentialCurve {
    /// Calculate IOB contribution using exponential model
    ///
    /// # Arguments
    /// * `insulin` - Insulin amount in units
    /// * `mins_ago` - Minutes since dose
    /// * `dia` - Duration of insulin action in hours
    /// * `peak` - Peak activity time in minutes
    pub fn calculate(insulin: f64, mins_ago: f64, dia: f64, peak: u32) -> IOBContrib {
        // Enforce minimum DIA of 5 hours for exponential curves
        let dia = dia.max(5.0);
        let end = dia * 60.0; // End time in minutes
        let peak = peak as f64;

        if mins_ago >= end {
            return IOBContrib::zero();
        }

        // Formula source: https://github.com/LoopKit/Loop/issues/388#issuecomment-317938473
        // Original variable mapping:
        //   td = end
        //   tp = peak
        //   t  = mins_ago

        // Time constant of exponential decay
        let tau = peak * (1.0 - peak / end) / (1.0 - 2.0 * peak / end);

        // Rise time factor
        let a = 2.0 * tau / end;

        // Auxiliary scale factor
        let s = 1.0 / (1.0 - a + (1.0 + a) * (-end / tau).exp());

        // Activity: how fast insulin is being used right now
        let activity_contrib = insulin * (s / (tau * tau)) * mins_ago * (1.0 - mins_ago / end) * (-mins_ago / tau).exp();

        // IOB: how much insulin remains active
        let inner = (mins_ago * mins_ago / (tau * end * (1.0 - a)) - mins_ago / tau - 1.0) * (-mins_ago / tau).exp() + 1.0;
        let iob_contrib = insulin * (1.0 - s * (1.0 - a) * inner);

        IOBContrib::new(iob_contrib, activity_contrib)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use approx::assert_relative_eq;

    #[test]
    fn test_bilinear_at_zero() {
        let contrib = BilinearCurve::calculate(1.0, 0.0, 3.0);
        assert_relative_eq!(contrib.iob_contrib, 1.0, epsilon = 0.001);
        assert_relative_eq!(contrib.activity_contrib, 0.0, epsilon = 0.001);
    }

    #[test]
    fn test_bilinear_after_dia() {
        let contrib = BilinearCurve::calculate(1.0, 180.0, 3.0);
        assert_relative_eq!(contrib.iob_contrib, 0.0, epsilon = 0.001);
        assert_relative_eq!(contrib.activity_contrib, 0.0, epsilon = 0.001);
    }

    #[test]
    fn test_bilinear_at_peak() {
        // At peak (75 min with 3h DIA), activity should be maximum
        let contrib = BilinearCurve::calculate(1.0, 75.0, 3.0);
        // Activity peak = 2 / (3 * 60) = 0.0111...
        assert!(contrib.activity_contrib > 0.01);
        // IOB should be around 0.56 based on the formula
        assert!(contrib.iob_contrib > 0.5 && contrib.iob_contrib < 0.6);
    }

    #[test]
    fn test_exponential_at_zero() {
        let contrib = ExponentialCurve::calculate(1.0, 0.0, 5.0, 75);
        // At t=0, all insulin is IOB
        assert!(contrib.iob_contrib > 0.99);
        // Activity starts at 0
        assert!(contrib.activity_contrib < 0.001);
    }

    #[test]
    fn test_exponential_after_dia() {
        let contrib = ExponentialCurve::calculate(1.0, 300.0, 5.0, 75);
        // After 5h DIA, IOB should be essentially 0
        assert_relative_eq!(contrib.iob_contrib, 0.0, epsilon = 0.001);
        assert_relative_eq!(contrib.activity_contrib, 0.0, epsilon = 0.001);
    }

    #[test]
    fn test_exponential_at_peak() {
        // Activity should be maximum around peak time
        let at_30 = ExponentialCurve::calculate(1.0, 30.0, 5.0, 75);
        let at_75 = ExponentialCurve::calculate(1.0, 75.0, 5.0, 75);
        let at_120 = ExponentialCurve::calculate(1.0, 120.0, 5.0, 75);

        // Activity at peak should be higher than before and after
        assert!(at_75.activity_contrib > at_30.activity_contrib);
        assert!(at_75.activity_contrib > at_120.activity_contrib);
    }

    #[test]
    fn test_ultra_rapid_faster_decay() {
        // Ultra-rapid (peak 55) should decay faster than rapid-acting (peak 75)
        let rapid = ExponentialCurve::calculate(1.0, 120.0, 5.0, 75);
        let ultra = ExponentialCurve::calculate(1.0, 120.0, 5.0, 55);

        // Ultra-rapid should have less IOB remaining at 120 min
        assert!(ultra.iob_contrib < rapid.iob_contrib);
    }

    #[test]
    fn test_calculate_iob_contrib_dispatch() {
        // Test that the main function correctly dispatches to the right curve
        let bilinear = calculate_iob_contrib(1.0, 60.0, InsulinCurve::Bilinear, 3.0, 75);
        let rapid = calculate_iob_contrib(1.0, 60.0, InsulinCurve::RapidActing, 5.0, 75);
        let ultra = calculate_iob_contrib(1.0, 60.0, InsulinCurve::UltraRapid, 5.0, 55);

        // All should have IOB between 0 and 1
        assert!(bilinear.iob_contrib > 0.0 && bilinear.iob_contrib < 1.0);
        assert!(rapid.iob_contrib > 0.0 && rapid.iob_contrib < 1.0);
        assert!(ultra.iob_contrib > 0.0 && ultra.iob_contrib < 1.0);
    }

    #[test]
    fn test_zero_insulin() {
        let contrib = calculate_iob_contrib(0.0, 60.0, InsulinCurve::RapidActing, 5.0, 75);
        assert_eq!(contrib.iob_contrib, 0.0);
        assert_eq!(contrib.activity_contrib, 0.0);
    }

    #[test]
    fn test_negative_time() {
        // Negative time should return zero (future dose)
        let contrib = calculate_iob_contrib(1.0, -10.0, InsulinCurve::RapidActing, 5.0, 75);
        assert_eq!(contrib.iob_contrib, 0.0);
        assert_eq!(contrib.activity_contrib, 0.0);
    }

    #[test]
    fn test_iob_matches_js_implementation() {
        // Test values from the JS implementation tests
        // Bolus of 2 units with 3h DIA (bilinear)
        let at_0 = BilinearCurve::calculate(2.0, 0.0, 3.0);
        assert_relative_eq!(at_0.iob_contrib, 2.0, epsilon = 0.01);

        // After 1 hour with bilinear, IOB should be less than 1.45 (from JS test)
        let at_60 = BilinearCurve::calculate(2.0, 60.0, 3.0);
        assert!(at_60.iob_contrib < 1.45);

        // Activity at 1 hour should be positive (insulin is being absorbed)
        assert!(at_60.activity_contrib > 0.0);
    }
}
