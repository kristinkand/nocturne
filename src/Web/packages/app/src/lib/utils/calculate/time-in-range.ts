import type { Entry } from "$lib/api";
import type { ExtendedAnalysisConfig } from "../glucose-analytics";

export function calculateTimeInRange(entries: Entry[], config: ExtendedAnalysisConfig) {
  const percentages = {
    severeLow: 0,
    low: 0,
    target: 0,
    high: 0,
    severeHigh: 0,
  };

  if (!entries || entries.length === 0) {
    return { percentages };
  }

  let severeLowCount = 0;
  let lowCount = 0;
  let targetCount = 0;
  let highCount = 0;
  let severeHighCount = 0;

  for (const entry of entries) {
    if (!entry.sgv) continue;

    if (entry.sgv < config.severeLow) {
      severeLowCount++;
    } else if (entry.sgv < config.low) {
      lowCount++;
    } else if (entry.sgv <= config.target) {
      targetCount++;
    } else if (entry.sgv <= config.high) {
      highCount++;
    } else {
      severeHighCount++;
    }
  }

  const total = entries.length;
  percentages.severeLow = (severeLowCount / total) * 100;
  percentages.low = (lowCount / total) * 100;
  percentages.target = (targetCount / total) * 100;
  percentages.high = (highCount / total) * 100;
  percentages.severeHigh = (severeHighCount / total) * 100;

  return { percentages };
}
