//! Utility functions

mod round;
mod time;

pub use round::{round_basal, round_value};
pub use time::{parse_timestamp, format_timestamp};


/// Round a value to a specific number of decimal places
pub fn round(value: f64, digits: u32) -> f64 {
    let scale = 10_f64.powi(digits as i32);
    (value * scale).round() / scale
}

/// Calculate percentile of a sorted array
pub fn percentile(data: &[f64], p: f64) -> Option<f64> {
    if data.is_empty() {
        return None;
    }

    let n = data.len();

    if n == 1 {
        return Some(data[0]);
    }

    let index = (p / 100.0) * (n - 1) as f64;
    let lower = index.floor() as usize;
    let upper = index.ceil() as usize;

    if lower == upper {
        Some(data[lower])
    } else {
        let weight = index - lower as f64;
        Some(data[lower] * (1.0 - weight) + data[upper] * weight)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_round() {
        assert!((round(1.2345, 2) - 1.23).abs() < 0.001);
        assert!((round(1.2345, 3) - 1.235).abs() < 0.0001);
        assert!((round(1.5, 0) - 2.0).abs() < 0.001);
    }

    #[test]
    fn test_percentile() {
        let data = vec![1.0, 2.0, 3.0, 4.0, 5.0];

        assert!((percentile(&data, 0.0).unwrap() - 1.0).abs() < 0.001);
        assert!((percentile(&data, 50.0).unwrap() - 3.0).abs() < 0.001);
        assert!((percentile(&data, 100.0).unwrap() - 5.0).abs() < 0.001);
    }

    #[test]
    fn test_percentile_empty() {
        let data: Vec<f64> = vec![];
        assert!(percentile(&data, 50.0).is_none());
    }
}
