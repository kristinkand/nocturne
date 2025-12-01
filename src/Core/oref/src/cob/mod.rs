//! COB (Carbs on Board) calculations
//!
//! This module calculates carb absorption using glucose deviation analysis,
//! implementing the algorithm from `lib/determine-basal/cob.js`.

use chrono::{DateTime, Duration, Utc};
use crate::types::{COBResult, GlucoseReading, Profile, Treatment, IOBData};
use crate::insulin::calculate_iob_contrib;
use crate::profile::{isf_lookup, basal_lookup};
use crate::Result;

/// Bucketed glucose data point for interpolation
#[derive(Debug, Clone)]
struct BucketedGlucose {
    date: i64,
    glucose: f64,
}

/// Result of carb absorption detection
#[derive(Debug, Clone, Default)]
pub struct CarbAbsorptionResult {
    /// Total carbs absorbed since meal
    pub carbs_absorbed: f64,
    /// Current deviation from expected BG
    pub current_deviation: f64,
    /// Maximum deviation seen
    pub max_deviation: f64,
    /// Minimum deviation seen
    pub min_deviation: f64,
    /// Slope from max deviation
    pub slope_from_max_deviation: f64,
    /// Slope from min deviation
    pub slope_from_min_deviation: f64,
    /// All deviations
    pub all_deviations: Vec<i32>,
}

/// Calculate carb absorption from glucose deviations
///
/// This implements the algorithm from `lib/determine-basal/cob.js`.
pub fn calculate(
    profile: &Profile,
    glucose_data: &[GlucoseReading],
    treatments: &[Treatment],
    clock: DateTime<Utc>,
) -> Result<COBResult> {
    // Find meal time from most recent carb entry
    let meal_time = find_meal_time(treatments, clock, profile.max_meal_absorption_time);

    if meal_time.is_none() {
        return Ok(COBResult::default());
    }

    let meal_time = meal_time.unwrap();
    let absorption = detect_carb_absorption_internal(
        profile,
        glucose_data,
        treatments,
        meal_time,
        clock,
    )?;

    // Calculate remaining COB
    let total_carbs = calculate_total_carbs(treatments, meal_time, clock);
    let meal_cob = (total_carbs - absorption.carbs_absorbed).max(0.0);

    Ok(COBResult {
        meal_cob,
        carbs_absorbed: absorption.carbs_absorbed,
        current_deviation: absorption.current_deviation,
        max_deviation: absorption.max_deviation,
        min_deviation: absorption.min_deviation,
        slope_from_max: absorption.slope_from_max_deviation,
        slope_from_min: absorption.slope_from_min_deviation,
    })
}

/// Find the most recent meal time within absorption window
fn find_meal_time(
    treatments: &[Treatment],
    clock: DateTime<Utc>,
    max_absorption_hours: f64,
) -> Option<DateTime<Utc>> {
    let clock_millis = clock.timestamp_millis();
    let window_start = clock_millis - (max_absorption_hours * 3600.0 * 1000.0) as i64;

    treatments
        .iter()
        .filter(|t| {
            let time = t.effective_date();
            time >= window_start && time <= clock_millis && t.carbs.unwrap_or(0.0) >= 1.0
        })
        .max_by_key(|t| t.effective_date())
        .map(|t| DateTime::from_timestamp_millis(t.effective_date()).unwrap())
}

/// Calculate total carbs from treatments since meal time
fn calculate_total_carbs(
    treatments: &[Treatment],
    meal_time: DateTime<Utc>,
    clock: DateTime<Utc>,
) -> f64 {
    let meal_millis = meal_time.timestamp_millis();
    let clock_millis = clock.timestamp_millis();

    treatments
        .iter()
        .filter(|t| {
            let time = t.effective_date();
            time >= meal_millis && time <= clock_millis
        })
        .filter_map(|t| t.carbs)
        .filter(|&c| c >= 1.0)
        .sum()
}

