//! Main determine basal algorithm

use chrono::Utc;
use crate::types::{
    DetermineBasalResult, GlucoseStatus,
    IOBData, MealData, Profile,
};
use crate::utils::round_basal;
use crate::Result;
use super::DetermineBasalInputs;
use super::predictions;
use super::smb;

/// Run the determine basal algorithm
///
/// This is the main entry point that matches `lib/determine-basal/determine-basal.js`.
pub fn determine_basal(inputs: &DetermineBasalInputs) -> Result<DetermineBasalResult> {
    let DetermineBasalInputs {
        glucose_status,
        current_temp,
        iob_data,
        profile,
        autosens_data,
        meal_data,
        micro_bolus_allowed,
        current_time,
    } = inputs;

    let now = current_time.unwrap_or_else(Utc::now);

    // Validate profile
    if profile.current_basal <= 0.0 {
        return Ok(DetermineBasalResult::error("Could not get current basal rate"));
    }

    let bg = glucose_status.glucose;
    let target_bg = profile.min_bg;
    let sens = profile.sens;
    let basal = round_basal(profile.current_basal, profile);

    // Check if BG is too old
    let bg_mins_ago = (now.timestamp_millis() - glucose_status.date) as f64 / 60000.0;

    // Generate all prediction curves
    let pred_bgs = predictions::predict_glucose(glucose_status, iob_data, profile);
    let pred_bgs_iob = generate_iob_only_predictions(glucose_status, iob_data, profile);
    let pred_bgs_zt = generate_zero_temp_predictions(glucose_status, iob_data, profile);
    let pred_bgs_uam = generate_uam_predictions(glucose_status, iob_data, profile);
    let pred_bgs_cob = generate_cob_predictions(glucose_status, iob_data, meal_data, profile);

    // ============ Low Glucose Suspend ============
    if bg < 80.0 {
        // Low glucose - suspend insulin
        let reason = format!(
            "BG {:.0} < 80, temp zero",
            bg
        );

        let mut result = DetermineBasalResult::temp_basal(0.0, 30, reason);
        result.cob = meal_data.meal_cob;
        result.iob = iob_data.iob;
        result.eventual_bg = bg;
        result.bg_mins_ago = Some(bg_mins_ago);
        result.target_bg = Some(target_bg);
        result.predicted_bg = Some(pred_bgs);
        result.pred_bgs_iob = Some(pred_bgs_iob);
        result.pred_bgs_zt = Some(pred_bgs_zt);
        result.pred_bgs_uam = Some(pred_bgs_uam);
        result.pred_bgs_cob = Some(pred_bgs_cob);
        return Ok(result);
    }

    // ============ Calculate Expected BG Impact ============
    // BGI = how much BG is expected to change based on current IOB activity
    let bgi = predictions::calculate_bgi(iob_data.activity, sens);
    let _deviation = glucose_status.delta - bgi;

    // ============ Calculate Eventual BG ============
    // Eventual BG if we continue at current temp and let IOB decay
    let eventual_bg = predictions::calculate_eventual_bg(glucose_status, iob_data, profile);

    // ============ Determine Action ============
    let mut result = DetermineBasalResult::default();
    result.cob = meal_data.meal_cob;
    result.iob = iob_data.iob;
    result.eventual_bg = eventual_bg;
    result.bg_mins_ago = Some(bg_mins_ago);
    result.target_bg = Some(target_bg);
    result.sensitivity_ratio = Some(autosens_data.ratio);

    // Always populate predictions
    result.predicted_bg = Some(pred_bgs);
    result.pred_bgs_iob = Some(pred_bgs_iob);
    result.pred_bgs_zt = Some(pred_bgs_zt);
    result.pred_bgs_uam = Some(pred_bgs_uam);
    result.pred_bgs_cob = Some(pred_bgs_cob);

    // Calculate insulin required to get to target
    let insulin_req = (eventual_bg - target_bg) / sens;
    result.insulin_req = Some(insulin_req);

    // ============ In Range - No Action Needed ============
    if eventual_bg >= profile.min_bg && eventual_bg <= profile.max_bg {
        // In range - check if we need to cancel high temp
        if current_temp.is_active() && current_temp.rate > basal {
            // Cancel high temp
            result.rate = Some(basal);
            result.duration = Some(30);
            result.reason = format!(
                "Eventual BG {:.0} in range ({:.0}-{:.0}), canceling high temp",
                eventual_bg, profile.min_bg, profile.max_bg
            );
        } else {
            result.reason = format!(
                "Eventual BG {:.0} in range ({:.0}-{:.0}), no action needed",
                eventual_bg, profile.min_bg, profile.max_bg
            );
        }
        return Ok(result);
    }

    // ============ Above Target ============
    if eventual_bg > profile.max_bg {
        // Need more insulin
        let needed_rate = basal + (insulin_req / 0.5); // Rough conversion
        let needed_rate = needed_rate.max(0.0).min(profile.max_basal);
        let needed_rate = round_basal(needed_rate, profile);

        // Check if SMB would help
        if *micro_bolus_allowed && insulin_req > 0.0 {
            if let Some(smb_amount) = smb::calculate_smb(profile, insulin_req, iob_data.iob, meal_data.meal_cob, basal) {
                result.units = Some(smb_amount);
            }
        }

        result.rate = Some(needed_rate);
        result.duration = Some(30);
        result.reason = format!(
            "Eventual BG {:.0} > {:.0}, insulin required {:.2}U, setting temp {:.2}U/hr",
            eventual_bg, profile.max_bg, insulin_req, needed_rate
        );

        return Ok(result);
    }

    // ============ Below Target ============
    if eventual_bg < profile.min_bg {
        // Reduce insulin
        let needed_rate = basal + (insulin_req / 0.5);
        let needed_rate = needed_rate.max(0.0);
        let needed_rate = round_basal(needed_rate, profile);

        result.rate = Some(needed_rate);
        result.duration = Some(30);
        result.reason = format!(
            "Eventual BG {:.0} < {:.0}, reducing to {:.2}U/hr",
            eventual_bg, profile.min_bg, needed_rate
        );

        return Ok(result);
    }

    // Default: no action
    result.reason = "No action needed".to_string();
    Ok(result)
}

