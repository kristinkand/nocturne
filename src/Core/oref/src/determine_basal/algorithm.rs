//! Main determine basal algorithm

use chrono::{DateTime, Utc};
use crate::types::{
    AutosensData, CurrentTemp, DetermineBasalResult, GlucoseStatus,
    IOBData, MealData, Profile,
};
use crate::utils::round_basal;
use crate::Result;
use super::DetermineBasalInputs;

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
        return Ok(result);
    }

    // ============ Calculate Expected BG Impact ============
    // BGI = how much BG is expected to change based on current IOB activity
    let bgi = -iob_data.activity * sens * 5.0;
    let deviation = glucose_status.delta - bgi;

    // ============ Calculate Eventual BG ============
    // Eventual BG if we continue at current temp and let IOB decay
    let eventual_bg = bg + (glucose_status.delta * 12.0) - (iob_data.iob * sens);
    let eventual_bg = eventual_bg.max(0.0);

    // ============ Determine Action ============
    let mut result = DetermineBasalResult::default();
    result.cob = meal_data.meal_cob;
    result.iob = iob_data.iob;
    result.eventual_bg = eventual_bg;
    result.bg_mins_ago = Some(bg_mins_ago);
    result.target_bg = Some(target_bg);
    result.sensitivity_ratio = Some(autosens_data.ratio);

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
            let smb_ratio = profile.smb_delivery_ratio.min(1.0);
            let max_smb = (profile.max_smb_basal_minutes as f64 / 60.0) * basal;
            let smb_amount = (insulin_req * smb_ratio).min(max_smb);
            let smb_amount = (smb_amount / profile.bolus_increment).floor() * profile.bolus_increment;

            if smb_amount >= profile.bolus_increment {
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
