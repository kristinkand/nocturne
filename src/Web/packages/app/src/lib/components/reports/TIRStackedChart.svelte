<script lang="ts">
  import { Chart, Svg, Bars, Text, Tooltip, Line } from "layerchart";
  import { scaleBand, scaleLinear } from "d3-scale";

  // Minimum percentage to render as filled bar (below this = outline only)

  // Minimum bar height in percentage points for tiny values
  const MIN_BAR_PERCENT = 5;

  interface TimeInRangePercentages {
    severeLow?: number;
    low?: number;
    target?: number;
    high?: number;
    severeHigh?: number;
  }

  interface Props {
    /** Pre-computed percentages - required to avoid reactive API calls */
    percentages?: TimeInRangePercentages;
    /** Thresholds for the glucose ranges in mg/dL */
    thresholds?: {
      severeLow: number;
      low: number;
      high: number;
      severeHigh: number;
    };
  }

  let {
    percentages,
    thresholds = { severeLow: 54, low: 70, high: 180, severeHigh: 250 },
  }: Props = $props();

  // Range keys in stacking order (bottom to top)
  const rangeKeys = [
    "severeLow",
    "low",
    "target",
    "high",
    "severeHigh",
  ] as const;
  type RangeKey = (typeof rangeKeys)[number];

  // Color mapping
  const colorMap: Record<RangeKey, string> = {
    severeLow: "var(--glucose-very-low)",
    low: "var(--glucose-low)",
    target: "var(--glucose-in-range)",
    high: "var(--glucose-high)",
    severeHigh: "var(--glucose-very-high)",
  };

  const labelMap: Record<RangeKey, string> = {
    severeLow: "Very Low",
    low: "Low",
    target: "In Range",
    high: "High",
    severeHigh: "Very High",
  };

  // Normalized percentages
  const pct = $derived({
    severeLow: percentages?.severeLow ?? 0,
    low: percentages?.low ?? 0,
    target: percentages?.target ?? 0,
    high: percentages?.high ?? 0,
    severeHigh: percentages?.severeHigh ?? 0,
  });

  // Transform data for stacked bar chart - one row per range with cumulative y0/y1
  // Tiny values get minimum height and outline-only styling
  const stackedData = $derived.by(() => {
    let cumulative = 0;
    return rangeKeys.map((key) => {
      const value = pct[key];
      const isTiny = value < MIN_BAR_PERCENT;
      const displayHeight = isTiny ? MIN_BAR_PERCENT : value;
      const y0 = cumulative;
      cumulative += displayHeight;
      const color = colorMap[key];

      return {
        category: "TIR",
        range: key,
        value,
        y0,
        y1: cumulative,
        color,
        label: labelMap[key],
        isTiny,
      };
    });
  });

  // Summary data for labels
  const rangeData = $derived(
    rangeKeys.map((key) => ({
      key,
      value: pct[key],
      color: colorMap[key],
      label: labelMap[key],
    }))
  );

  // Total display height (may exceed 100 if tiny values are expanded)
  const totalDisplayHeight = $derived(
    stackedData.length > 0 ? stackedData[stackedData.length - 1].y1 : 100
  );

  // Calculate positions for threshold labels (using display positions from stackedData)
  const thresholdPositions = $derived.by(() => {
    const thresholdValues = [
      thresholds.severeLow,
      thresholds.low,
      thresholds.high,
      thresholds.severeHigh,
    ];
    // Use the y1 of each segment (except the last) as the position
    return stackedData.slice(0, -1).map((segment, i) => ({
      key: segment.range,
      position: segment.y1,
      threshold: thresholdValues[i],
    }));
  });
</script>

<div class="relative h-full w-full">
  <Chart
    data={stackedData}
    x="category"
    xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
    y={["y0", "y1"]}
    yScale={scaleLinear()}
    yDomain={[0, totalDisplayHeight]}
    c="range"
    cDomain={[...rangeKeys]}
    cRange={rangeKeys.map((k) => colorMap[k])}
    padding={{ top: 8, bottom: 8, left: 0, right: 100 }}
    tooltip={{ mode: "band" }}
  >
    {#snippet children({ context })}
      <Svg>
        <Bars rx={4} strokeWidth={2} stroke="inherit" />

        <!-- Threshold labels at boundaries (on the bar) -->
        {#each thresholdPositions as tp}
          {@const yPos =
            context.height -
            (tp.position / totalDisplayHeight) * context.height}

          <Text
            x={context.width - 64}
            y={yPos}
            textAnchor="end"
            verticalAnchor="middle"
            class="fill-muted-foreground text-xs tabular-nums"
            value={`${tp.threshold}`}
          />
        {/each}

        <!-- Spline connectors from bar to percentage labels -->
        {#each stackedData as segment}
          {@const midpoint = (segment.y0 + segment.y1) / 2}
          {@const yPos =
            context.height - (midpoint / totalDisplayHeight) * context.height}

          <Line
            y1={yPos}
            x1={context.width - 12}
            x2={context.width - 82}
            y2={yPos}
            stroke={segment.color}
            strokeWidth={1}
            x="x"
            y="y"
            stroke-dasharray="2,2"
          />
        {/each}

        <!-- Percentage labels on the right side -->
        {#each stackedData as segment}
          {@const midpoint = (segment.y0 + segment.y1) / 2}
          {@const yPos =
            context.height - (midpoint / totalDisplayHeight) * context.height}

          <Text
            x={context.width - 8}
            y={yPos}
            textAnchor="start"
            verticalAnchor="middle"
            class={[
              "tabular-nums",
              segment.range === "target"
                ? "fill-foreground text-2xl font-bold"
                : "fill-muted-foreground text-sm",
            ].join(" ")}
            value={`${Math.round(segment.value)}%`}
          />
        {/each}
      </Svg>

      <!-- Tooltip -->
      <Tooltip.Root>
        {#snippet children({ data: _data })}
          <Tooltip.List>
            {#each rangeData.toReversed() as range}
              <Tooltip.Item
                label={range.label}
                format="percent"
                value={range.value / 100}
                color={range.color}
              />
            {/each}
          </Tooltip.List>
        {/snippet}
      </Tooltip.Root>
    {/snippet}
  </Chart>
</div>
