//! Insulin Sensitivity Factor (ISF) schedule lookups

use chrono::{DateTime, Timelike, Utc};
use crate::types::{ISFProfile, Profile};

/// Look up the ISF at a specific time
pub fn isf_lookup(profile: &Profile, time: DateTime<Utc>) -> f64 {
    isf_lookup_from_schedule(&profile.isf_profile, time)
        .unwrap_or(profile.sens)
}

/// Look up ISF from a specific schedule
pub fn isf_lookup_from_schedule(isf_profile: &ISFProfile, time: DateTime<Utc>) -> Option<f64> {
    if isf_profile.sensitivities.is_empty() {
        return None;
    }

    let now_minutes = time.hour() * 60 + time.minute();

    // Sort by offset
    let mut schedule: Vec<_> = isf_profile.sensitivities.iter().collect();
    schedule.sort_by_key(|e| e.offset);

    // Check first entry starts at midnight
    if schedule[0].offset != 0 {
        return None;
    }

    // Find applicable entry
    let mut isf_entry = schedule.last().unwrap();

    for i in 0..schedule.len() {
        let entry = &schedule[i];
        let next_offset = if i + 1 < schedule.len() {
            schedule[i + 1].offset
        } else {
            24 * 60
        };

        if now_minutes >= entry.offset && now_minutes < next_offset {
            isf_entry = entry;
            break;
        }
    }

    Some(isf_entry.sensitivity)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::types::ISFEntry;
    use chrono::TimeZone;

    fn make_profile_with_isf_schedule() -> Profile {
        Profile {
            sens: 50.0,
            isf_profile: ISFProfile {
                sensitivities: vec![
                    ISFEntry { offset: 0, sensitivity: 45.0, end_offset: None },
                    ISFEntry { offset: 360, sensitivity: 50.0, end_offset: None }, // 06:00
                    ISFEntry { offset: 1080, sensitivity: 55.0, end_offset: None }, // 18:00
                ],
            },
            ..Default::default()
        }
    }

    #[test]
    fn test_isf_lookup_night() {
        let profile = make_profile_with_isf_schedule();
        let time = Utc.with_ymd_and_hms(2024, 1, 1, 3, 0, 0).unwrap();

        let isf = isf_lookup(&profile, time);
        assert!((isf - 45.0).abs() < 0.1);
    }

    #[test]
    fn test_isf_lookup_day() {
        let profile = make_profile_with_isf_schedule();
        let time = Utc.with_ymd_and_hms(2024, 1, 1, 12, 0, 0).unwrap();

        let isf = isf_lookup(&profile, time);
        assert!((isf - 50.0).abs() < 0.1);
    }

    #[test]
    fn test_isf_lookup_evening() {
        let profile = make_profile_with_isf_schedule();
        let time = Utc.with_ymd_and_hms(2024, 1, 1, 20, 0, 0).unwrap();

        let isf = isf_lookup(&profile, time);
        assert!((isf - 55.0).abs() < 0.1);
    }

    #[test]
    fn test_empty_schedule_uses_default() {
        let profile = Profile {
            sens: 42.0,
            isf_profile: ISFProfile::default(),
            ..Default::default()
        };

        let isf = isf_lookup(&profile, Utc::now());
        assert!((isf - 42.0).abs() < 0.1);
    }
}
