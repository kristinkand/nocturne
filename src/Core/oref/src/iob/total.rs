//! Total IOB calculation from all treatments

use chrono::{DateTime, Utc};
use crate::insulin::calculate_iob_contrib;
use crate::types::{IOBData, Profile, Treatment};
use crate::Result;

/// Calculate total IOB from all treatments at a specific time
///
/// This matches the JavaScript `iobTotal()` function in `lib/iob/total.js`.
///
/// # Arguments
/// * `profile` - User profile with DIA and insulin curve settings
/// * `treatments` - Processed insulin treatments
/// * `time` - Time at which to calculate IOB
///
/// # Returns
/// IOBData containing total IOB, activity, and breakdown by source
pub fn calculate_total_iob(
    profile: &Profile,
    treatments: &[Treatment],
    time: DateTime<Utc>,
) -> Result<IOBData> {
    let now_millis = time.timestamp_millis();

    // Get effective DIA and curve settings
    let dia = profile.effective_dia();
    let curve = profile.curve;
    let peak = profile.effective_peak_time();

    // Calculate DIA in milliseconds
    let dia_ago = now_millis - (dia * 60.0 * 60.0 * 1000.0) as i64;

    let mut total_iob = 0.0;
    let mut total_activity = 0.0;
    let mut basal_iob = 0.0;
    let mut bolus_iob = 0.0;
    let mut net_basal_insulin = 0.0;
    let mut bolus_insulin = 0.0;

    for treatment in treatments {
        let treatment_date = treatment.effective_date();

        // Skip future treatments
        if treatment_date > now_millis {
            continue;
        }

        // Skip treatments older than DIA
        if treatment_date < dia_ago {
            continue;
        }

        // Get insulin amount
        let insulin = match treatment.insulin {
            Some(i) => i,
            None => continue,
        };

        // Skip zero insulin
        if insulin.abs() < 0.0001 {
            continue;
        }

        // Calculate minutes since treatment
        let mins_ago = (now_millis - treatment_date) as f64 / 60000.0;

        // Calculate IOB contribution
        let contrib = calculate_iob_contrib(
            insulin.abs(),
            mins_ago,
            curve,
            dia,
            peak,
        );

        // Apply sign for negative insulin (suspended basal)
        let sign = if insulin < 0.0 { -1.0 } else { 1.0 };
        let iob_contrib = contrib.iob_contrib * sign;
        let activity_contrib = contrib.activity_contrib * sign;

        total_iob += iob_contrib;
        total_activity += activity_contrib;

        // Categorize by source
        // Small doses (< 0.1 U) are considered basal adjustments
        // Larger doses are considered boluses
        if insulin.abs() < 0.1 {
            basal_iob += iob_contrib;
            net_basal_insulin += insulin;
        } else {
            bolus_iob += iob_contrib;
            bolus_insulin += insulin;
        }
    }

    Ok(IOBData {
        iob: total_iob,
        activity: total_activity,
        basal_iob,
        bolus_iob,
        net_basal_insulin,
        bolus_insulin,
        time,
        iob_with_zero_temp: None,
        last_bolus_time: None,
        last_temp: None,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::insulin::InsulinCurve;

    fn make_profile(dia: f64, curve: InsulinCurve) -> Profile {
        Profile {
            dia,
            curve,
            ..Default::default()
        }
    }

    #[test]
    fn test_single_bolus_at_time_zero() {
        let now = Utc::now();
        let profile = make_profile(3.0, InsulinCurve::Bilinear);

        let treatments = vec![Treatment::bolus(2.0, now)];

        let iob = calculate_total_iob(&profile, &treatments, now).unwrap();

        // IOB should equal the bolus amount
        assert!((iob.iob - 2.0).abs() < 0.01);
        assert!(iob.bolus_iob > 1.9);
        assert!(iob.basal_iob.abs() < 0.01);
    }

    #[test]
    fn test_single_bolus_after_dia() {
        let now = Utc::now();
        let bolus_time = now - chrono::Duration::hours(4);
        let profile = make_profile(3.0, InsulinCurve::Bilinear);

        let treatments = vec![Treatment::bolus(2.0, bolus_time)];

        let iob = calculate_total_iob(&profile, &treatments, now).unwrap();

        // IOB should be zero after DIA
        assert!(iob.iob.abs() < 0.01);
    }

    #[test]
    fn test_multiple_boluses() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        let treatments = vec![
            Treatment::bolus(1.0, now),
            Treatment::bolus(1.0, now - chrono::Duration::hours(1)),
        ];

        let iob = calculate_total_iob(&profile, &treatments, now).unwrap();

        // Should have IOB from both boluses
        // First bolus: 1.0 U
        // Second bolus: < 1.0 U (some absorbed)
        assert!(iob.iob > 1.0);
        assert!(iob.iob < 2.0);
    }

    #[test]
    fn test_negative_insulin_reduces_iob() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        let treatments = vec![
            Treatment::bolus(2.0, now),
            Treatment {
                insulin: Some(-0.5),
                date: now.timestamp_millis(),
                ..Default::default()
            },
        ];

        let iob = calculate_total_iob(&profile, &treatments, now).unwrap();

        // Net IOB should be 1.5
        assert!((iob.iob - 1.5).abs() < 0.01);
    }

    #[test]
    fn test_activity_is_positive() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        // Bolus from 1 hour ago
        let treatments = vec![
            Treatment::bolus(1.0, now - chrono::Duration::hours(1)),
        ];

        let iob = calculate_total_iob(&profile, &treatments, now).unwrap();

        // Activity should be positive (insulin is being absorbed)
        assert!(iob.activity > 0.0);
    }

    #[test]
    fn test_ultra_rapid_faster_absorption() {
        let now = Utc::now();
        let bolus_time = now - chrono::Duration::hours(2);

        let rapid_profile = make_profile(5.0, InsulinCurve::RapidActing);
        let ultra_profile = make_profile(5.0, InsulinCurve::UltraRapid);

        let treatments = vec![Treatment::bolus(1.0, bolus_time)];

        let rapid_iob = calculate_total_iob(&rapid_profile, &treatments, now).unwrap();
        let ultra_iob = calculate_total_iob(&ultra_profile, &treatments, now).unwrap();

        // Ultra-rapid should have less IOB remaining
        assert!(ultra_iob.iob < rapid_iob.iob);
    }

    #[test]
    fn test_small_doses_categorized_as_basal() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        let treatments = vec![
            Treatment {
                insulin: Some(0.05), // Small basal adjustment
                date: now.timestamp_millis(),
                ..Default::default()
            },
        ];

        let iob = calculate_total_iob(&profile, &treatments, now).unwrap();

        assert!(iob.basal_iob > 0.04);
        assert!(iob.bolus_iob.abs() < 0.01);
    }
}
