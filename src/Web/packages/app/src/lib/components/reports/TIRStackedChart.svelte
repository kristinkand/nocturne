<script lang="ts">
  import {
    DEFAULT_CONFIG,
    type ExtendedAnalysisConfig,
  } from "$lib/utils/glucose-analytics.ts";
  import { BarChart, Tooltip } from "layerchart";
  import { calculateTimeInRange as calculateTimeInRangeRemote } from "$lib/data/statistics.remote.ts";
  import type { Entry } from "$lib/api";

  interface TimeInRangePercentages {
    severeLow?: number;
    low?: number;
    target?: number;
    high?: number;
    severeHigh?: number;
  }

  let {
    entries,
    config,
    percentages,
  }: {
    entries: Entry[];
    config?: ExtendedAnalysisConfig;
    /** Pre-computed percentages - when provided, skips API call */
    percentages?: TimeInRangePercentages;
  } = $props();

  const mergedConfig = {
    ...DEFAULT_CONFIG,
    ...config,
  };

  // Only fetch from API if percentages not provided
  const timeInRangePromise = $derived(
    percentages
      ? null
      : calculateTimeInRangeRemote({
          entries,
          config: mergedConfig,
        })
  );

  const timeInRange = $derived.by(() => {
    // Use pre-fetched percentages if provided
    if (percentages) {
      return {
        percentages: {
          severeLow: percentages.severeLow ?? 0,
          low: percentages.low ?? 0,
          target: percentages.target ?? 0,
          high: percentages.high ?? 0,
          severeHigh: percentages.severeHigh ?? 0,
        },
      };
    }

    // Extract percentages from the backend response
    if (!timeInRangePromise) return { percentages: {} };

    // Access the resolved value via .current
    const resolved = timeInRangePromise.current;
    if (!resolved) return { percentages: {} };

    return {
      percentages: {
        severeLow: resolved.percentages?.severeLow ?? 0,
        low: resolved.percentages?.low ?? 0,
        target: resolved.percentages?.target ?? 0,
        high: resolved.percentages?.high ?? 0,
        severeHigh: resolved.percentages?.severeHigh ?? 0,
      },
    };
  });

  const chartConfig = {
    severeLow: {
      key: "severeLow",
      label: "Severe Low",
      color: "var(--glucose-very-low)",
    },
    low: {
      key: "low",
      label: "Low",
      color: "var(--glucose-low)",
    },
    target: {
      key: "target",
      label: "Target",
      color: "var(--glucose-in-range)",
    },
    high: {
      key: "high",
      label: "High",
      color: "var(--glucose-high)",
    },
    severeHigh: {
      key: "severeHigh",
      label: "Severe High",
      color: "var(--glucose-very-high)",
    },
  };
</script>

<BarChart
  data={[timeInRange.percentages]}
  yDomain={[0, 100]}
  x={() => 1}
  yBaseline={undefined}
  yNice={false}
  series={Object.values(chartConfig).map((c) => ({
    key: c.key,
    color: c.color,
    label: c.label,
  }))}
  legend
  seriesLayout="stack"
  padding={{ top: 0, bottom: 24 }}
  props={{
    bars: {
      motion: { type: "tween", duration: 200 },
      strokeWidth: 0,
    },

    tooltip: {
      hideTotal: true,
      context: { mode: "bounds" },
      // header: { format: "none" },
    },
    xAxis: {
      hidden: true,
    },
    yAxis: {
      hidden: true,
    },
  }}
>
  {#snippet tooltip({ context: _ })}
    <Tooltip.Root>
      {#snippet children({ data })}
        <Tooltip.List>
          <Tooltip.Item label="Label:" value={data.label} />
          <!-- <Tooltip.Item label="Range:" value="{data.start} - {data.end}" /> -->
        </Tooltip.List>
      {/snippet}
    </Tooltip.Root>
  {/snippet}
</BarChart>
