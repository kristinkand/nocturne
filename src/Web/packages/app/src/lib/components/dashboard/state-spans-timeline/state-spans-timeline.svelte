<script lang="ts">
  import { Chart, Svg, Axis, Rect, Text, Group, Area, Spline } from "layerchart";
  import { scaleTime, scaleLinear } from "d3-scale";
  import { curveStepAfter } from "d3-shape";
  import { PumpModeIcon, ActivityCategoryIcon } from "$lib/components/icons";
  import type { ProcessedSpan } from "../../../../routes/time-spans/data.remote";

  interface TrackConfig {
    key: string;
    label: string;
    spans: ProcessedSpan[];
    visible: boolean;
  }

  interface Props {
    pumpModeSpans: ProcessedSpan[];
    profileSpans: ProcessedSpan[];
    tempBasalSpans: ProcessedSpan[];
    overrideSpans: ProcessedSpan[];
    activitySpans: ProcessedSpan[];
    dateRange: { from: Date; to: Date };
    showPumpModes: boolean;
    showProfiles: boolean;
    showTempBasals: boolean;
    showOverrides: boolean;
    showActivities: boolean;
  }

  let {
    pumpModeSpans,
    profileSpans,
    tempBasalSpans,
    overrideSpans,
    activitySpans,
    dateRange,
    showPumpModes,
    showProfiles,
    showTempBasals,
    showOverrides,
    showActivities,
  }: Props = $props();

  // Build track configuration based on visibility (excluding temp basal which is handled separately)
  const standardTracks = $derived.by(() => {
    const allTracks: TrackConfig[] = [
      { key: "pumpMode", label: "PUMP MODE", spans: pumpModeSpans, visible: showPumpModes },
      { key: "profile", label: "PROFILE", spans: profileSpans, visible: showProfiles },
      { key: "override", label: "OVERRIDE", spans: overrideSpans, visible: showOverrides },
      { key: "activity", label: "ACTIVITY", spans: activitySpans, visible: showActivities },
    ];
    return allTracks.filter((t) => t.visible);
  });

  // Track height in pixels
  const TRACK_HEIGHT = 40;
  const BASAL_TRACK_HEIGHT = 60;
  const LABEL_WIDTH = 90;

  // Calculate total chart height based on visible tracks
  const chartHeight = $derived.by(() => {
    let height = standardTracks.length * TRACK_HEIGHT + 30;
    if (showTempBasals) {
      height += BASAL_TRACK_HEIGHT;
    }
    return Math.max(height, 100);
  });

  // Convert basal spans to data points for area chart
  const basalDataPoints = $derived.by(() => {
    if (!tempBasalSpans || tempBasalSpans.length === 0) return [];

    // Create step data from spans
    const points: { time: Date; rate: number }[] = [];

    for (const span of tempBasalSpans) {
      const rate = span.rate ?? 0;
      // Add point at start
      points.push({ time: span.startTime, rate });
      // Add point just before end (for step visualization)
      points.push({ time: new Date(span.endTime.getTime() - 1), rate });
    }

    // Sort by time
    points.sort((a, b) => a.time.getTime() - b.time.getTime());

    return points;
  });

  // Calculate max basal rate for y-scale
  const maxBasalRate = $derived.by(() => {
    if (basalDataPoints.length === 0) return 2;
    const max = Math.max(...basalDataPoints.map((p) => p.rate));
    return Math.max(max * 1.2, 0.5); // At least 0.5, with 20% headroom
  });

  // Calculate basal track position
  const basalTrackTop = $derived(standardTracks.length * TRACK_HEIGHT + 5);

  // Hovered span for tooltip
  let hoveredSpan = $state<ProcessedSpan | null>(null);
  let tooltipX = $state(0);
  let tooltipY = $state(0);

  // Format duration for tooltip
  function formatDuration(start: Date, end: Date): string {
    const ms = end.getTime() - start.getTime();
    const hours = Math.floor(ms / (1000 * 60 * 60));
    const minutes = Math.floor((ms % (1000 * 60 * 60)) / (1000 * 60));
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }

  // Format time for tooltip
  function formatTime(date: Date): string {
    return date.toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  }
</script>

