// Utility functions

export function scaleBG(bg: number, units: string): number {
  if (units === "mmol") {
    return Math.round((bg / 18.01559) * 10) / 10;
  }
  return bg;
}
