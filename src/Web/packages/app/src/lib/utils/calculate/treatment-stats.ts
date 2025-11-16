/**
 * Formats an insulin value for display.
 * @param insulin The insulin value.
 * @returns The formatted insulin string.
 */
export function formatInsulinDisplay(insulin: number | undefined): string {
  if (insulin === undefined || insulin === null) {
    return "N/A";
  }
  return insulin.toFixed(2);
}

/**
 * Formats a carb value for display.
 * @param carbs The carb value.
 * @returns The formatted carb string.
 */
export function formatCarbDisplay(carbs: number | undefined): string {
  if (carbs === undefined || carbs === null) {
    return "N/A";
  }
  return carbs.toFixed(0);
}

export interface TreatmentSummary {
  totalInsulin: number;
  totalCarbs: number;
  count: number;
}
