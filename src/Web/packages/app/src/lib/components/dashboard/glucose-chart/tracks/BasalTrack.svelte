<script lang="ts">
  import {
    Area,
    Spline,
    Axis,
    Text,
    Group,
    ChartClipPath,
    AnnotationRange,
    AnnotationLine,
    AnnotationPoint,
  } from "layerchart";
  import { curveStepAfter } from "d3";
  import type { ScaleLinear } from "d3-scale";

  interface BasalDataPoint {
    time: Date;
    rate: number;
    scheduledRate?: number;
    isTemp?: boolean;
  }

  interface TempBasalSpan {
    id: string;
    displayStart: Date;
    displayEnd: Date;
    color: string;
    rate: number | null;
    percent: number | null;
  }

  interface StaleBasalData {
    start: Date;
    end: Date;
  }

  interface Props {
    basalData: BasalDataPoint[];
    scheduledBasalData: { time: Date; rate: number }[];
    tempBasalSpans: TempBasalSpan[];
    staleBasalData: StaleBasalData | null;
    maxBasalRate: number;
    basalScale: (rate: number) => number;
    basalZero: number;
    basalTrackTop: number;
    basalAxisScale: ScaleLinear<number, number>;
    context: { xScale: (time: Date) => number; yScale: (value: number) => number };
    showBasal: boolean;
  }

  let {
    basalData,
    scheduledBasalData,
    tempBasalSpans,
    staleBasalData,
    maxBasalRate,
    basalScale,
    basalZero,
    basalTrackTop,
    basalAxisScale,
    context,
    showBasal,
  }: Props = $props();

  // Group consecutive temp basal points into segments
  const tempBasalSegments = $derived.by(() => {
    const segments: BasalDataPoint[][] = [];
    let currentSegment: BasalDataPoint[] = [];

    for (const point of basalData) {
      if (point.isTemp) {
        currentSegment.push(point);
      } else {
        if (currentSegment.length > 0) {
          segments.push(currentSegment);
          currentSegment = [];
        }
      }
    }
    if (currentSegment.length > 0) {
      segments.push(currentSegment);
    }
    return segments;
  });
</script>

{#if showBasal}
  <ChartClipPath>
    <!-- Temp basal span indicators (shown in basal track when basal is visible) -->
    {#each tempBasalSpans as span (span.id)}
      <AnnotationRange
        x={[span.displayStart.getTime(), span.displayEnd.getTime()]}
        y={[basalScale(maxBasalRate * 0.9), basalScale(maxBasalRate * 0.7)]}
        fill={span.color}
        class="opacity-40"
      />
      <!-- Show temp basal rate label -->
      {#if span.rate !== null}
        <Group
          x={context.xScale(span.displayStart)}
          y={context.yScale(basalScale(maxBasalRate * 0.8))}
        >
          <Text x={4} y={0} class="text-[7px] fill-insulin-basal font-medium">
            {span.rate.toFixed(2)}U/h
          </Text>
        </Group>
      {:else if span.percent !== null}
        <Group
          x={context.xScale(span.displayStart)}
          y={context.yScale(basalScale(maxBasalRate * 0.8))}
        >
          <Text x={4} y={0} class="text-[7px] fill-insulin-basal font-medium">
            {span.percent}%
          </Text>
        </Group>
      {/if}
    {/each}
  </ChartClipPath>

  <!-- Stale basal data indicator -->
  {#if staleBasalData}
    <ChartClipPath>
      <AnnotationRange
        x={[staleBasalData.start.getTime(), staleBasalData.end.getTime()]}
        y={[basalScale(maxBasalRate), basalZero]}
        pattern={{
          size: 8,
          lines: {
            rotate: -45,
            opacity: 0.1,
          },
        }}
      />
    </ChartClipPath>
    <AnnotationLine
      x={staleBasalData.start}
      class="stroke-yellow-500/50 stroke-1"
      stroke-dasharray="2,2"
    />
    <AnnotationPoint
      x={staleBasalData.start.getTime()}
      y={basalScale(maxBasalRate)}
      label="Last pump sync"
      labelPlacement="bottom-right"
      fill="yellow"
      class="hover:bg-background hover:text-foreground"
    />
  {/if}

  <!-- Scheduled basal rate line -->
  {#if scheduledBasalData.length > 0}
    <Spline
      data={scheduledBasalData}
      x={(d) => d.time}
      y={(d) => basalScale(d.rate)}
      curve={curveStepAfter}
      class="stroke-muted-foreground/50 stroke-1 fill-none"
      stroke-dasharray="4,4"
    />
  {/if}

  <!-- Basal axis on right -->
  <Axis
    placement="right"
    scale={basalAxisScale}
    ticks={2}
    tickLabelProps={{
      class: "text-[9px] fill-muted-foreground",
    }}
  />

  <!-- Basal track label -->
  <Text
    x={4}
    y={basalTrackTop + 12}
    class="text-[8px] fill-muted-foreground font-medium"
  >
    BASAL
  </Text>

  <!-- Basal area - split into scheduled and temp basal layers -->
  {#if basalData.length > 0}
    <!-- Scheduled basal rate (background layer) -->
    <Area
      data={basalData}
      x={(d) => d.time}
      y0={() => basalZero}
      y1={(d) => basalScale(d.scheduledRate ?? d.rate)}
      curve={curveStepAfter}
      fill="var(--insulin-basal)"
      class="stroke-insulin stroke-1"
    />
    <!-- Temp basal overlay (only where isTemp is true) -->
    {#each tempBasalSegments as segment, i (i)}
      <Area
        data={segment}
        x={(d) => d.time}
        y0={() => basalZero}
        y1={(d) => basalScale(d.rate)}
        curve={curveStepAfter}
        fill="var(--insulin-temp-basal)"
        class="stroke-insulin-bolus stroke-1"
      />
    {/each}
  {/if}
{/if}
