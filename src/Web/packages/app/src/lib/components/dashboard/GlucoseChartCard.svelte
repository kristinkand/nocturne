<script lang="ts">
  import type { Entry, Treatment } from "$lib/api";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import {
    Chart,
    Axis,
    Svg,
    Spline,
    Group,
    Rule,
    ChartClipPath,
  } from "layerchart";
  import { chartConfig } from "$lib/constants";

  interface ComponentProps {
    entries?: Entry[];
    treatments?: Treatment[];
    demoMode?: boolean;
    dateRange?: {
      from: Date | string;
      to: Date | string;
    };
  }

  const realtimeStore = getRealtimeStore();
  let {
    entries = realtimeStore.entries,
    treatments = realtimeStore.treatments,
    demoMode = realtimeStore.demoMode,
    dateRange,
  }: ComponentProps = $props();

  // Helper to normalize date - handles both Date objects and ISO strings
  function normalizeDate(
    date: Date | string | undefined,
    fallback: Date
  ): Date {
    if (!date) return fallback;
    return date instanceof Date ? date : new Date(date);
  }

  // Use realtime store values as fallback when props not provided
  const displayDemoMode = $derived(demoMode ?? realtimeStore.demoMode);
  const displayDateRange = $derived({
    from: normalizeDate(
      dateRange?.from,
      new Date(new Date().getTime() - 12 * 60 * 60 * 1000)
    ),
    to: normalizeDate(dateRange?.to, new Date()),
  });

  // Filter treatments by date range
  const filteredTreatments = $derived(
    treatments.filter((t) => {
      const treatmentTime =
        t.mills ?? (t.created_at ? new Date(t.created_at).getTime() : 0);
      return (
        treatmentTime >= displayDateRange.from.getTime() &&
        treatmentTime <= displayDateRange.to.getTime()
      );
    })
  );

  // Bolus treatments (insulin injections/deliveries)
  const bolusTreatments = $derived(
    filteredTreatments.filter(
      (t) =>
        t.insulin &&
        t.insulin > 0 &&
        [
          "Meal Bolus",
          "Correction Bolus",
          "Snack Bolus",
          "Bolus Wizard",
          "Combo Bolus",
        ].includes(t.eventType ?? "")
    )
  );

  // Carb treatments
  const carbTreatments = $derived(
    filteredTreatments.filter((t) => t.carbs && t.carbs > 0)
  );

  // Temp basal treatments (rate changes)
  const tempBasalTreatments = $derived(
    filteredTreatments.filter(
      (t) =>
        t.eventType === "Temp Basal" &&
        (t.rate !== undefined || t.percent !== undefined)
    )
  );

  // Calculate max values for scaling
  const maxInsulin = $derived(
    Math.max(1, ...bolusTreatments.map((t) => t.insulin ?? 0))
  );
  const maxCarbs = $derived(
    Math.max(10, ...carbTreatments.map((t) => t.carbs ?? 0))
  );

  // Y domain for glucose
  const yMax = $derived(
    Math.min(400, Math.max(100, ...entries.map((e) => e.sgv ?? 0)) + 50)
  );

  // X domain time range
  const xMin = $derived(displayDateRange.from.getTime());
  const xMax = $derived(displayDateRange.to.getTime());
  const xRange = $derived(xMax - xMin);

  // Helper to get treatment time
  function getTreatmentTime(t: Treatment): number {
    return t.mills ?? (t.created_at ? new Date(t.created_at).getTime() : 0);
  }

  // Helper to calculate X position as percentage of chart width
  function getXPercent(mills: number): number {
    return ((mills - xMin) / xRange) * 100;
  }

  // Helper to scale insulin circle radius
  function getInsulinRadius(insulin: number): number {
    return 3 + (insulin / maxInsulin) * 5; // 3-8px radius
  }

  // Helper to scale carbs bar height in glucose units (for proper Y scaling)
  function getCarbsGlucoseHeight(carbs: number): number {
    return 20 + (carbs / maxCarbs) * 40; // 20-60 glucose units height
  }

  // Helper to find nearest glucose entry for a treatment
  function findNearestGlucose(treatmentTime: number): number {
    if (entries.length === 0) return 150;

    const nearest = entries.reduce((prev, curr) => {
      const prevDiff = Math.abs((prev.mills ?? 0) - treatmentTime);
      const currDiff = Math.abs((curr.mills ?? 0) - treatmentTime);
      return currDiff < prevDiff ? curr : prev;
    });

    return nearest?.sgv ?? 150;
  }

  $inspect(entries);
</script>

