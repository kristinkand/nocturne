<script lang="ts">
  import type { Entry, Treatment } from "$lib/api";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import * as ToggleGroup from "$lib/components/ui/toggle-group";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { Chart, Axis, Svg, Area, Tooltip } from "layerchart";
  import { chartConfig } from "$lib/constants";
  import { curveStepAfter, curveMonotoneX } from "d3";

  interface ComponentProps {
    entries?: Entry[];
    treatments?: Treatment[];
    demoMode?: boolean;
    dateRange?: {
      from: Date | string;
      to: Date | string;
    };
    /** Default basal rate from profile (U/hr) */
    defaultBasalRate?: number;
    /** Insulin to carb ratio (g per 1U) */
    carbRatio?: number;
  }

  const realtimeStore = getRealtimeStore();
  let {
    entries = realtimeStore.entries,
    treatments = realtimeStore.treatments,
    demoMode = realtimeStore.demoMode,
    dateRange,
    defaultBasalRate = 1.0,
    carbRatio = 10,
  }: ComponentProps = $props();

  // Time range selection (in hours)
  type TimeRangeOption = "2" | "4" | "6" | "12" | "24";
  let selectedTimeRange = $state<TimeRangeOption>("6");

  const timeRangeOptions: { value: TimeRangeOption; label: string }[] = [
    { value: "2", label: "2h" },
    { value: "4", label: "4h" },
    { value: "6", label: "6h" },
    { value: "12", label: "12h" },
    { value: "24", label: "24h" },
  ];

  // Helper to normalize date
  function normalizeDate(
    date: Date | string | undefined,
    fallback: Date
  ): Date {
    if (!date) return fallback;
    return date instanceof Date ? date : new Date(date);
  }

  const displayDemoMode = $derived(demoMode ?? realtimeStore.demoMode);

  const displayDateRange = $derived({
    from: dateRange
      ? normalizeDate(dateRange.from, new Date())
      : new Date(
          new Date().getTime() - parseInt(selectedTimeRange) * 60 * 60 * 1000
        ),
    to: dateRange ? normalizeDate(dateRange.to, new Date()) : new Date(),
  });

  // Filter entries by date range
  const filteredEntries = $derived(
    entries.filter((e) => {
      const entryTime = e.mills ?? 0;
      return (
        entryTime >= displayDateRange.from.getTime() &&
        entryTime <= displayDateRange.to.getTime()
      );
    })
  );

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

  // Bolus treatments
  const bolusTreatments = $derived(
    filteredTreatments.filter(
      (t) =>
        t.insulin &&
        t.insulin > 0 &&
        (t.eventType?.includes("Bolus") ||
          t.eventType === "SMB" ||
          t.eventType === "Correction Bolus" ||
          t.eventType === "Meal Bolus" ||
          t.eventType === "Snack Bolus" ||
          t.eventType === "Bolus Wizard" ||
          t.eventType === "Combo Bolus")
    )
  );

  // Carb treatments
  const carbTreatments = $derived(
    filteredTreatments.filter((t) => t.carbs && t.carbs > 0)
  );

  // Temp basal treatments
  const tempBasalTreatments = $derived(
    filteredTreatments.filter(
      (t) =>
        t.eventType === "Temp Basal" &&
        (t.rate !== undefined || t.percent !== undefined)
    )
  );

  // X domain
  const xMin = $derived(displayDateRange.from.getTime());
  const xMax = $derived(displayDateRange.to.getTime());
  const xRange = $derived(xMax - xMin);

  function getTreatmentTime(t: Treatment): number {
    return t.mills ?? (t.created_at ? new Date(t.created_at).getTime() : 0);
  }

  function getXPercent(mills: number): number {
    return ((mills - xMin) / xRange) * 100;
  }

  // =====================================
  // Chart Layout - Unified Y-axis scale
  // =====================================
  // We'll use a unified coordinate system where:
  // - Y values 0-100 represent the visual chart (0 = bottom, 100 = top)
  // - Different data types map to different regions

  // Basal region: 85-100 (top 15%)
  // Glucose region: 20-82 (main 62%)
  // IOB/COB region: 0-18 (bottom 18%)

  const BASAL_Y_TOP = 100;
  const BASAL_Y_BOTTOM = 88;
  const GLUCOSE_Y_TOP = 85;
  const GLUCOSE_Y_BOTTOM = 22;
  const IOB_COB_Y_TOP = 18;
  const IOB_COB_Y_BOTTOM = 2;

  // Glucose scaling
  const glucoseYMin = 40;
  const glucoseYMax = $derived(
    Math.min(400, Math.max(280, ...filteredEntries.map((e) => e.sgv ?? 0)) + 20)
  );

  function glucoseToY(sgv: number): number {
    const normalized = (sgv - glucoseYMin) / (glucoseYMax - glucoseYMin);
    return GLUCOSE_Y_BOTTOM + normalized * (GLUCOSE_Y_TOP - GLUCOSE_Y_BOTTOM);
  }

  // Glucose data for chart
  const glucoseData = $derived(
    filteredEntries
      .filter((e) => e.sgv !== null && e.sgv !== undefined)
      .map((e) => ({
        time: new Date(e.mills ?? 0),
        sgv: e.sgv ?? 0,
        y: glucoseToY(e.sgv ?? 0),
        color: getGlucoseColor(e.sgv ?? 0),
      }))
      .sort((a, b) => a.time.getTime() - b.time.getTime())
  );

  // Basal scaling
  const maxBasalRate = $derived(
    Math.max(
      defaultBasalRate * 2.5,
      ...tempBasalTreatments.map((t) => t.rate ?? 0)
    )
  );

  function basalToY(rate: number): number {
    const normalized = rate / maxBasalRate;
    return BASAL_Y_BOTTOM + normalized * (BASAL_Y_TOP - BASAL_Y_BOTTOM);
  }

  const defaultBasalY = $derived(basalToY(defaultBasalRate));

  // Basal data
  const basalData = $derived.by(() => {
    const data: { time: Date; rate: number; y: number }[] = [];

    data.push({
      time: displayDateRange.from,
      rate: defaultBasalRate,
      y: basalToY(defaultBasalRate),
    });

    if (tempBasalTreatments.length > 0) {
      const sorted = [...tempBasalTreatments].sort(
        (a, b) => getTreatmentTime(a) - getTreatmentTime(b)
      );

      sorted.forEach((t) => {
        const startTime = getTreatmentTime(t);
        const duration = (t.duration ?? 30) * 60 * 1000;
        const endTime = startTime + duration;
        const rate = t.rate ?? defaultBasalRate;

        data.push({
          time: new Date(startTime - 1),
          rate: defaultBasalRate,
          y: basalToY(defaultBasalRate),
        });
        data.push({ time: new Date(startTime), rate, y: basalToY(rate) });
        data.push({ time: new Date(endTime), rate, y: basalToY(rate) });
        data.push({
          time: new Date(endTime + 1),
          rate: defaultBasalRate,
          y: basalToY(defaultBasalRate),
        });
      });
    }

    data.push({
      time: displayDateRange.to,
      rate: defaultBasalRate,
      y: basalToY(defaultBasalRate),
    });

    return data.sort((a, b) => a.time.getTime() - b.time.getTime());
  });

  // IOB calculation
  const insulinDuration = 4 * 60 * 60 * 1000;

  const iobData = $derived.by(() => {
    const data: { time: Date; iob: number }[] = [];
    const step = 5 * 60 * 1000;

    for (let t = xMin; t <= xMax; t += step) {
      let iob = 0;
      bolusTreatments.forEach((bolus) => {
        const bolusTime = getTreatmentTime(bolus);
        const elapsed = t - bolusTime;
        if (elapsed >= 0 && elapsed < insulinDuration) {
          const remaining = Math.exp(-elapsed / (insulinDuration / 3));
          iob += (bolus.insulin ?? 0) * remaining;
        }
      });
      data.push({ time: new Date(t), iob });
    }
    return data;
  });

  const maxIOB = $derived(Math.max(3, ...iobData.map((d) => d.iob)));

  function iobToY(iob: number): number {
    const normalized = iob / maxIOB;
    return IOB_COB_Y_BOTTOM + normalized * (IOB_COB_Y_TOP - IOB_COB_Y_BOTTOM);
  }

  const iobDataWithY = $derived(
    iobData.map((d) => ({ ...d, y: iobToY(d.iob) }))
  );

  // COB calculation
  const carbDuration = 3 * 60 * 60 * 1000;

  const cobData = $derived.by(() => {
    const data: { time: Date; cob: number }[] = [];
    const step = 5 * 60 * 1000;

    for (let t = xMin; t <= xMax; t += step) {
      let cob = 0;
      carbTreatments.forEach((carb) => {
        const carbTime = getTreatmentTime(carb);
        const elapsed = t - carbTime;
        if (elapsed >= 0 && elapsed < carbDuration) {
          const remaining = 1 - elapsed / carbDuration;
          cob += (carb.carbs ?? 0) * remaining;
        }
      });
      data.push({ time: new Date(t), cob });
    }
    return data;
  });

  const maxCOB = $derived(
    Math.max(carbRatio * 3, ...cobData.map((d) => d.cob))
  );

  function cobToY(cob: number): number {
    const normalized = cob / maxCOB;
    return IOB_COB_Y_BOTTOM + normalized * (IOB_COB_Y_TOP - IOB_COB_Y_BOTTOM);
  }

  const cobDataWithY = $derived(
    cobData.map((d) => ({ ...d, y: cobToY(d.cob) }))
  );

  // Reference line positions
  const lowThreshold = chartConfig.low.threshold ?? 55;
  const highThreshold = chartConfig.high.threshold ?? 180;
  const lowLineY = $derived(glucoseToY(lowThreshold));
  const highLineY = $derived(glucoseToY(highThreshold));
  const iobRefY = $derived(iobToY(1));
  const cobRefY = $derived(cobToY(carbRatio));

  function getGlucoseColor(sgv: number): string {
    const low = chartConfig.low.threshold ?? 55;
    const target = chartConfig.target.threshold ?? 80;
    const high = chartConfig.high.threshold ?? 180;
    const severeHigh = chartConfig.severeHigh.threshold ?? 250;

    if (sgv < low) return "rgb(239 68 68)";
    if (sgv < target) return "rgb(234 179 8)";
    if (sgv <= high) return "rgb(34 197 94)";
    if (sgv <= severeHigh) return "rgb(249 115 22)";
    return "rgb(239 68 68)";
  }

  function formatTime(date: Date): string {
    return date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
  }
