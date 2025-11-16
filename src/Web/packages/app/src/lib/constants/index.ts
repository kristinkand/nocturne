/** Central exports for all constants */

export * from "./tir-colors";
export { default as TIR_COLORS } from "./tir-colors";
import type { GlycemicThresholds } from "../api";
import { TIR_COLORS_CSS } from "./tir-colors";

export interface CompressionLowConfig {
  enabled: boolean;
  threshold: number;
  duration: number;
  recovery: number;
}

export const DEFAULT_THRESHOLDS: GlycemicThresholds = {
  low: 55,
  targetBottom: 80,
  targetTop: 140,
  tightTargetBottom: 80,
  tightTargetTop: 120,
  high: 180,
  severeLow: 40,
  severeHigh: 250,
};

export const DEFAULT_COMPRESSION_CONFIG: CompressionLowConfig = {
  enabled: true,
  threshold: 40,
  duration: 15,
  recovery: 70,
};

export const DEFAULT_CONFIG = {
  thresholds: DEFAULT_THRESHOLDS,
  sensorType: "GENERIC_5MIN",
  compressionLowConfig: DEFAULT_COMPRESSION_CONFIG,
  includeLoopingMetrics: false,
  units: "mg/dl",
};

export const chartConfig = {
  severeLow: {
    threshold: DEFAULT_THRESHOLDS.severeLow,
    label: "Severe Low",
    color: TIR_COLORS_CSS.severeLow,
  },
  low: {
    threshold: DEFAULT_THRESHOLDS.low,
    label: "Low",
    color: TIR_COLORS_CSS.low,
  },
  target: {
    threshold: DEFAULT_THRESHOLDS.targetBottom,
    label: "Target",
    color: TIR_COLORS_CSS.target,
  },
  high: {
    threshold: DEFAULT_THRESHOLDS.high,
    label: "High",
    color: TIR_COLORS_CSS.high,
  },
  severeHigh: {
    threshold: DEFAULT_THRESHOLDS.severeHigh,
    label: "Severe High",
    color: TIR_COLORS_CSS.severeHigh,
  },
};