<Card>
  <CardHeader>
    <CardTitle class="flex items-center gap-2">
      Blood Glucose Trend
      {#if displayDemoMode}
        <Badge variant="outline" class="text-xs">Contains Demo Data</Badge>
      {/if}
    </CardTitle>
    <!-- Legend -->
    <div class="flex flex-wrap gap-4 text-xs text-muted-foreground">
      <div class="flex items-center gap-1">
        <div class="w-3 h-0.5 bg-primary"></div>
        <span>Glucose</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-3 h-3 rounded-full bg-blue-500"></div>
        <span>Bolus</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-3 h-3 bg-amber-500"></div>
        <span>Carbs</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-3 h-0.5 bg-purple-500"></div>
        <span>Temp Basal</span>
      </div>
    </div>
  </CardHeader>
  <CardContent class="h-96">
    <Chart
      data={entries}
      x={(e) => (e.mills ? new Date(e.mills) : undefined)}
      y="sgv"
      yDomain={[0, yMax]}
      yNice
      xDomain={[displayDateRange.from, displayDateRange.to]}
      padding={{ left: 40, bottom: 40, top: 20, right: 20 }}
    >
      <Svg>
        <!-- Axes -->
        <Axis placement="left" rule grid format={(v) => `${v}`} />
        <Axis
          placement="bottom"
          rule
          format={(v) =>
            v instanceof Date
              ? v.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
              : v}
        />

        <!-- Target range threshold lines using Rule -->
        <Rule
          y={chartConfig.high.threshold}
          class="stroke-destructive/60 [stroke-dasharray:4,4]"
        />
        <Rule
          y={chartConfig.low.threshold}
          class="stroke-warning/60 [stroke-dasharray:4,4]"
        />

        <!-- Clip path to keep chart content within bounds -->
        <ChartClipPath>
          <!-- Temp Basal treatments (rendered as colored bands at the top of chart) -->
          <Group class="temp-basals">
            {#each tempBasalTreatments as treatment}
              {@const startX = getXPercent(getTreatmentTime(treatment))}
              {@const duration = treatment.duration ?? 30}
              {@const endX = getXPercent(
                getTreatmentTime(treatment) + duration * 60 * 1000
              )}
              <line
                x1="{startX}%"
                x2="{endX}%"
                y1="2%"
                y2="2%"
                stroke="rgb(168 85 247)"
                stroke-width="4"
                opacity="0.8"
              />
              <text
                x="{(startX + endX) / 2}%"
                y="6%"
                text-anchor="middle"
                class="text-[8px] fill-purple-600"
              >
                {treatment.rate != null
                  ? `${treatment.rate.toFixed(1)}`
                  : `${treatment.percent}%`}
              </text>
            {/each}
          </Group>

          <!-- Carbs (rendered as bars from bottom of chart area) -->
          <Group class="carbs">
            {#each carbTreatments as treatment}
              {@const xPos = getXPercent(getTreatmentTime(treatment))}
              {@const carbHeight = getCarbsGlucoseHeight(treatment.carbs ?? 0)}
              {@const barTop = (carbHeight / yMax) * 100}
              <rect
                x="{xPos - 0.4}%"
                y="{100 - barTop}%"
                width="0.8%"
                height="{barTop}%"
                fill="rgb(245 158 11)"
                fill-opacity="0.7"
                rx="2"
              />
              <text
                x="{xPos}%"
                y="{100 - barTop - 1}%"
                text-anchor="middle"
                class="text-[8px] fill-amber-700 font-medium"
              >
                {treatment.carbs}g
              </text>
            {/each}
          </Group>

          <!-- Glucose line -->
          <Spline
            data={entries.filter((d) => d.sgv !== null && d.sgv !== undefined)}
            x={(e) => (e.mills ? new Date(e.mills) : undefined)}
            y="sgv"
            class="stroke-primary stroke-2"
          />

          <!-- Bolus markers (circles positioned above the glucose line) -->
          <Group class="boluses">
            {#each bolusTreatments as treatment}
              {@const treatmentMills = getTreatmentTime(treatment)}
              {@const xPos = getXPercent(treatmentMills)}
              {@const radius = getInsulinRadius(treatment.insulin ?? 0)}
              {@const nearestGlucose = findNearestGlucose(treatmentMills)}
              {@const yPos = 100 - ((nearestGlucose + 20) / yMax) * 100}
              <circle
                cx="{xPos}%"
                cy="{yPos}%"
                r={radius}
                fill="rgb(59 130 246)"
                fill-opacity="0.9"
                stroke="rgb(29 78 216)"
                stroke-width="1"
              />
              <text
                x="{xPos}%"
                y="{yPos - 2.5}%"
                text-anchor="middle"
                class="text-[8px] fill-blue-700 font-semibold"
              >
                {treatment.insulin?.toFixed(1)}U
              </text>
            {/each}
          </Group>
        </ChartClipPath>
      </Svg>
    </Chart>
  </CardContent>
</Card>
