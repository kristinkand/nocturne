//! Autosens - automatic sensitivity detection
//!
//! Detects changes in insulin sensitivity over time by analyzing
//! glucose deviations from expected values.
//!
//! This implements the algorithm from `lib/determine-basal/autosens.js`.

use chrono::{DateTime, Duration, Timelike, Utc};
use crate::types::{AutosensData, GlucoseReading, Profile, Treatment, TempTarget};
use crate::insulin::calculate_iob_contrib;
use crate::profile::{isf_lookup, basal_lookup};
use crate::Result;

/// Configuration for autosens detection
#[derive(Debug, Clone)]
pub struct AutosensConfig {
    /// Number of deviations to use (default 96 = 8 hours)
    pub lookback: usize,
    /// Use retrospective mode (look back from first glucose reading)
    pub retrospective: bool,
    /// Minimum autosens ratio
    pub autosens_min: f64,
    /// Maximum autosens ratio
    pub autosens_max: f64,
}

impl Default for AutosensConfig {
    fn default() -> Self {
        Self {
            lookback: 96,
            retrospective: false,
            autosens_min: 0.7,
            autosens_max: 1.2,
        }
    }
}

/// Detect insulin sensitivity from glucose history
///
/// This implements the algorithm from `lib/determine-basal/autosens.js`.
///
/// # Arguments
/// * `profile` - User profile settings
/// * `glucose_data` - Historical glucose readings
/// * `treatments` - Treatment history
/// * `clock` - Current time
///
/// # Returns
/// AutosensData with detected sensitivity ratio
pub fn detect_sensitivity(
    profile: &Profile,
    glucose_data: &[GlucoseReading],
    treatments: &[Treatment],
    clock: DateTime<Utc>,
) -> Result<AutosensData> {
    detect_sensitivity_with_config(
        profile,
        glucose_data,
        treatments,
        &[],
        clock,
        &AutosensConfig::default(),
    )
}

/// Detect sensitivity with full configuration
pub fn detect_sensitivity_with_config(
    profile: &Profile,
    glucose_data: &[GlucoseReading],
    treatments: &[Treatment],
    temp_targets: &[TempTarget],
    clock: DateTime<Utc>,
    config: &AutosensConfig,
) -> Result<AutosensData> {
    if glucose_data.is_empty() {
        return Ok(AutosensData { ratio: 1.0 });
    }

    // Determine last site change (default to 24 hours ago)
    let last_site_change = if config.retrospective {
        DateTime::from_timestamp_millis(glucose_data[0].date)
            .unwrap_or(clock)
            - Duration::hours(24)
    } else {
        clock - Duration::hours(24)
    };

    // Check for pump rewind events if configured
    let last_site_change = if profile.rewind_resets_autosens {
        find_last_rewind(treatments).unwrap_or(last_site_change)
    } else {
        last_site_change
    };

    // Bucket glucose data
    let bucketed_data = bucket_glucose_data_for_autosens(glucose_data, last_site_change);

    if bucketed_data.len() < 4 {
        return Ok(AutosensData { ratio: 1.0 });
    }

    // Find meal treatments for exclusion
    let meals = find_meal_treatments(treatments, &bucketed_data);

    // Calculate deviations
    let deviations = calculate_deviations(
        profile,
        &bucketed_data,
        treatments,
        &meals,
        temp_targets,
        config,
    )?;

    if deviations.is_empty() {
        return Ok(AutosensData { ratio: 1.0 });
    }

    // Calculate sensitivity ratio from deviations
    let ratio = calculate_ratio_from_deviations(&deviations, profile, config);

    Ok(AutosensData { ratio })
}

/// Bucketed glucose data point
#[derive(Debug, Clone)]
struct BucketedGlucose {
    date: i64,
    glucose: f64,
    meal_carbs: f64,
}

