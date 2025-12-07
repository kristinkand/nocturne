//! SMB (Super Micro Bolus) logic

use crate::types::{MealData, Profile};

/// Determine if SMB should be enabled
///
/// This matches the `enable_smb()` function in `determine-basal.js`.
pub fn should_enable_smb(
    profile: &Profile,
    micro_bolus_allowed: bool,
    meal_data: &MealData,
    bg: f64,
    target_bg: f64,
) -> bool {
    if !micro_bolus_allowed {
        return false;
    }

    // Don't allow SMB with high temp target unless configured
    if !profile.allow_smb_with_high_temptarget && profile.temptarget_set && target_bg > 100.0 {
        return false;
    }

    // Check Bolus Wizard safety
    if meal_data.bw_found && !profile.a52_risk_enable {
        return false;
    }

    // Always enabled
    if profile.enable_smb_always {
        return true;
    }

    // Enabled with COB
    if profile.enable_smb_with_cob && meal_data.meal_cob > 0.0 {
        return true;
    }

    // Enabled after carbs (within 6h)
    if profile.enable_smb_after_carbs && meal_data.carbs > 0.0 {
        return true;
    }

    // Enabled with low temp target
    if profile.enable_smb_with_temptarget && profile.temptarget_set && target_bg < 100.0 {
        return true;
    }

    // Enabled for high BG
    if profile.enable_smb_high_bg && bg >= profile.enable_smb_high_bg_target {
        return true;
    }

    false
}

/// Calculate maximum SMB amount
pub fn calculate_max_smb(
    profile: &Profile,
    _iob: f64,
    _cob: f64,
    basal: f64,
) -> f64 {
    // Max SMB is limited by max minutes of basal
    let max_minutes = if _cob > 0.0 {
        profile.max_smb_basal_minutes
    } else {
        profile.max_uam_smb_basal_minutes
    };

    (max_minutes as f64 / 60.0) * basal
}

/// Calculate recommended SMB amount
pub fn calculate_smb(
    profile: &Profile,
    insulin_req: f64,
    iob: f64,
    cob: f64,
    basal: f64,
) -> Option<f64> {
    if insulin_req <= 0.0 {
        return None;
    }

    let max_smb = calculate_max_smb(profile, iob, cob, basal);
    let smb_ratio = profile.smb_delivery_ratio.min(1.0);

    let smb = (insulin_req * smb_ratio).min(max_smb);

    // Round to bolus increment
    let smb = (smb / profile.bolus_increment).floor() * profile.bolus_increment;

    // Must meet minimum threshold
    if smb >= profile.bolus_increment {
        Some(smb)
    } else {
        None
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn make_profile() -> Profile {
        Profile {
            enable_smb_always: false,
            enable_smb_with_cob: true,
            enable_smb_after_carbs: false,
            enable_smb_with_temptarget: false,
            enable_smb_high_bg: false,
            enable_smb_high_bg_target: 110.0,
            allow_smb_with_high_temptarget: false,
            a52_risk_enable: false,
            max_smb_basal_minutes: 30,
            max_uam_smb_basal_minutes: 30,
            smb_delivery_ratio: 0.5,
            bolus_increment: 0.1,
            ..Default::default()
        }
    }

    #[test]
    fn test_smb_disabled_when_not_allowed() {
        let profile = make_profile();
        let meal_data = MealData::with_cob(50.0, 50.0);

        assert!(!should_enable_smb(&profile, false, &meal_data, 150.0, 100.0));
    }

    #[test]
    fn test_smb_enabled_with_cob() {
        let profile = make_profile();
        let meal_data = MealData::with_cob(50.0, 50.0);

        assert!(should_enable_smb(&profile, true, &meal_data, 150.0, 100.0));
    }

    #[test]
    fn test_smb_disabled_without_cob() {
        let profile = make_profile();
        let meal_data = MealData::empty();

        assert!(!should_enable_smb(&profile, true, &meal_data, 150.0, 100.0));
    }

    #[test]
    fn test_smb_always_enabled() {
        let mut profile = make_profile();
        profile.enable_smb_always = true;
        let meal_data = MealData::empty();

        assert!(should_enable_smb(&profile, true, &meal_data, 150.0, 100.0));
    }

    #[test]
    fn test_calculate_smb() {
        let profile = make_profile();

        // Need 1 U, basal is 1 U/hr, max 30 min = 0.5 U max
        // With 50% delivery ratio = 0.5 U * 0.5 = 0.25 U
        let smb = calculate_smb(&profile, 1.0, 0.0, 50.0, 1.0);

        assert!(smb.is_some());
        assert!(smb.unwrap() <= 0.5);
        assert!(smb.unwrap() >= 0.1);
    }

    #[test]
    fn test_smb_rounds_to_increment() {
        let mut profile = make_profile();
        profile.bolus_increment = 0.05;

        let smb = calculate_smb(&profile, 0.13, 0.0, 50.0, 1.0);

        if let Some(units) = smb {
            // Should be a multiple of 0.05
            let remainder = (units * 100.0) % 5.0;
            assert!(remainder < 0.001);
        }
    }
}