/// Detect carb absorption from BG deviations
///
/// This is the core algorithm that buckets glucose data and calculates
/// deviations from expected BG based on IOB activity.
fn detect_carb_absorption_internal(
    profile: &Profile,
    glucose_data: &[GlucoseReading],
    treatments: &[Treatment],
    meal_time: DateTime<Utc>,
    ci_time: DateTime<Utc>,
) -> Result<CarbAbsorptionResult> {
    if glucose_data.is_empty() {
        return Ok(CarbAbsorptionResult::default());
    }

    let meal_millis = meal_time.timestamp_millis();

    // Bucket the glucose data into 5-minute intervals
    let bucketed_data = bucket_glucose_data(glucose_data, meal_millis, profile.max_meal_absorption_time);

    if bucketed_data.len() < 4 {
        return Ok(CarbAbsorptionResult::default());
    }

    let mut carbs_absorbed = 0.0;
    let mut current_deviation = 0.0;
    let mut max_deviation = 0.0;
    let mut min_deviation = 999.0;
    let mut slope_from_max_deviation = 0.0;
    let mut slope_from_min_deviation = 999.0;
    let mut all_deviations = Vec::new();

    let ci_millis = ci_time.timestamp_millis();

    // Process bucketed data
    for i in 0..(bucketed_data.len().saturating_sub(3)) {
        let bg_time = bucketed_data[i].date;
        let bg = bucketed_data[i].glucose;

        if bg < 39.0 || bucketed_data[i + 3].glucose < 39.0 {
            continue;
        }

        // Calculate average delta over 15 minutes (3 readings)
        let avg_delta = (bg - bucketed_data[i + 3].glucose) / 3.0;
        let delta = bg - bucketed_data[i + 1].glucose;

        // Get sensitivity at this time
        let bg_datetime = DateTime::from_timestamp_millis(bg_time).unwrap_or(ci_time);
        let sens = isf_lookup(profile, bg_datetime);

        // Calculate IOB at this time
        let iob = calculate_iob_at_time(profile, treatments, bg_datetime);

        // Calculate BGI (BG Impact from insulin)
        let bgi = round_to_decimal(-iob.activity * sens * 5.0, 2);

        // Calculate deviation (actual change - expected change)
        let deviation = round_to_decimal(delta - bgi, 2);

        // Calculate current deviation (for first reading)
        if i == 0 {
            current_deviation = round_to_decimal(avg_delta - bgi, 3);
            if ci_millis > bg_time {
                all_deviations.push(current_deviation.round() as i32);
            }
        } else if ci_millis > bg_time {
            let avg_deviation = round_to_decimal(avg_delta - bgi, 3);
            let deviation_slope = (avg_deviation - current_deviation)
                / (bg_time - ci_millis) as f64 * 1000.0 * 60.0 * 5.0;

            if avg_deviation > max_deviation {
                slope_from_max_deviation = deviation_slope.min(0.0);
                max_deviation = avg_deviation;
            }
            if avg_deviation < min_deviation {
                slope_from_min_deviation = deviation_slope.max(0.0);
                min_deviation = avg_deviation;
            }

            all_deviations.push(avg_deviation.round() as i32);
        }

        // If this reading is after meal time, calculate carb absorption
        if bg_time > meal_millis {
            // Carb impact: use the greater of deviation, current_deviation/2, or min_5m_carbimpact
            let ci = deviation
                .max(current_deviation / 2.0)
                .max(profile.min_5m_carbimpact);

            // Convert to carbs absorbed using carb ratio and sensitivity
            let absorbed = ci * profile.carb_ratio / sens;
            carbs_absorbed += absorbed;
        }
    }

    Ok(CarbAbsorptionResult {
        carbs_absorbed,
        current_deviation,
        max_deviation,
        min_deviation,
        slope_from_max_deviation,
        slope_from_min_deviation,
        all_deviations,
    })
}