/// Bucket glucose data into 5-minute intervals for autosens
fn bucket_glucose_data_for_autosens(
    glucose_data: &[GlucoseReading],
    last_site_change: DateTime<Utc>,
) -> Vec<BucketedGlucose> {
    if glucose_data.is_empty() {
        return Vec::new();
    }

    let last_site_millis = last_site_change.timestamp_millis();
    let mut bucketed = Vec::new();

    // Process in chronological order (reverse if needed)
    let mut sorted: Vec<_> = glucose_data.iter().collect();
    sorted.sort_by_key(|g| g.date);

    for (i, reading) in sorted.iter().enumerate() {
        if reading.glucose < 39.0 {
            continue;
        }

        // Only consider BGs since last site change
        if reading.date < last_site_millis {
            continue;
        }

        if i == 0 {
            bucketed.push(BucketedGlucose {
                date: reading.date,
                glucose: reading.glucose,
                meal_carbs: 0.0,
            });
            continue;
        }

        let prev = &sorted[i - 1];
        if prev.glucose < 39.0 {
            continue;
        }

        let elapsed_minutes = (reading.date - prev.date) as f64 / 60000.0;

        if elapsed_minutes.abs() > 2.0 {
            bucketed.push(BucketedGlucose {
                date: reading.date,
                glucose: reading.glucose,
                meal_carbs: 0.0,
            });
        } else if let Some(last) = bucketed.last_mut() {
            // Average with previous if very close
            last.glucose = (last.glucose + reading.glucose) / 2.0;
        }
    }

    bucketed
}

/// Find the last pump rewind event
fn find_last_rewind(treatments: &[Treatment]) -> Option<DateTime<Utc>> {
    treatments
        .iter()
        .filter(|t| t.event_type.as_deref() == Some("Rewind"))
        .max_by_key(|t| t.effective_date())
        .and_then(|t| DateTime::from_timestamp_millis(t.effective_date()))
}

/// Find meal treatments sorted by time
fn find_meal_treatments(
    treatments: &[Treatment],
    bucketed_data: &[BucketedGlucose],
) -> Vec<(i64, f64)> {
    if bucketed_data.is_empty() {
        return Vec::new();
    }

    let oldest_bg = bucketed_data.first().map(|b| b.date).unwrap_or(0);

    let mut meals: Vec<_> = treatments
        .iter()
        .filter(|t| t.carbs.unwrap_or(0.0) >= 1.0)
        .filter(|t| t.effective_date() >= oldest_bg)
        .map(|t| (t.effective_date(), t.carbs.unwrap_or(0.0)))
        .collect();

    meals.sort_by_key(|(time, _)| *time);
    meals
}

/// Deviation type for categorization
#[derive(Debug, Clone, Copy, PartialEq)]
enum DeviationType {
    /// Normal non-meal deviation
    NonMeal,
    /// Carb absorption deviation (excluded)
    CarbAbsorption,
    /// Unannounced meal deviation (excluded)
    Uam,
}