<div class="relative w-full" style="height: {chartHeight}px;">
  {#if standardTracks.length === 0 && !showTempBasals}
    <div class="flex h-full items-center justify-center text-sm text-muted-foreground">
      No tracks selected. Enable at least one category above.
    </div>
  {:else}
    <Chart
      data={basalDataPoints}
      x={(d) => d.time}
      y={(d) => d.rate}
      xScale={scaleTime()}
      yScale={scaleLinear()}
      xDomain={[dateRange.from, dateRange.to]}
      yDomain={[0, maxBasalRate]}
      padding={{ top: 5, right: 10, bottom: 25, left: LABEL_WIDTH }}
    >
      <Svg>
        <!-- Standard category tracks (non-basal) -->
        {#each standardTracks as track, i (track.key)}
          {@const yPos = i * TRACK_HEIGHT + 5}
          <!-- Track background -->
          <Rect
            x={0}
            y={yPos}
            width="100%"
            height={TRACK_HEIGHT - 2}
            fill="var(--muted)"
            class="opacity-20"
          />
          <!-- Track label -->
          <Text
            x={-LABEL_WIDTH + 8}
            y={yPos + TRACK_HEIGHT / 2 + 4}
            class="text-[10px] fill-muted-foreground font-medium"
          >
            {track.label}
          </Text>

          <!-- Span bars for this track -->
          {#each track.spans as span (span.id)}
            {@const xStart = span.startTime}
            {@const xEnd = span.endTime}
            <Rect
              x={xStart}
              y={yPos + 2}
              width={xEnd.getTime() - xStart.getTime()}
              height={TRACK_HEIGHT - 6}
              fill={span.color}
              class="opacity-70 cursor-pointer transition-opacity hover:opacity-100"
              rx={3}
              onmouseenter={(e: MouseEvent) => {
                hoveredSpan = span;
                tooltipX = e.clientX;
                tooltipY = e.clientY;
              }}
              onmousemove={(e: MouseEvent) => {
                tooltipX = e.clientX;
                tooltipY = e.clientY;
              }}
              onmouseleave={() => {
                hoveredSpan = null;
              }}
            />
            <!-- Icon/label at start of span -->
            {#if track.key === "pumpMode"}
              <Group x={xStart} y={yPos + TRACK_HEIGHT / 2}>
                <foreignObject x={4} y={-8} width={16} height={16}>
                  <div class="flex items-center justify-center w-full h-full">
                    <PumpModeIcon state={span.state} size={14} color={span.color} />
                  </div>
                </foreignObject>
              </Group>
            {:else if track.key === "activity"}
              <Group x={xStart} y={yPos + TRACK_HEIGHT / 2}>
                <foreignObject x={4} y={-8} width={16} height={16}>
                  <div class="flex items-center justify-center w-full h-full">
                    <ActivityCategoryIcon category={span.category} size={14} color={span.color} />
                  </div>
                </foreignObject>
              </Group>
            {:else if track.key === "profile" && span.profileName}
              <Text
                x={xStart}
                y={yPos + TRACK_HEIGHT / 2 + 4}
                dx={6}
                class="text-[9px] fill-foreground font-medium pointer-events-none"
              >
                {span.profileName}
              </Text>
            {:else if track.key === "override"}
              <Text
                x={xStart}
                y={yPos + TRACK_HEIGHT / 2 + 4}
                dx={6}
                class="text-[9px] fill-foreground font-medium pointer-events-none"
              >
                {span.state}
              </Text>
            {/if}
          {/each}
        {/each}

        <!-- Basal delivery track (rendered as area chart) -->
        {#if showTempBasals}
          {@const basalYScale = scaleLinear().domain([0, maxBasalRate]).range([basalTrackTop + BASAL_TRACK_HEIGHT - 5, basalTrackTop + 2])}

          <!-- Track background -->
          <Rect
            x={0}
            y={basalTrackTop}
            width="100%"
            height={BASAL_TRACK_HEIGHT - 2}
            fill="var(--muted)"
            class="opacity-20"
          />
          <!-- Track label -->
          <Text
            x={-LABEL_WIDTH + 8}
            y={basalTrackTop + 12}
            class="text-[10px] fill-muted-foreground font-medium"
          >
            BASAL
          </Text>
          <!-- Max rate label -->
          <Text
            x={-LABEL_WIDTH + 8}
            y={basalTrackTop + BASAL_TRACK_HEIGHT - 8}
            class="text-[8px] fill-muted-foreground"
          >
            {maxBasalRate.toFixed(1)} U/h
          </Text>

          <!-- Basal area chart -->
          {#if basalDataPoints.length > 0}
            <Area
              data={basalDataPoints}
              x={(d) => d.time}
              y0={() => basalTrackTop + BASAL_TRACK_HEIGHT - 5}
              y1={(d) => basalYScale(d.rate)}
              curve={curveStepAfter}
              fill="var(--insulin-basal)"
              class="opacity-50"
            />
            <Spline
              data={basalDataPoints}
              x={(d) => d.time}
              y={(d) => basalYScale(d.rate)}
              curve={curveStepAfter}
              class="stroke-insulin-basal stroke-1 fill-none"
            />
          {:else}
            <Text
              x={50}
              y={basalTrackTop + BASAL_TRACK_HEIGHT / 2 + 4}
              class="text-[10px] fill-muted-foreground"
            >
              No basal data
            </Text>
          {/if}
        {/if}

        <!-- Time axis at bottom -->
        <Axis
          placement="bottom"
          rule
          tickLabelProps={{
            class: "text-[10px] fill-muted-foreground",
          }}
        />
      </Svg>
    </Chart>

    <!-- Custom tooltip -->
    {#if hoveredSpan}
      <div
        class="fixed z-50 bg-popover text-popover-foreground border rounded-md shadow-md px-3 py-2 text-sm pointer-events-none"
        style="left: {tooltipX + 12}px; top: {tooltipY - 10}px;"
      >
        <div class="font-medium">{hoveredSpan.state}</div>
        <div class="text-xs text-muted-foreground mt-1">
          <div>{formatTime(hoveredSpan.startTime)} - {formatTime(hoveredSpan.endTime)}</div>
          <div>Duration: {formatDuration(hoveredSpan.startTime, hoveredSpan.endTime)}</div>
          {#if hoveredSpan.rate !== null}
            <div>Rate: {hoveredSpan.rate.toFixed(2)} U/hr</div>
          {/if}
          {#if hoveredSpan.percent !== null}
            <div>Percent: {hoveredSpan.percent}%</div>
          {/if}
          {#if hoveredSpan.profileName}
            <div>Profile: {hoveredSpan.profileName}</div>
          {/if}
        </div>
      </div>
    {/if}
  {/if}
</div>
