<script lang="ts">
  import { LineChart } from "layerchart";
  import { scaleTime, scaleThreshold, scaleLinear } from "d3-scale";
  import { chartConfig } from "$lib/constants";
  import { DEFAULT_THRESHOLDS } from "$lib/constants";
  import type { Entry, GlycemicThresholds, Treatment } from "$lib/api";
  import type { WithElementRef } from "$lib/utils";
  import type { HTMLAttributes } from "svelte/elements";
  import type { DateRange } from "@layerstack/utils/dateRange";

  interface Props {
    entries: Entry[];
    treatments: Treatment[];
    /** Optional. If not provided, will be inferred from the supplied entries. */
    dateRange?: DateRange;
    thresholds?: GlycemicThresholds;
  }

  type ProcessedEntry = Entry & { _timestamp: number };

  let {
    // Forwarded element ref for consumers if needed
    ref = $bindable(null),
    // Allow consumers to provide custom root classes using the `class` attribute
    class: _className,
    entries,
    treatments,
    dateRange,
    thresholds = DEFAULT_THRESHOLDS,
    // Collect any additional props so they can be forwarded to the root element
    ..._restProps
  }: WithElementRef<HTMLAttributes<HTMLElement>> & Props = $props();

  // Performance thresholds
  const CANVAS_THRESHOLD = 500; // Switch to canvas above this many points
  const LARGE_DATASET_THRESHOLD = 2000; // Disable expensive features above this

  // Determine if this is a large dataset for performance optimizations
  const isLargeDataset = $derived(entries.length > LARGE_DATASET_THRESHOLD);
  const useCanvas = $derived(entries.length > CANVAS_THRESHOLD);

  // Helper to get timestamp as number (faster than creating Date objects)
  function getTimestamp(d: Entry): number {
    if (d.mills && d.mills > 0) return d.mills;
    if (d.dateString) return new Date(d.dateString).getTime();
    if (d.created_at) return new Date(d.created_at).getTime();
    return 0;
  }

  // Pre-process entries to extract timestamps once (avoid repeated Date object creation)
  const processedEntries = $derived(
    entries.map(
      (e): ProcessedEntry => ({
        ...e,
        _timestamp: getTimestamp(e),
      })
    )
  );

  // Resolve the effective date range – compute min/max in single pass
  const resolvedDateRange: DateRange = $derived(
    dateRange
      ? dateRange
      : (() => {
          if (processedEntries.length === 0) {
            const now = new Date();
            return { from: now, to: now };
          }

          let minTime = Infinity;
          let maxTime = -Infinity;
          for (const d of processedEntries) {
            if (d._timestamp < minTime) minTime = d._timestamp;
            if (d._timestamp > maxTime) maxTime = d._timestamp;
          }

          return { from: new Date(minTime), to: new Date(maxTime) };
        })()
  );

  const insulinToCarbRatio = 12; // Hardcoded ratio

  // Create D3 scale for insulin to carb ratio
  const insulinScale = scaleLinear()
    .domain([0, 1]) // 1 unit of insulin
    .range([0, insulinToCarbRatio]); // maps to 12 carbs

  // Scale insulin values by the insulin-to-carb ratio using D3 scale
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const _scaledTreatments = $derived(
    treatments.map((treatment) => ({
      ...treatment,
      insulin: treatment.insulin
        ? insulinScale(treatment.insulin)
        : treatment.insulin,
    }))
  );

  const xScale = $derived(
    scaleTime().domain([
      resolvedDateRange.from ?? new Date(),
      resolvedDateRange.to ?? new Date(),
    ])
  );

  // Optimized x accessor using pre-computed timestamp
  function getX(d: ProcessedEntry) {
    return new Date(d._timestamp);
  }

  // Format timestamp for tooltip labels
  function formatTimestamp(d: ProcessedEntry) {
    return new Date(d._timestamp).toLocaleTimeString();
  }
</script>

<!-- <ChartC.Container config={chartConfig} class="h-fit w-full grid grid-stack"> -->
<!-- <div bind:this={ref} class={cn("w-full", className)} {...restProps}> -->
<LineChart
  data={processedEntries}
  x={getX}
  y={"sgv"}
  c="sgv"
  renderContext={useCanvas ? "canvas" : "svg"}
  yBaseline={0}
  {xScale}
  xBaseline={resolvedDateRange.to!.getTime()}
  cScale={scaleThreshold()}
  brush
  props={{
    points: isLargeDataset ? { r: 0 } : { r: 3 },
    highlight: { motion: isLargeDataset ? false : undefined },
    tooltip: {
      root: { motion: isLargeDataset ? false : undefined },
    },
  }}
  labels={isLargeDataset
    ? undefined
    : {
        x: formatTimestamp,
      }}
  cDomain={[
    thresholds.low,
    thresholds.targetBottom,
    thresholds.targetTop,
    thresholds.high,
  ]}
  cRange={Object.values(chartConfig).map((c) => c.color)}
  tooltip={isLargeDataset ? undefined : {}}
  annotations={[
    {
      type: "line",
      y: thresholds.high,
      label: `High (${thresholds.high})`,
      props: {
        label: { class: "text-xs" },
        line: {
          class: "[stroke-dasharray:2,2] stroke-high-bg",
          color: chartConfig.high.color,
        },
      },
    },
    {
      type: "line",
      y: thresholds.low,
      label: `Low (${thresholds.low})`,
      props: {
        label: { class: "text-xs" },
        line: { class: "[stroke-dasharray:2,2] stroke-low-bg" },
      },
    },
  ]}
  padding={{ top: 20, right: 30, bottom: 40, left: 50 }}
></LineChart>
<!-- <Bar x={(d) => getDateString(d)} y={"carbs"} data={scaledTreatments} /> -->
<!-- </div> -->
<!-- </ChartC.Container> -->
