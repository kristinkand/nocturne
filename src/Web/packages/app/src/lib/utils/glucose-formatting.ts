/**
 * Glucose Formatting Utilities
 *
 * Provides consistent glucose value formatting throughout the app,
 * respecting the user's unit preference (mg/dL or mmol/L).
 *
 * Use the convenience functions (bg(), bgDelta(), bgLabel(), bgRange()) for
 * automatic unit detection from global settings. These are the recommended
 * functions for most use cases.
 */

import { glucoseUnitsState } from "$lib/stores/glucose-units-store.svelte";

/** Supported glucose unit types */
export type GlucoseUnits = "mg/dl" | "mmol";

/** Conversion factor from mg/dL to mmol/L */
const MGDL_TO_MMOL = 18.01559;

// =============================================================================
// Core conversion functions (require explicit units parameter)
// =============================================================================

/**
 * Convert a glucose value from mg/dL to the specified units
 * @param mgdl - Glucose value in mg/dL
 * @param units - Target units ("mg/dl" or "mmol")
 * @returns Glucose value in the specified units
 */
export function convertToDisplayUnits(mgdl: number, units: GlucoseUnits): number {
  if (units === "mmol") {
    return Math.round((mgdl / MGDL_TO_MMOL) * 10) / 10;
  }
  return Math.round(mgdl);
}

/**
 * Format a glucose value for display with appropriate precision
 * @param mgdl - Glucose value in mg/dL
 * @param units - Display units ("mg/dl" or "mmol")
 * @returns Formatted glucose string
 */
export function formatGlucoseValue(mgdl: number, units: GlucoseUnits): string {
  const value = convertToDisplayUnits(mgdl, units);
  if (units === "mmol") {
    return value.toFixed(1);
  }
  return Math.round(value).toString();
}

/**
 * Format a glucose delta value for display
 * @param deltaMgdl - Delta value in mg/dL
 * @param units - Display units ("mg/dl" or "mmol")
 * @param includeSign - Whether to include +/- sign (default: true)
 * @returns Formatted delta string
 */
export function formatGlucoseDelta(
  deltaMgdl: number,
  units: GlucoseUnits,
  includeSign: boolean = true
): string {
  const value = convertToDisplayUnits(deltaMgdl, units);
  const sign = includeSign && value > 0 ? "+" : "";

  if (units === "mmol") {
    return `${sign}${value.toFixed(1)}`;
  }
  return `${sign}${Math.round(value)}`;
}

/**
 * Get the unit label for display
 * @param units - Units type
 * @returns Human-readable unit label
 */
export function getUnitLabel(units: GlucoseUnits): string {
  return units === "mmol" ? "mmol/L" : "mg/dL";
}

/**
 * Format a glucose range for display
 * @param lowMgdl - Low threshold in mg/dL
 * @param highMgdl - High threshold in mg/dL
 * @param units - Display units
 * @returns Formatted range string (e.g., "70-180 mg/dL" or "3.9-10.0 mmol/L")
 */
export function formatGlucoseRange(
  lowMgdl: number,
  highMgdl: number,
  units: GlucoseUnits
): string {
  const low = formatGlucoseValue(lowMgdl, units);
  const high = formatGlucoseValue(highMgdl, units);
  const label = getUnitLabel(units);
  return `${low}-${high} ${label}`;
}

// =============================================================================
// Convenience functions (auto-detect units from global preference)
// These are the recommended functions for most use cases.
// =============================================================================

/**
 * Format a glucose value using the global unit preference
 * @param mgdl - Glucose value in mg/dL
 * @returns Formatted glucose string in user's preferred units
 */
export function bg(mgdl: number): string {
  return formatGlucoseValue(mgdl, glucoseUnitsState.units);
}

/**
 * Format a glucose delta using the global unit preference
 * @param deltaMgdl - Delta value in mg/dL
 * @param includeSign - Whether to include +/- sign (default: true)
 * @returns Formatted delta string in user's preferred units
 */
export function bgDelta(deltaMgdl: number, includeSign: boolean = true): string {
  return formatGlucoseDelta(deltaMgdl, glucoseUnitsState.units, includeSign);
}

/**
 * Get the current unit label from global preference
 * @returns "mg/dL" or "mmol/L" based on user preference
 */
export function bgLabel(): string {
  return getUnitLabel(glucoseUnitsState.units);
}

/**
 * Format a glucose range using the global unit preference
 * @param lowMgdl - Low threshold in mg/dL
 * @param highMgdl - High threshold in mg/dL
 * @returns Formatted range string in user's preferred units
 */
export function bgRange(lowMgdl: number, highMgdl: number): string {
  return formatGlucoseRange(lowMgdl, highMgdl, glucoseUnitsState.units);
}

/**
 * Convert a mg/dL value to the user's preferred units
 * @param mgdl - Value in mg/dL
 * @returns Numeric value in user's preferred units
 */
export function bgValue(mgdl: number): number {
  return convertToDisplayUnits(mgdl, glucoseUnitsState.units);
}

/**
 * Get standard glucose range thresholds in user's preferred units
 * @returns Object with common threshold values
 */
export function bgThresholds(): {
  urgentLow: number;
  low: number;
  targetLow: number;
  targetHigh: number;
  high: number;
  urgentHigh: number;
} {
  const units = glucoseUnitsState.units;
  return {
    urgentLow: convertToDisplayUnits(54, units),
    low: convertToDisplayUnits(70, units),
    targetLow: convertToDisplayUnits(70, units),
    targetHigh: convertToDisplayUnits(180, units),
    high: convertToDisplayUnits(180, units),
    urgentHigh: convertToDisplayUnits(250, units),
  };
}

