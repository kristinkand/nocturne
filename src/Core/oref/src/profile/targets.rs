//! BG target schedule lookups

use chrono::{DateTime, Utc};
use crate::types::Profile;

/// BG targets result
#[derive(Debug, Clone)]
pub struct BgTargets {
    pub min_bg: f64,
    pub max_bg: f64,
    pub temptarget_set: bool,
}

impl Default for BgTargets {
    fn default() -> Self {
        Self {
            min_bg: 100.0,
            max_bg: 120.0,
            temptarget_set: false,
        }
    }
}

/// Look up BG targets at a specific time
pub fn bg_targets_lookup(profile: &Profile, _time: DateTime<Utc>) -> BgTargets {
    let mut targets = BgTargets {
        min_bg: profile.min_bg,
        max_bg: profile.max_bg,
        temptarget_set: profile.temptarget_set,
    };

    // Apply bounds
    targets = bound_target_range(targets);

    targets
}

/// Apply safety bounds to target range
fn bound_target_range(mut targets: BgTargets) -> BgTargets {
    // If targets are < 20, assume they're mmol/L and convert
    if targets.min_bg < 20.0 {
        targets.min_bg *= 18.0;
    }
    if targets.max_bg < 20.0 {
        targets.max_bg *= 18.0;
    }

    // Hard-code lower bounds (safety)
    targets.min_bg = targets.min_bg.max(80.0);
    targets.max_bg = targets.max_bg.max(80.0);

    // Hard-code upper bounds (safety)
    targets.min_bg = targets.min_bg.min(200.0);
    targets.max_bg = targets.max_bg.min(200.0);

    targets
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bg_targets_lookup() {
        let profile = Profile {
            min_bg: 100.0,
            max_bg: 120.0,
            ..Default::default()
        };

        let targets = bg_targets_lookup(&profile, Utc::now());
        assert!((targets.min_bg - 100.0).abs() < 0.1);
        assert!((targets.max_bg - 120.0).abs() < 0.1);
    }

    #[test]
    fn test_mmol_conversion() {
        let targets = bound_target_range(BgTargets {
            min_bg: 5.5,
            max_bg: 6.5,
            temptarget_set: false,
        });

        // Should be converted to mg/dL
        assert!(targets.min_bg > 90.0);
        assert!(targets.max_bg > 100.0);
    }

    #[test]
    fn test_safety_floor() {
        let targets = bound_target_range(BgTargets {
            min_bg: 60.0,
            max_bg: 70.0,
            temptarget_set: false,
        });

        // Should be raised to 80
        assert!((targets.min_bg - 80.0).abs() < 0.1);
        assert!((targets.max_bg - 80.0).abs() < 0.1);
    }

    #[test]
    fn test_safety_ceiling() {
        let targets = bound_target_range(BgTargets {
            min_bg: 250.0,
            max_bg: 300.0,
            temptarget_set: false,
        });

        // Should be lowered to 200
        assert!((targets.min_bg - 200.0).abs() < 0.1);
        assert!((targets.max_bg - 200.0).abs() < 0.1);
    }
}