/// Calculate deviations from glucose data
fn calculate_deviations(
    profile: &Profile,
    bucketed_data: &[BucketedGlucose],
    treatments: &[Treatment],
    meals: &[(i64, f64)],
    temp_targets: &[TempTarget],
    config: &AutosensConfig,
) -> Result<Vec<f64>> {
    let mut deviations = Vec::new();
    let mut meal_cob = 0.0;
    let mut meal_carbs = 0.0;
    let mut absorbing = false;
    let mut uam = false;
    let mut meal_start_counter = 999;
    let mut current_type = DeviationType::NonMeal;
    let mut meals_stack: Vec<(i64, f64)> = meals.to_vec();

    // Process from index 3 to have enough history for avgDelta
    for i in 3..bucketed_data.len() {
        let bg = bucketed_data[i].glucose;
        let last_bg = bucketed_data[i - 1].glucose;
        let old_bg = bucketed_data[i - 3].glucose;

        if bg < 40.0 || old_bg < 40.0 || last_bg < 40.0 {
            continue;
        }

        let bg_time = bucketed_data[i].date;
        let bg_datetime = DateTime::from_timestamp_millis(bg_time).unwrap_or(Utc::now());

        // Calculate deltas
        let avg_delta = (bg - old_bg) / 3.0;
        let delta = bg - last_bg;

        // Get sensitivity at this time
        let sens = isf_lookup(profile, bg_datetime);

        // Calculate IOB at this time
        let iob = calculate_iob_at_time(profile, treatments, bg_datetime);

        // Calculate BGI
        let bgi = round_to_decimal(-iob.activity * sens * 5.0, 2);

        // Calculate deviation
        let mut deviation = delta - bgi;

        // Set positive deviations to zero if BG < 80
        if bg < 80.0 && deviation > 0.0 {
            deviation = 0.0;
        }

        let deviation = round_to_decimal(deviation, 2);

        // Process meal carbs - add any meals older than current BG
        while let Some(&(meal_time, carbs)) = meals_stack.last() {
            if meal_time < bg_time {
                meal_cob += carbs;
                meal_carbs += carbs;
                meals_stack.pop();
            } else {
                break;
            }
        }

        // Calculate carb absorption for this interval
        if meal_cob > 0.0 {
            let ci = deviation.max(profile.min_5m_carbimpact);
            let absorbed = ci * profile.carb_ratio / sens;
            meal_cob = (meal_cob - absorbed).max(0.0);
        }

        // Determine deviation type
        if meal_cob > 0.0 || absorbing || meal_carbs > 0.0 {
            // Carb absorption mode
            absorbing = deviation > 0.0;

            // Stop excluding after 5h if COB is depleted
            if meal_start_counter > 60 && meal_cob < 0.5 {
                absorbing = false;
            }

            if !absorbing && meal_cob < 0.5 {
                meal_carbs = 0.0;
            }

            if current_type != DeviationType::CarbAbsorption {
                meal_start_counter = 0;
            }

            meal_start_counter += 1;
            current_type = DeviationType::CarbAbsorption;
        } else {
            let current_basal = basal_lookup(profile, bg_datetime);

            // Check for UAM
            if (!config.retrospective && iob.iob > 2.0 * current_basal)
                || uam
                || meal_start_counter < 9
            {
                meal_start_counter += 1;
                uam = deviation > 0.0;
                current_type = DeviationType::Uam;
            } else {
                if current_type == DeviationType::Uam {
                    // End UAM
                }
                current_type = DeviationType::NonMeal;
            }
        }

        // Only include non-meal deviations
        if current_type == DeviationType::NonMeal {
            deviations.push(deviation);

            // Add extra negative deviation for high temp targets if exercise mode
            if profile.exercise_mode || profile.high_temptarget_raises_sensitivity {
                if let Some(tt) = get_active_temp_target(temp_targets, bg_datetime) {
                    if tt > 100.0 {
                        let temp_deviation = -(tt - 100.0) / 20.0;
                        deviations.push(temp_deviation);
                    }
                }
            }
        }

        // Add neutral deviation every 2 hours to help decay
        let hours = bg_datetime.hour();
        let minutes = bg_datetime.minute();
        if minutes < 5 && hours % 2 == 0 {
            deviations.push(0.0);
        }

        // Keep only last N deviations
        if deviations.len() > config.lookback {
            deviations.remove(0);
        }
    }

    // Pad with zeros if we don't have enough data (dampens sensitivity changes)
    if deviations.len() < 96 {
        let pad = ((1.0 - deviations.len() as f64 / 96.0) * 18.0).round() as usize;
        for _ in 0..pad {
            deviations.push(0.0);
        }
    }

    Ok(deviations)
}

/// Calculate sensitivity ratio from deviations
fn calculate_ratio_from_deviations(
    deviations: &[f64],
    profile: &Profile,
    config: &AutosensConfig,
) -> f64 {
    if deviations.is_empty() {
        return 1.0;
    }

    let mut sorted = deviations.to_vec();
    sorted.sort_by(|a, b| a.partial_cmp(b).unwrap_or(std::cmp::Ordering::Equal));

    // Get 50th percentile (median)
    let p50 = percentile(&sorted, 0.50);

    // Calculate basal offset based on median deviation
    let basal_off = p50 * (60.0 / 5.0) / profile.sens;

    // Calculate raw ratio
    let raw_ratio = 1.0 + (basal_off / profile.max_basal);

    // Clamp to configured limits
    let ratio = raw_ratio
        .max(config.autosens_min)
        .min(config.autosens_max);

    // Round to 2 decimal places
    (ratio * 100.0).round() / 100.0
}