</script>

<Card class="bg-slate-950 border-slate-800">
  <CardHeader class="pb-2">
    <div class="flex items-center justify-between">
      <CardTitle class="flex items-center gap-2 text-slate-100">
        Blood Glucose
        {#if displayDemoMode}
          <Badge
            variant="outline"
            class="text-xs border-slate-700 text-slate-400"
          >
            Demo
          </Badge>
        {/if}
      </CardTitle>

      <ToggleGroup.Root
        type="single"
        bind:value={selectedTimeRange}
        class="bg-slate-900 rounded-lg p-0.5"
      >
        {#each timeRangeOptions as option}
          <ToggleGroup.Item
            value={option.value}
            class="px-3 py-1 text-xs font-medium text-slate-400 data-[state=on]:bg-slate-700 data-[state=on]:text-slate-100 rounded-md transition-colors"
          >
            {option.label}
          </ToggleGroup.Item>
        {/each}
      </ToggleGroup.Root>
    </div>
  </CardHeader>

  <CardContent class="p-2">
    <div class="h-[420px] relative">
      <Chart
        data={glucoseData}
        x="time"
        y="y"
        yDomain={[0, 100]}
        xDomain={[displayDateRange.from, displayDateRange.to]}
        padding={{ left: 48, bottom: 30, top: 8, right: 12 }}
      >
        <Svg>
          <!-- Region Labels (positioned in padding area) -->
          <text x="2" y="6%" class="text-[8px] fill-slate-500 font-medium">
            BASAL
          </text>
          <text x="2" y="50%" class="text-[8px] fill-slate-500 font-medium">
            BG
          </text>
          <text x="2" y="92%" class="text-[8px] fill-slate-500 font-medium">
            IOB/COB
          </text>

          <!-- Region Separators -->
          <line
            x1="0%"
            x2="100%"
            y1="{100 - GLUCOSE_Y_TOP}%"
            y2="{100 - GLUCOSE_Y_TOP}%"
            stroke="rgb(51 65 85)"
            stroke-width="1"
          />
          <line
            x1="0%"
            x2="100%"
            y1="{100 - IOB_COB_Y_TOP}%"
            y2="{100 - IOB_COB_Y_TOP}%"
            stroke="rgb(51 65 85)"
            stroke-width="1"
          />

          <!-- ===== BASAL REGION ===== -->
          <!-- Default Basal Reference (dotted line) -->
          <line
            x1="0%"
            x2="100%"
            y1="{100 - defaultBasalY}%"
            y2="{100 - defaultBasalY}%"
            stroke="rgb(100 116 139)"
            stroke-width="1"
            stroke-dasharray="4,4"
          />
          <text
            x="99%"
            y="{100 - defaultBasalY - 1}%"
            text-anchor="end"
            class="text-[7px] fill-slate-400"
          >
            {defaultBasalRate.toFixed(1)}U/hr
          </text>

          <!-- Basal Area -->
          {#if basalData.length > 0}
            <Area
              data={basalData}
              x="time"
              y="y"
              y0={BASAL_Y_BOTTOM}
              class="fill-blue-500/40"
              line={{ class: "stroke-blue-400 stroke-1" }}
              curve={curveStepAfter}
            />
          {/if}

          <!-- ===== GLUCOSE REGION ===== -->
          <!-- High/Low Reference Lines -->
          <line
            x1="0%"
            x2="100%"
            y1="{100 - highLineY}%"
            y2="{100 - highLineY}%"
            stroke="rgb(245 158 11)"
            stroke-width="1"
            stroke-dasharray="4,4"
            opacity="0.5"
          />
          <text
            x="99%"
            y="{100 - highLineY - 1}%"
            text-anchor="end"
            class="text-[7px] fill-amber-500"
          >
            {highThreshold}
          </text>

          <line
            x1="0%"
            x2="100%"
            y1="{100 - lowLineY}%"
            y2="{100 - lowLineY}%"
            stroke="rgb(239 68 68)"
            stroke-width="1"
            stroke-dasharray="4,4"
            opacity="0.5"
          />
          <text
            x="99%"
            y="{100 - lowLineY + 3}%"
            text-anchor="end"
            class="text-[7px] fill-red-500"
          >
            {lowThreshold}
          </text>

          <!-- Y Axis Labels for Glucose -->
          {#each [50, 100, 150, 200, 250, 300].filter((v) => v <= glucoseYMax && v >= glucoseYMin) as value}
            {@const yPos = glucoseToY(value)}
            <text
              x="44"
              y="{100 - yPos}%"
              text-anchor="end"
              dominant-baseline="middle"
              class="text-[8px] fill-slate-500"
            >
              {value}
            </text>
            <line
              x1="46"
              x2="100%"
              y1="{100 - yPos}%"
              y2="{100 - yPos}%"
              stroke="rgb(51 65 85)"
              stroke-width="0.5"
              opacity="0.3"
            />
          {/each}

          <!-- Glucose Dots -->
          {#each glucoseData as entry}
            {@const xPct = getXPercent(entry.time.getTime())}
            <circle
              cx="{xPct}%"
              cy="{100 - entry.y}%"
              r="3"
              fill={entry.color}
              opacity="0.9"
            />
          {/each}

          <!-- Bolus Markers -->
          {#each bolusTreatments as treatment}
            {@const xPct = getXPercent(getTreatmentTime(treatment))}
            {@const insulin = treatment.insulin ?? 0}
            {@const markerY = 100 - GLUCOSE_Y_TOP + 2}
            <polygon
              points="{xPct}%,{markerY}% {xPct - 0.4}%,{markerY + 3}% {xPct +
                0.4}%,{markerY + 3}%"
              fill="rgb(59 130 246)"
              opacity="0.9"
            />
            <text
              x="{xPct}%"
              y="{markerY - 1}%"
              text-anchor="middle"
              class="text-[6px] fill-blue-400 font-medium"
            >
              {insulin < 1 ? insulin.toFixed(2) : insulin.toFixed(1)}
            </text>
          {/each}

          <!-- Carb Markers -->
          {#each carbTreatments as treatment}
            {@const xPct = getXPercent(getTreatmentTime(treatment))}
            {@const carbs = treatment.carbs ?? 0}
            {@const markerY = 100 - GLUCOSE_Y_BOTTOM - 2}
            <polygon
              points="{xPct}%,{markerY}% {xPct - 0.4}%,{markerY - 3}% {xPct +
                0.4}%,{markerY - 3}%"
              fill="rgb(245 158 11)"
              opacity="0.9"
            />
            <text
              x="{xPct}%"
              y="{markerY + 3}%"
              text-anchor="middle"
              class="text-[6px] fill-amber-400 font-medium"
            >
              {carbs}g
            </text>
          {/each}

          <!-- ===== IOB/COB REGION ===== -->
          <!-- 1U IOB Reference -->
          {#if maxIOB >= 1}
            <line
              x1="0%"
              x2="100%"
              y1="{100 - iobRefY}%"
              y2="{100 - iobRefY}%"
              stroke="rgb(96 165 250)"
              stroke-width="1"
              stroke-dasharray="2,2"
              opacity="0.4"
            />
            <text
              x="99%"
              y="{100 - iobRefY - 1}%"
              text-anchor="end"
              class="text-[6px] fill-blue-400"
            >
              1U
            </text>
          {/if}

          <!-- Carb Ratio Reference -->
          {#if maxCOB >= carbRatio}
            <line
              x1="0%"
              x2="100%"
              y1="{100 - cobRefY}%"
              y2="{100 - cobRefY}%"
              stroke="rgb(251 191 36)"
              stroke-width="1"
              stroke-dasharray="2,2"
              opacity="0.4"
            />
            <text
              x="99%"
              y="{100 - cobRefY + 2}%"
              text-anchor="end"
              class="text-[6px] fill-amber-400"
            >
              {carbRatio}g
            </text>
          {/if}

          <!-- IOB Area -->
          {#if iobDataWithY.some((d) => d.iob > 0.01)}
            <Area
              data={iobDataWithY}
              x="time"
              y="y"
              y0={IOB_COB_Y_BOTTOM}
              class="fill-blue-600/30"
              line={{ class: "stroke-blue-400 stroke-1" }}
              curve={curveMonotoneX}
            />
          {/if}

          <!-- COB Area -->
          {#if cobDataWithY.some((d) => d.cob > 0.1)}
            <Area
              data={cobDataWithY}
              x="time"
              y="y"
              y0={IOB_COB_Y_BOTTOM}
              class="fill-amber-600/30"
              line={{ class: "stroke-amber-400 stroke-1" }}
              curve={curveMonotoneX}
            />
          {/if}

          <!-- X Axis -->
          <Axis
            placement="bottom"
            format={(v) => (v instanceof Date ? formatTime(v) : String(v))}
            tickLabelProps={{ class: "text-[8px] fill-slate-500" }}
          />
        </Svg>

        <!-- Tooltip -->
        <Tooltip.Root
          class="bg-slate-800 text-slate-100 p-2 rounded shadow-lg border border-slate-700 text-xs"
        >
          {#snippet children({ data })}
            {#if data?.sgv}
              <div class="font-medium">{data.sgv} mg/dL</div>
              <div class="text-slate-400">{formatTime(data.time)}</div>
            {/if}
          {/snippet}
        </Tooltip.Root>
      </Chart>
    </div>

    <!-- Legend -->
    <div
      class="flex flex-wrap justify-center gap-4 text-[10px] text-slate-500 pt-2"
    >
      <div class="flex items-center gap-1">
        <div class="w-2 h-2 rounded-full bg-green-500"></div>
        <span>In Range</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-2 h-2 rounded-full bg-amber-500"></div>
        <span>High</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-2 h-2 rounded-full bg-red-500"></div>
        <span>Low</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-3 h-2 bg-blue-500/50 border border-blue-400"></div>
        <span>Basal</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-3 h-2 bg-blue-600/40 border border-blue-400"></div>
        <span>IOB</span>
      </div>
      <div class="flex items-center gap-1">
        <div class="w-3 h-2 bg-amber-600/40 border border-amber-400"></div>
        <span>COB</span>
      </div>
    </div>
  </CardContent>
</Card>
