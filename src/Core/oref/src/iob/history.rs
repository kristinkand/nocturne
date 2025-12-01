//! Treatment history processing for IOB calculations
//!
//! This module processes pump history events and converts them into
//! insulin treatments that can be used for IOB calculations.

use chrono::{DateTime, Duration, Utc};
use crate::types::{Profile, Treatment};
use crate::Result;

/// Find insulin treatments from pump history
///
/// Processes pump history events and converts temp basals into discrete
/// insulin doses for IOB calculations.
///
/// # Arguments
/// * `profile` - User profile with basal settings
/// * `history` - Raw pump history events
/// * `clock` - Current time
/// * `zero_temp_duration` - If > 0, assume zero temp for this many minutes into future
///
/// # Returns
/// Vector of processed treatments ready for IOB calculation
pub fn find_insulin_treatments(
    profile: &Profile,
    history: &[Treatment],
    clock: DateTime<Utc>,
    zero_temp_duration: i32,
) -> Result<Vec<Treatment>> {
    let mut treatments = Vec::new();
    let now_millis = clock.timestamp_millis();

    // Get DIA in milliseconds
    let dia_hours = profile.effective_dia();
    let dia_ago = now_millis - (dia_hours * 60.0 * 60.0 * 1000.0) as i64;

    // Process each history event
    for event in history {
        let event_date = event.effective_date();

        // Skip events older than DIA
        if event_date < dia_ago {
            continue;
        }

        // Skip future events
        if event_date > now_millis {
            continue;
        }

        // Handle bolus events
        if let Some(insulin) = event.insulin {
            if insulin > 0.0 {
                treatments.push(Treatment {
                    insulin: Some(insulin),
                    date: event_date,
                    timestamp: event.timestamp.clone(),
                    started_at: event.started_at.clone().or_else(|| event.timestamp.clone()),
                    ..Default::default()
                });
            }
        }

        // Handle temp basal events - convert to discrete insulin doses
        if let (Some(rate), Some(duration)) = (event.rate, event.duration) {
            if duration > 0.0 {
                // Get scheduled basal rate
                let scheduled_basal = lookup_basal_at_time(profile, event_date);

                // Calculate net insulin per 5-minute interval
                let net_rate = rate - scheduled_basal;

                // Split temp basal into 5-minute chunks
                let chunks = (duration / 5.0).ceil() as i32;

                for chunk in 0..chunks {
                    let chunk_start = event_date + (chunk as i64 * 5 * 60 * 1000);

                    // Don't add chunks in the future
                    if chunk_start > now_millis {
                        break;
                    }

                    // Calculate insulin for this 5-minute chunk
                    let chunk_duration = if chunk == chunks - 1 {
                        // Last chunk might be partial
                        duration - (chunk as f64 * 5.0)
                    } else {
                        5.0
                    };

                    let chunk_insulin = net_rate * chunk_duration / 60.0;

                    if chunk_insulin.abs() > 0.0001 {
                        treatments.push(Treatment {
                            insulin: Some(chunk_insulin),
                            date: chunk_start,
                            ..Default::default()
                        });
                    }
                }
            }
        }
    }

    // If zero_temp_duration is specified, add zero temp basal into the future
    if zero_temp_duration > 0 {
        let scheduled_basal = profile.current_basal;
        let chunks = zero_temp_duration / 5;

        for chunk in 0..chunks {
            let chunk_start = now_millis + (chunk as i64 * 5 * 60 * 1000);
            let chunk_insulin = -scheduled_basal * 5.0 / 60.0;

            treatments.push(Treatment {
                insulin: Some(chunk_insulin),
                date: chunk_start,
                ..Default::default()
            });
        }
    }

    // Sort by date
    treatments.sort_by_key(|t| t.date);

    Ok(treatments)
}

/// Look up the scheduled basal rate at a specific time
fn lookup_basal_at_time(profile: &Profile, time_millis: i64) -> f64 {
    if profile.basal_profile.is_empty() {
        return profile.current_basal;
    }

    // Convert millis to datetime
    let dt = DateTime::from_timestamp_millis(time_millis)
        .unwrap_or_else(|| Utc::now());

    let now_minutes = dt.hour() * 60 + dt.minute();

    // Sort schedule by index
    let mut schedule = profile.basal_profile.clone();
    schedule.sort_by_key(|e| e.i);

    // Find the applicable rate
    let mut rate = schedule.last().map(|e| e.rate).unwrap_or(profile.current_basal);

    for i in 0..schedule.len() {
        let entry = &schedule[i];
        let next_minutes = if i + 1 < schedule.len() {
            schedule[i + 1].minutes
        } else {
            24 * 60 // End of day
        };

        if now_minutes >= entry.minutes && now_minutes < next_minutes {
            rate = entry.rate;
            break;
        }
    }

    rate
}

/// Split a temp basal that spans schedule changes
///
/// This handles cases where a temp basal runs across midnight or
/// when the scheduled basal rate changes during the temp.
pub fn split_temp_basal_at_schedule_changes(
    treatment: &Treatment,
    profile: &Profile,
) -> Vec<Treatment> {
    // For now, return the treatment as-is
    // Full implementation would split at schedule boundaries
    vec![treatment.clone()]
}

use chrono::Timelike;

#[cfg(test)]
mod tests {
    use super::*;

    fn make_profile() -> Profile {
        Profile {
            dia: 5.0,
            current_basal: 1.0,
            ..Default::default()
        }
    }

    #[test]
    fn test_find_bolus_treatments() {
        let now = Utc::now();
        let profile = make_profile();

        let history = vec![
            Treatment::bolus(2.0, now - Duration::hours(1)),
        ];

        let treatments = find_insulin_treatments(&profile, &history, now, 0).unwrap();

        assert_eq!(treatments.len(), 1);
        assert_eq!(treatments[0].insulin, Some(2.0));
    }

    #[test]
    fn test_filter_old_treatments() {
        let now = Utc::now();
        let profile = make_profile();

        // Bolus from 6 hours ago (beyond 5h DIA)
        let history = vec![
            Treatment::bolus(2.0, now - Duration::hours(6)),
        ];

        let treatments = find_insulin_treatments(&profile, &history, now, 0).unwrap();

        assert!(treatments.is_empty());
    }

    #[test]
    fn test_temp_basal_to_insulin() {
        let now = Utc::now();
        let profile = make_profile();

        // Temp basal of 2 U/hr for 30 min, scheduled basal is 1 U/hr
        // Net rate is 1 U/hr, so 0.5 U total over 30 min
        let history = vec![
            Treatment::temp_basal(2.0, 30.0, now - Duration::minutes(30)),
        ];

        let treatments = find_insulin_treatments(&profile, &history, now, 0).unwrap();

        // Should be split into 6 chunks (30 min / 5 min)
        assert!(treatments.len() >= 6);

        // Each chunk should have ~0.083 U (1 U/hr * 5/60 hr)
        let total: f64 = treatments.iter().map(|t| t.insulin.unwrap_or(0.0)).sum();
        assert!((total - 0.5).abs() < 0.01);
    }

    #[test]
    fn test_zero_temp_future() {
        let now = Utc::now();
        let profile = make_profile();

        // No history, but request 30 min zero temp projection
        let treatments = find_insulin_treatments(&profile, &[], now, 30).unwrap();

        // Should have 6 chunks of negative insulin
        assert_eq!(treatments.len(), 6);

        for t in &treatments {
            assert!(t.insulin.unwrap_or(0.0) < 0.0);
        }
    }
}