/// Calculate percentile of sorted values
fn percentile(sorted: &[f64], p: f64) -> f64 {
    if sorted.is_empty() {
        return 0.0;
    }

    let index = (p * (sorted.len() - 1) as f64).round() as usize;
    sorted[index.min(sorted.len() - 1)]
}

/// IOB data for internal calculations
struct IobCalc {
    iob: f64,
    activity: f64,
}

/// Calculate IOB at a specific time
fn calculate_iob_at_time(
    profile: &Profile,
    treatments: &[Treatment],
    time: DateTime<Utc>,
) -> IobCalc {
    let time_millis = time.timestamp_millis();
    let dia_millis = (profile.dia * 3600.0 * 1000.0) as i64;

    let mut iob = 0.0;
    let mut activity = 0.0;

    for treatment in treatments {
        let treatment_time = treatment.effective_date();

        if treatment_time > time_millis {
            continue;
        }

        if time_millis - treatment_time > dia_millis {
            continue;
        }

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

    IobCalc { iob, activity }
}

/// Get active temp target at a given time
fn get_active_temp_target(temp_targets: &[TempTarget], time: DateTime<Utc>) -> Option<f64> {
    let time_millis = time.timestamp_millis();

    // Sort by created_at descending (most recent first)
    let mut sorted: Vec<_> = temp_targets.iter().collect();
    sorted.sort_by(|a, b| b.created_at.cmp(&a.created_at));

    for tt in sorted {
        let start = tt.created_at;
        let expires = start + tt.duration as i64 * 60 * 1000;

        if tt.duration == 0 && time_millis >= start {
            // Cancelled temp target
            return None;
        } else if time_millis >= start && time_millis < expires {
            return Some((tt.target_top + tt.target_bottom) / 2.0);
        }
    }

    None
}

/// Round to a specific number of decimal places
fn round_to_decimal(value: f64, decimals: u32) -> f64 {
    let factor = 10_f64.powi(decimals as i32);
    (value * factor).round() / factor
}

/// Result of sensitivity detection
#[derive(Debug, Clone)]
pub struct SensitivityResult {
    /// Sensitivity ratio (1.0 = normal)
    pub ratio: f64,

    /// Number of deviations used
    pub deviation_count: usize,

    /// Average deviation
    pub avg_deviation: f64,

    /// Sensitivity category
    pub category: SensitivityCategory,
}

/// Sensitivity category
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum SensitivityCategory {
    /// Normal sensitivity
    Normal,
    /// Increased sensitivity (ratio < 1.0)
    Sensitive,
    /// Decreased sensitivity (ratio > 1.0)
    Resistant,
}

impl From<f64> for SensitivityCategory {
    fn from(ratio: f64) -> Self {
        if ratio < 0.95 {
            SensitivityCategory::Sensitive
        } else if ratio > 1.05 {
            SensitivityCategory::Resistant
        } else {
            SensitivityCategory::Normal
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_percentile() {
        let values = vec![1.0, 2.0, 3.0, 4.0, 5.0];
        assert_eq!(percentile(&values, 0.5), 3.0);
        assert_eq!(percentile(&values, 0.0), 1.0);
        assert_eq!(percentile(&values, 1.0), 5.0);
    }

    #[test]
    fn test_sensitivity_category() {
        assert_eq!(SensitivityCategory::from(0.8), SensitivityCategory::Sensitive);
        assert_eq!(SensitivityCategory::from(1.0), SensitivityCategory::Normal);
        assert_eq!(SensitivityCategory::from(1.2), SensitivityCategory::Resistant);
    }

    #[test]
    fn test_round_to_decimal() {
        assert_eq!(round_to_decimal(1.2345, 2), 1.23);
        assert_eq!(round_to_decimal(1.2355, 2), 1.24);
    }
}
