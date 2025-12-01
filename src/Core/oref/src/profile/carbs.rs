//! Carb ratio schedule lookups

use chrono::{DateTime, Utc};
use crate::types::Profile;

/// Look up the carb ratio at a specific time
pub fn carb_ratio_lookup(profile: &Profile, _time: DateTime<Utc>) -> f64 {
    // For now, just return the single carb ratio
    // Full implementation would support time-based schedules
    profile.carb_ratio
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_carb_ratio_lookup() {
        let profile = Profile {
            carb_ratio: 10.0,
            ..Default::default()
        };

        let ratio = carb_ratio_lookup(&profile, Utc::now());
        assert!((ratio - 10.0).abs() < 0.1);
    }
}
