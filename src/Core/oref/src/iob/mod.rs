//! Insulin on Board (IOB) calculations
//!
//! This module calculates the total active insulin from all treatments
//! (boluses and temp basals) over the duration of insulin action.

mod history;
mod total;

pub use history::find_insulin_treatments;
pub use total::calculate_total_iob;

use chrono::{DateTime, Utc};
use crate::types::{IOBData, Profile, Treatment, TempBasalState};
use crate::Result;

/// Generate IOB array from treatment history
///
/// This is the main entry point for IOB calculations, matching the
/// JavaScript `generate()` function in `lib/iob/index.js`.
///
/// # Arguments
/// * `profile` - User profile with DIA and insulin curve settings
/// * `history` - Pump history events
/// * `clock` - Current time
/// * `current_iob_only` - If true, only calculate current IOB (faster for COB calculation)
///
/// # Returns
/// Array of IOB data points, one per 5-minute interval out to 4 hours
pub fn calculate(
    profile: &Profile,
    history: &[Treatment],
    clock: DateTime<Utc>,
    current_iob_only: bool,
) -> Result<Vec<IOBData>> {
    // Find all insulin treatments
    let treatments = find_insulin_treatments(profile, history, clock, 0)?;

    // Also calculate with zero temp for comparison
    let treatments_with_zero_temp = if !current_iob_only {
        find_insulin_treatments(profile, history, clock, 240)?
    } else {
        vec![]
    };

    let mut iob_array = Vec::new();

    // Track last bolus time and last temp
    let mut last_bolus_time: i64 = 0;
    let mut last_temp = TempBasalState::default();

    for treatment in &treatments {
        if let Some(insulin) = treatment.insulin {
            if insulin > 0.0 {
                if let Some(ref started_at) = treatment.started_at {
                    if let Ok(dt) = DateTime::parse_from_rfc3339(started_at) {
                        last_bolus_time = last_bolus_time.max(dt.timestamp_millis());
                    }
                }
            }
        }

        if treatment.rate.is_some() && treatment.duration.is_some() {
            let treatment_date = treatment.effective_date();
            if treatment_date > last_temp.date {
                last_temp = TempBasalState {
                    date: treatment_date,
                    duration: treatment.duration.unwrap_or(0.0),
                    rate: treatment.rate,
                };
            }
        }
    }

    // Determine how many 5-minute intervals to calculate
    let i_stop = if current_iob_only {
        1 // Only calculate current IOB
    } else {
        4 * 60 / 5 // 4 hours of 5-minute intervals = 48
    };

    for i in (0..i_stop).step_by(1) {
        let t = clock + chrono::Duration::minutes((i * 5) as i64);

        let iob = calculate_total_iob(profile, &treatments, t)?;

        let mut iob_data = iob;

        // Calculate IOB with zero temp if we have that data
        if !treatments_with_zero_temp.is_empty() {
            let iob_with_zero = calculate_total_iob(profile, &treatments_with_zero_temp, t)?;
            iob_data.iob_with_zero_temp = Some(Box::new(iob_with_zero));
        }

        iob_array.push(iob_data.rounded());
    }

    // Add last bolus time and last temp to first element
    if !iob_array.is_empty() {
        iob_array[0].last_bolus_time = if last_bolus_time > 0 { Some(last_bolus_time) } else { None };
        iob_array[0].last_temp = if last_temp.date > 0 { Some(last_temp) } else { None };
    }

    Ok(iob_array)
}

/// Calculate IOB for current time only (faster, used for COB calculations)
pub fn calculate_current(
    profile: &Profile,
    history: &[Treatment],
    clock: DateTime<Utc>,
) -> Result<IOBData> {
    let iob_array = calculate(profile, history, clock, true)?;
    Ok(iob_array.into_iter().next().unwrap_or_else(|| IOBData::zero(clock)))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::insulin::InsulinCurve;
    use chrono::TimeZone;

    fn make_profile(dia: f64, curve: InsulinCurve) -> Profile {
        Profile {
            dia,
            curve,
            current_basal: 1.0,
            ..Default::default()
        }
    }

    #[test]
    fn test_calculate_iob_right_after_bolus() {
        let now = Utc::now();
        let profile = make_profile(3.0, InsulinCurve::Bilinear);

        let treatments = vec![Treatment::bolus(2.0, now)];

        let iob = calculate(&profile, &treatments, now, true).unwrap();
        assert!(!iob.is_empty());
        assert!((iob[0].iob - 2.0).abs() < 0.01);
    }

    #[test]
    fn test_calculate_iob_after_dia() {
        let now = Utc::now();
        let bolus_time = now - chrono::Duration::hours(3);
        let profile = make_profile(3.0, InsulinCurve::Bilinear);

        let treatments = vec![Treatment::bolus(2.0, bolus_time)];

        let iob = calculate(&profile, &treatments, now, true).unwrap();
        assert!(!iob.is_empty());
        assert!(iob[0].iob.abs() < 0.01);
    }

    #[test]
    fn test_calculate_iob_array_length() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        let treatments = vec![Treatment::bolus(1.0, now)];

        let iob = calculate(&profile, &treatments, now, false).unwrap();
        // Should have 48 entries (4 hours / 5 min = 48)
        assert_eq!(iob.len(), 48);
    }

    #[test]
    fn test_iob_decreases_over_time() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        let treatments = vec![Treatment::bolus(1.0, now)];

        let iob = calculate(&profile, &treatments, now, false).unwrap();

        // IOB should generally decrease over time
        assert!(iob[0].iob > iob[10].iob);
        assert!(iob[10].iob > iob[20].iob);
        assert!(iob[20].iob > iob[40].iob);
    }

    #[test]
    fn test_activity_peaks_then_decreases() {
        let now = Utc::now();
        let profile = make_profile(5.0, InsulinCurve::RapidActing);

        let treatments = vec![Treatment::bolus(1.0, now)];

        let iob = calculate(&profile, &treatments, now, false).unwrap();

        // Activity should be low at start, peak around 75 min (index ~15), then decrease
        let activity_at_0 = iob[0].activity;
        let activity_at_15 = iob[15].activity; // 75 min
        let activity_at_40 = iob[40].activity; // 200 min

        assert!(activity_at_15 > activity_at_0);
        assert!(activity_at_15 > activity_at_40);
    }
}
