<script lang="ts">
  import { AreaChart } from "layerchart";
  import * as ChartC from "$lib/components/ui/chart/index.js";
  import { getUniversalApiClient } from "$lib/api";
  import type { Treatment } from "$lib/api";
  import { onMount } from "svelte";

  interface IOBProfile {
    getDIA?: (time: number, spec_profile?: unknown) => number;
    getSensitivity?: (time: number, spec_profile?: unknown) => number;
  }
  interface Props {
    treatments: Treatment[];
    intervalMinutes?: number; // Time interval in minutes (defaults to 15)
    profile?: IOBProfile; // Optional IOB profile for more accurate calculations
  }
  let { treatments, intervalMinutes = 5, profile }: Props = $props();

  let chartData: Array<{
    timeSlot: number;
    hour: number;
    minute: number;
    timeLabel: string;
    totalIOB: number;
    bolusIOB: number;
    basalIOB: number;
  }> = $state([]);

  let isLoading = $state(true);
  let error = $state<string | null>(null);

  // Load IOB data from backend API
  async function loadIOBData() {
    try {
      isLoading = true;
      error = null;

      const apiClient = getUniversalApiClient();
      const response = await apiClient.iob.getHourlyIob({
        intervalMinutes,
        hours: 24,
        startTime: undefined, // Use default start time (24 hours ago)
      });

      // Transform API response to match chart data format
      chartData =
        response.data?.map((item) => ({
          timeSlot: item.timeSlot || 0,
          hour: item.hour || 0,
          minute: item.minute || 0,
          timeLabel: item.timeLabel || "",
          totalIOB: item.totalIOB || 0,
          bolusIOB: item.bolusIOB || 0,
          basalIOB: item.basalIOB || 0,
        })) || [];
    } catch (err) {
      console.error("Failed to load IOB data:", err);
      error = "Failed to load IOB data";
      chartData = [];
    } finally {
      isLoading = false;
    }
  }

  // Load data when component mounts or when parameters change
  onMount(() => {
    loadIOBData();
  });

  // Reload when interval changes
  $effect(() => {
    if (intervalMinutes) {
      loadIOBData();
    }
  });
</script>

<ChartC.Container config={{}} class="w-full h-80">
  {#if isLoading}
    <div class="flex items-center justify-center h-full text-muted-foreground">
      <div class="text-center">
        <p class="text-lg font-medium">Loading IOB data...</p>
      </div>
    </div>
  {:else if error}
    <div class="flex items-center justify-center h-full text-muted-foreground">
      <div class="text-center">
        <p class="text-lg font-medium text-destructive">
          Error loading IOB data
        </p>
        <p class="text-sm">{error}</p>
      </div>
    </div>
  {:else if chartData.length > 0 && chartData.some((h) => h.totalIOB > 0)}
    <AreaChart
      legend
      data={chartData}
      x="timeSlot"
      series={[
        {
          key: "bolusIOB",
          color: "var(--iob-temporary)",
          label: "Bolus IOB (U)",
        },
        {
          key: "basalIOB",
          color: "var(--iob-basal)",
          label: "Basal IOB (U)",
        },
      ]}
      props={{
        xAxis: {
          format: (d) => {
            const index = Math.floor(d);
            if (index < 0 || index >= chartData.length) return "";
            const intervalsPerHour = 60 / intervalMinutes;
            const hour = Math.floor(index / intervalsPerHour);

            // Show labels at appropriate intervals based on time scale
            let showLabel = false;
            if (intervalMinutes <= 5) {
              // For 5-minute intervals, show every 2 hours (every 24th interval)
              showLabel = index % (intervalsPerHour * 2) === 0;
            } else if (intervalMinutes <= 15) {
              // For 15-minute intervals, show every hour (every 4th interval)
              showLabel = index % intervalsPerHour === 0;
            } else {
              // For 30+ minute intervals, show every hour
              showLabel = index % intervalsPerHour === 0;
            }

            if (showLabel) {
              return hour === 0
                ? "12 AM"
                : hour < 12
                  ? `${hour} AM`
                  : hour === 12
                    ? "12 PM"
                    : `${hour - 12} PM`;
            }
            return "";
          },
        },
        yAxis: {
          format: "metric",
          label: "Insulin on Board (U)",
        },
        tooltip: {
          header: {
            format: (d) => {
              const index = Math.floor(d);
              if (index < 0 || index >= chartData.length) return "";
              return chartData[index].timeLabel;
            },
          },
        },
      }}
      seriesLayout="stack"
      padding={{ top: 20, right: 30, bottom: 40, left: 60 }}
    />
  {:else}
    <div class="flex items-center justify-center h-full text-muted-foreground">
      <div class="text-center">
        <p class="text-lg font-medium">No IOB data available</p>
        <p class="text-sm">No insulin on board found for visualization</p>
      </div>
    </div>
  {/if}
</ChartC.Container>
