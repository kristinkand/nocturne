/**
 * Treatment Formatting Utilities Centralized formatting functions for treatment
 * display
 */

import type { Treatment } from "$lib/api";

/** Formats a timestamp to display full date and time */
export function formatDateTime(dateStr: string | undefined): string {
  if (!dateStr) return "-";
  const date = new Date(dateStr);
  return date.toLocaleDateString() + " " + date.toLocaleTimeString();
}

/** Formats glucose reading with measurement method */
export function formatGlucose(treatment: Treatment): string {
  if (treatment.glucose && treatment.glucose > 0) {
    let glucoseStr = treatment.glucose.toString();
    if (treatment.glucoseType) {
      glucoseStr += ` (${treatment.glucoseType})`;
    }
    return glucoseStr;
  }
  return "-";
}

/** Formats event type with optional reason */
export function formatEventType(treatment: Treatment): string {
  let result = treatment.eventType || "Unknown";

  if (treatment.reason) {
    result += ` - ${treatment.reason}`;
  }

  return result;
}

/** Formats notes and entered by information */
export function formatNotes(treatment: Treatment): string {
  const parts: string[] = [];

  if (treatment.notes) {
    parts.push(treatment.notes);
  }

  if (treatment.enteredBy) {
    parts.push(`by ${treatment.enteredBy}`);
  }

  return parts.join(" ");
}
