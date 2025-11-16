<script lang="ts">
  import type { Entry } from "$lib/api";
  import { ApiClient, type AveragedStats } from "$lib/api";
  import { PUBLIC_API_URL } from "$env/static/public";
  import { DEFAULT_THRESHOLDS } from "$lib/constants";
  import { AreaChart, Tooltip } from "layerchart";
  import { onMount } from "svelte";

  let {
    entries,
    averagedStats,
  }: {
    entries?: Entry[];
    averagedStats?: AveragedStats[];
  } = $props();

  let data = $state(averagedStats || []);
  let loading = $state(false);

  onMount(() => {
    if (!data.length && entries?.length) {
      const apiClient = new ApiClient(PUBLIC_API_URL);
      loading = true;
      apiClient.statistics.calculateAveragedStats(entries).then((stats) => {
        data = stats;
        loading = false;
      });
    }
  });

  $effect(() => {
    if (averagedStats?.length) {
      data = averagedStats;
    }
  });
</script>

{#if loading}
  <div>Loading...</div>
{:else if data.length > 0}
  <AreaChart
    {data}
    x={(d) => d.hour}
    y={(d) => d.median}
    renderContext="svg"
    legend
    series={[
      {
        key: "p10",
        value: [(d) => d.percentiles?.p25, (d) => d.percentiles?.p10],
        color: "var(--chart-1)",
        label: "P10",
      },
      {
        key: "p25",
        value: [(d) => d.median, (d) => d.percentiles?.p25],
        color: "var(--chart-2)",
        label: "P25",
      },
      {
        key: "median",
        value: [(d) => d.median, (d) => d.median],
        color: "black",
        props: {
          line: { strokeWidth: 1.75 },
        },
        label: "Median",
      },
      {
        key: "percentiles.p75",
        value: [(d) => d.median, (d) => d.percentiles?.p75],
        color: "var(--chart-3)",
        label: "P75",
      },
      {
        key: "p90",
        value: [(d) => d.percentiles?.p75, (d) => d.percentiles?.p90],
        color: "var(--chart-1)",
        label: "P90",
      },
    ]}
    xDomain={[0, 23]}
    yDomain={[0, 400]}
    seriesLayout="overlap"
    tooltip={{ mode: "bisect-x" }}
    annotations={[
      {
        type: "line",
        x: 0,
        y: DEFAULT_THRESHOLDS.low,
        label: "Low",
        labelXOffset: 4,
        labelYOffset: 4,
        props: {
          label: {
            class: "text-xs text-muted-foreground",
          },
          line: {
            stroke: "var(--low-bg)",
            strokeWidth: 1,
            "stroke-dasharray": "4 2",
          },
        },
      },
      {
        type: "line",
        x: 0,
        y: DEFAULT_THRESHOLDS.high,
        label: "High",
        labelXOffset: 4,
        labelYOffset: -12,
        props: {
          label: {
            class: "text-xs text-muted-foreground",
          },
          line: {
            stroke: "var(--high-bg)",
            strokeWidth: 1,
            "stroke-dasharray": "4 2",
          },
        },
      },
    ]}
    brush
    props={{
      area: { motion: { type: "tween", duration: 200 } },
      xAxis: {
        motion: { type: "tween", duration: 200 },
        tickMultiline: true,
      },
    }}
    padding={{ top: 20, right: 20, bottom: 40, left: 20 }}
  ></AreaChart>
{:else}
  <div class="flex items-center justify-center text-muted-foreground">
    <div class="text-center">
      <p class="text-lg font-medium">No data available</p>
      <p class="text-sm">No glucose data found for the selected time period</p>
    </div>
  </div>
{/if}