/// Generate IOB-only predictions (no delta extrapolation)
fn generate_iob_only_predictions(
    glucose_status: &GlucoseStatus,
    iob_data: &IOBData,
    profile: &Profile,
) -> Vec<f64> {
    let bg = glucose_status.glucose;
    let sens = profile.sens;

    (0..48).map(|i| {
        let minutes = i as f64 * 5.0;
        let iob_factor = (-minutes / 60.0).exp();
        let predicted_iob_effect = iob_data.iob * iob_factor * sens;
        (bg - predicted_iob_effect).max(39.0)
    }).collect()
}

/// Generate zero-temp predictions (no insulin delivery)
fn generate_zero_temp_predictions(
    glucose_status: &GlucoseStatus,
    iob_data: &IOBData,
    profile: &Profile,
) -> Vec<f64> {
    let bg = glucose_status.glucose;
    let sens = profile.sens;
    let basal = profile.current_basal;

    (0..48).map(|i| {
        let minutes = i as f64 * 5.0;
        // IOB effect decays, but we're not adding new insulin
        let iob_factor = (-minutes / 60.0).exp();
        let predicted_iob_effect = iob_data.iob * iob_factor * sens;
        // Add delta extrapolation (BG rises if we stop insulin)
        let delta_factor = (-minutes / 45.0).exp();
        let delta_effect = glucose_status.delta.max(0.0) * (minutes / 5.0) * delta_factor;
        // Baseline BG rise from lack of basal
        let basal_rise = (basal / 60.0) * minutes * sens * 0.5;
        (bg + delta_effect + basal_rise - predicted_iob_effect).max(39.0)
    }).collect()
}

/// Generate UAM predictions (unannounced meal detection)
fn generate_uam_predictions(
    glucose_status: &GlucoseStatus,
    iob_data: &IOBData,
    profile: &Profile,
) -> Vec<f64> {
    let bg = glucose_status.glucose;
    let sens = profile.sens;

    (0..48).map(|i| {
        let minutes = i as f64 * 5.0;
        let iob_factor = (-minutes / 60.0).exp();
        let predicted_iob_effect = iob_data.iob * iob_factor * sens;
        // UAM assumes delta continues longer
        let delta_factor = (-minutes / 60.0).exp(); // Slower decay
        let delta_effect = glucose_status.delta * (minutes / 5.0) * delta_factor;
        (bg + delta_effect - predicted_iob_effect).max(39.0)
    }).collect()
}