/// Bucket glucose data into 5-minute intervals with interpolation
fn bucket_glucose_data(
    glucose_data: &[GlucoseReading],
    meal_millis: i64,
    max_absorption_hours: f64,
) -> Vec<BucketedGlucose> {
    if glucose_data.is_empty() {
        return Vec::new();
    }

    let mut bucketed = Vec::new();

    // Start with first valid reading
    if let Some(first) = glucose_data.first() {
        if first.glucose >= 39.0 {
            bucketed.push(BucketedGlucose {
                date: first.date,
                glucose: first.glucose,
            });
        }
    }

    let mut last_bg_time = glucose_data.first().map(|g| g.date).unwrap_or(0);
    let mut last_bg = glucose_data.first().map(|g| g.glucose).unwrap_or(0.0);
    let mut found_pre_meal = false;

    for reading in glucose_data.iter().skip(1) {
        let bg_time = reading.date;

        // Skip invalid readings
        if reading.glucose < 39.0 {
            continue;
        }

        // Check if within absorption window
        let hours_after_meal = (bg_time - meal_millis) as f64 / (3600.0 * 1000.0);
        if hours_after_meal > max_absorption_hours || found_pre_meal {
            continue;
        } else if hours_after_meal < 0.0 {
            found_pre_meal = true;
        }

        let elapsed_minutes = (bg_time - last_bg_time) as f64 / 60000.0;

        if elapsed_minutes.abs() > 8.0 {
            // Interpolate missing data points (cap at 4 hours)
            let mut remaining = elapsed_minutes.abs().min(240.0);
            let mut interp_time = last_bg_time;
            let gap_delta = reading.glucose - last_bg;

            while remaining > 5.0 {
                interp_time -= 5 * 60 * 1000;
                let interp_bg = last_bg + (5.0 / remaining * gap_delta);

                bucketed.push(BucketedGlucose {
                    date: interp_time,
                    glucose: interp_bg.round(),
                });

                remaining -= 5.0;
                last_bg = interp_bg;
            }
        } else if elapsed_minutes.abs() > 2.0 {
            bucketed.push(BucketedGlucose {
                date: bg_time,
                glucose: reading.glucose,
            });
        } else if let Some(last) = bucketed.last_mut() {
            // Average with previous if very close
            last.glucose = (last.glucose + reading.glucose) / 2.0;
        }

        last_bg_time = bg_time;
        last_bg = reading.glucose;
    }

    bucketed
}

/// Calculate IOB at a specific time
fn calculate_iob_at_time(
    profile: &Profile,
    treatments: &[Treatment],
    time: DateTime<Utc>,
) -> IOBData {
    let time_millis = time.timestamp_millis();
    let dia_millis = (profile.dia * 60.0 * 60.0 * 1000.0) as i64;

    let mut iob = 0.0;
    let mut activity = 0.0;

    for treatment in treatments {
        let treatment_time = treatment.effective_date();

        // Skip if treatment is after current time
        if treatment_time > time_millis {
            continue;
        }

        // Skip if treatment is older than DIA
        if time_millis - treatment_time > dia_millis {
            continue;
        }

        // Get insulin amount
        let insulin = treatment.insulin.unwrap_or(0.0);
        if insulin <= 0.0 {
            continue;
        }

        let minutes_ago = (time_millis - treatment_time) as f64 / 60000.0;
        let contrib = calculate_iob_contrib(
            insulin,
            minutes_ago,
            profile.curve,
            profile.dia,
            profile.peak,
        );

        iob += contrib.iob_contrib;
        activity += contrib.activity_contrib;
    }

    IOBData {
        iob,
        activity,
        basal_iob: 0.0,
        bolus_iob: iob,
        net_basal_insulin: 0.0,
        bolus_insulin: 0.0,
        time,
        iob_with_zero_temp: None,
        last_bolus_time: None,
        last_temp: None,
    }
}

/// Round to a specific number of decimal places
fn round_to_decimal(value: f64, decimals: u32) -> f64 {
    let factor = 10_f64.powi(decimals as i32);
    (value * factor).round() / factor
}

/// Detect carb absorption from BG deviations (public interface)
pub fn detect_carb_absorption(
    profile: &Profile,
    glucose_data: &[GlucoseReading],
    meal_time: DateTime<Utc>,
) -> Result<f64> {
    let result = detect_carb_absorption_internal(
        profile,
        glucose_data,
        &[],
        meal_time,
        Utc::now(),
    )?;
    Ok(result.carbs_absorbed)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::insulin::InsulinCurve;

    fn test_profile() -> Profile {
        Profile {
            dia: 5.0,
            sens: 50.0,
            carb_ratio: 10.0,
            curve: InsulinCurve::RapidActing,
            peak: 75,
            min_5m_carbimpact: 8.0,
            max_meal_absorption_time: 6.0,
            current_basal: 1.0,
            max_basal: 3.0,
            max_iob: 5.0,
            min_bg: 100.0,
            max_bg: 120.0,
            ..Default::default()
        }
    }

    #[test]
    fn test_empty_data_returns_default() {
        let profile = test_profile();
        let result = calculate(&profile, &[], &[], Utc::now()).unwrap();
        assert_eq!(result.meal_cob, 0.0);
    }

    #[test]
    fn test_round_to_decimal() {
        assert_eq!(round_to_decimal(1.2345, 2), 1.23);
        assert_eq!(round_to_decimal(1.2355, 2), 1.24);
        assert_eq!(round_to_decimal(-1.2345, 2), -1.23);
    }
}
