<script lang="ts">
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { Badge } from "$lib/components/ui/badge";
  import { Chart, Svg, Spline, Rule } from "layerchart";
  import {
    ArrowUp,
    ArrowDown,
    ArrowUpRight,
    ArrowDownRight,
    Minus,
  } from "lucide-svelte";

  const realtimeStore = getRealtimeStore();

  // Get current glucose values
  const currentBG = $derived(realtimeStore.currentBG);
  const bgDelta = $derived(realtimeStore.bgDelta);
  const direction = $derived(realtimeStore.direction);
  const demoMode = $derived(realtimeStore.demoMode);
  const lastUpdated = $derived(realtimeStore.lastUpdated);

  // Get last 3 hours of entries for mini chart
  const chartEntries = $derived.by(() => {
    const threeHoursAgo = Date.now() - 3 * 60 * 60 * 1000;
    return realtimeStore.entries
      .filter((e) => (e.mills ?? 0) > threeHoursAgo)
      .map((e) => ({
        date: new Date(e.mills ?? 0),
        value: e.sgv ?? e.mgdl ?? 0,
      }))
      .sort((a, b) => a.date.getTime() - b.date.getTime());
  });

  // Y domain for chart
  const yMax = $derived.by(() => {
    if (chartEntries.length === 0) return 300;
    const values = chartEntries.map((e) => e.value);
    return Math.min(400, Math.max(...values) + 30);
  });

  // Get background color based on BG value
  const getBGColor = (bg: number) => {
    if (bg < 70) return "bg-destructive text-destructive-foreground";
    if (bg < 80) return "bg-yellow-500 text-black";
    if (bg > 250) return "bg-destructive text-destructive-foreground";
    if (bg > 180) return "bg-orange-500 text-black";
    return "bg-green-500 text-white";
  };

  // Get direction icon
  const getDirectionIcon = (dir: string) => {
    switch (dir) {
      case "DoubleUp":
        return ArrowUp;
      case "SingleUp":
        return ArrowUpRight;
      case "FortyFiveUp":
        return ArrowUpRight;
      case "DoubleDown":
        return ArrowDown;
      case "SingleDown":
        return ArrowDownRight;
      case "FortyFiveDown":
        return ArrowDownRight;
      default:
        return Minus;
    }
  };

  const DirectionIcon = $derived(getDirectionIcon(direction));

  // Calculate time since last reading
  const timeSince = $derived.by(() => {
    const diff = Date.now() - lastUpdated;
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return "just now";
    if (mins === 1) return "1 min ago";
    return `${mins} min ago`;
  });
</script>

<div class="space-y-3 group-data-[collapsible=icon]:hidden">
  <!-- Current BG Display -->
  <div class="flex items-center justify-between">
    <div class="flex items-center gap-2">
      <div
        class="text-3xl font-bold px-3 py-1.5 rounded-lg {getBGColor(
          currentBG
        )}"
      >
        {currentBG}
      </div>
      <div class="flex flex-col items-start">
        <div class="flex items-center gap-1">
          <DirectionIcon class="h-5 w-5" />
          <span class="text-sm font-medium">
            {bgDelta > 0 ? "+" : ""}{bgDelta}
          </span>
        </div>
        <span class="text-xs text-muted-foreground">{timeSince}</span>
      </div>
    </div>
    {#if demoMode}
      <Badge variant="secondary" class="text-xs">Demo</Badge>
    {/if}
  </div>

  <!-- Mini Chart -->
  <div
    class="h-16 w-full rounded-md bg-card border border-border overflow-hidden"
  >
    {#if chartEntries.length > 1}
      <Chart
        data={chartEntries}
        x="date"
        y="value"
        yDomain={[40, yMax]}
        padding={{ top: 2, bottom: 2, left: 2, right: 2 }}
      >
        <Svg>
          <!-- Target range lines -->
          <Rule y={70} class="stroke-yellow-500/40" />
          <Rule y={180} class="stroke-orange-500/40" />

          <!-- Glucose line -->
          <Spline class="stroke-primary stroke-2 fill-none" />
        </Svg>
      </Chart>
    {:else}
      <div
        class="h-full flex items-center justify-center text-xs text-muted-foreground"
      >
        Waiting for data...
      </div>
    {/if}
  </div>
</div>

<!-- Collapsed state: just show current BG -->
<div class="hidden group-data-[collapsible=icon]:flex justify-center">
  <div class="text-lg font-bold px-2 py-1 rounded-md {getBGColor(currentBG)}">
    {currentBG}
  </div>
</div>