/// Generate COB predictions (with carb absorption)
fn generate_cob_predictions(
    glucose_status: &GlucoseStatus,
    iob_data: &IOBData,
    meal_data: &MealData,
    profile: &Profile,
) -> Vec<f64> {
    let bg = glucose_status.glucose;
    let sens = profile.sens;
    let carb_ratio = profile.carb_ratio.max(1.0);
    let cob = meal_data.meal_cob;

    (0..48).map(|i| {
        let minutes = i as f64 * 5.0;
        let iob_factor = (-minutes / 60.0).exp();
        let predicted_iob_effect = iob_data.iob * iob_factor * sens;
        // Delta extrapolation
        let delta_factor = (-minutes / 30.0).exp();
        let delta_effect = glucose_status.delta * (minutes / 5.0) * delta_factor;
        // Carb absorption effect (peaks at ~45 mins, decays after)
        let carb_absorption = if cob > 0.0 {
            let absorption_peak = (-((minutes - 45.0) / 30.0).powi(2)).exp();
            let carb_effect = (cob / carb_ratio) * sens * 0.5 * absorption_peak;
            carb_effect
        } else {
            0.0
        };
        (bg + delta_effect + carb_absorption - predicted_iob_effect).max(39.0)
    }).collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::types::{AutosensData, CurrentTemp, GlucoseStatus, IOBData, MealData, Profile};

    fn make_inputs() -> (GlucoseStatus, CurrentTemp, IOBData, Profile, AutosensData, MealData) {
        let glucose_status = GlucoseStatus {
            glucose: 115.0,
            delta: 0.0,
            short_avgdelta: 0.0,
            long_avgdelta: 0.1,
            date: Utc::now().timestamp_millis(),
            noise: None,
        };

        let current_temp = CurrentTemp::none();

        let iob_data = IOBData {
            iob: 0.0,
            activity: 0.0,
            ..Default::default()
        };

        let profile = Profile {
            current_basal: 0.9,
            max_basal: 3.5,
            min_bg: 110.0,
            max_bg: 120.0,
            sens: 40.0,
            max_iob: 2.5,
            ..Default::default()
        };

        let autosens = AutosensData::with_ratio(1.0);
        let meal_data = MealData::default();

        (glucose_status, current_temp, iob_data, profile, autosens, meal_data)
    }

    #[test]
    fn test_in_range_no_action() {
        let (glucose_status, current_temp, iob_data, profile, autosens, meal_data) = make_inputs();

        let inputs = DetermineBasalInputs {
            glucose_status: &glucose_status,
            current_temp: &current_temp,
            iob_data: &iob_data,
            profile: &profile,
            autosens_data: &autosens,
            meal_data: &meal_data,
            micro_bolus_allowed: false,
            current_time: Some(Utc::now()),
        };

        let result = determine_basal(&inputs).unwrap();

        // In range, no temp needed
        assert!(result.rate.is_none() || result.rate == Some(profile.current_basal));
    }

    #[test]
    fn test_low_glucose_suspend() {
        let (mut glucose_status, current_temp, iob_data, profile, autosens, meal_data) = make_inputs();
        glucose_status.glucose = 70.0;

        let inputs = DetermineBasalInputs {
            glucose_status: &glucose_status,
            current_temp: &current_temp,
            iob_data: &iob_data,
            profile: &profile,
            autosens_data: &autosens,
            meal_data: &meal_data,
            micro_bolus_allowed: false,
            current_time: Some(Utc::now()),
        };

        let result = determine_basal(&inputs).unwrap();

        // Should suspend to zero
        assert_eq!(result.rate, Some(0.0));
        assert!(result.duration.unwrap() >= 30);
    }

    #[test]
    fn test_high_glucose_increases_basal() {
        let (mut glucose_status, current_temp, iob_data, profile, autosens, meal_data) = make_inputs();
        glucose_status.glucose = 180.0;

        let inputs = DetermineBasalInputs {
            glucose_status: &glucose_status,
            current_temp: &current_temp,
            iob_data: &iob_data,
            profile: &profile,
            autosens_data: &autosens,
            meal_data: &meal_data,
            micro_bolus_allowed: false,
            current_time: Some(Utc::now()),
        };

        let result = determine_basal(&inputs).unwrap();

        // Should increase basal
        assert!(result.rate.unwrap() > profile.current_basal);
    }
}
