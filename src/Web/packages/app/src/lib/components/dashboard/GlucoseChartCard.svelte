<script lang="ts">
  import type { Entry } from "$lib/api";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { LineChart, Spline } from "layerchart";
  import { chartConfig } from "$lib/constants";

  interface ComponentProps {
    entries?: Entry[];
    demoMode?: boolean;
    dateRange?: {
      from: Date;
      to: Date;
    };
  }

  const realtimeStore = getRealtimeStore();
  let {
    entries = realtimeStore.entries,
    demoMode = realtimeStore.demoMode,
    dateRange,
  }: ComponentProps = $props();
  // Use realtime store values as fallback when props not provided
  const displayDemoMode = $derived(demoMode ?? realtimeStore.demoMode);
  const displayDateRange = $derived({
    from:
      dateRange?.from ?? new Date(new Date().getTime() - 12 * 60 * 60 * 1000),
    to: dateRange?.to ?? new Date(),
  });

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
  </CardHeader>
  <CardContent class="h-96">
    <LineChart
      data={entries}
      x={(e) => (e.mills ? new Date(e.mills) : undefined)}
      xBaseline={displayDateRange.to!.getTime()}
      y="sgv"
      yDomain={[0, Math.min(400, Math.max(...entries.map((e) => e.sgv!)) + 50)]}
      yNice
      xDomain={[displayDateRange.from, displayDateRange.to]}
      padding={{ left: 16, bottom: 24 }}
      cDomain={[
        chartConfig.low.threshold,
        chartConfig.target.threshold,
        chartConfig.high.threshold,
        chartConfig.severeHigh.threshold,
      ]}
      cRange={Object.values(chartConfig).map((c) => c.color)}
      brush
      props={{
        spline: { motion: { type: "tween", duration: 200 } },
        xAxis: {
          motion: { type: "tween", duration: 200 },
          tickMultiline: true,
        },
      }}
      annotations={[
        {
          type: "line",
          y: chartConfig.high.threshold,
          label: `High (${chartConfig.high.threshold})`,
          props: {
            label: { class: "text-xs" },
            line: {
              class: "[stroke-dasharray:2,2] stroke-high-bg",
            },
          },
        },
        {
          type: "line",
          y: chartConfig.low.threshold,
          label: `Low (${chartConfig.low.threshold})`,
          props: {
            label: { class: "text-xs" },
            line: { class: "[stroke-dasharray:2,2] stroke-low-bg" },
          },
        },
      ]}
    >
      {#snippet belowMarks({ series })}
        {#each series as s}
          <Spline
            data={entries.filter((d) => d.sgv !== null)}
            y={s.value}
            class="[stroke-dasharray:3,3]"
            stroke={s.color}
          />
        {/each}
      {/snippet}
      <!-- {#snippet tooltip({ context })}
        <Tooltip.Root>
          {#snippet children({ data })}
            {@const value = context.y(data).sgv}
            <Tooltip.Header>
              {JSON.stringify(context.x(data))}
              {context.x(data).sgv}
            </Tooltip.Header>
            <Tooltip.List>
              <Tooltip.Item
                label="value"
                {value}
                color={value > 50 ? "var(--color-danger)" : "var(--color-info)"}
              />
            </Tooltip.List>
          {/snippet}
        </Tooltip.Root>
      {/snippet} -->
    </LineChart>
  </CardContent>
</Card>
