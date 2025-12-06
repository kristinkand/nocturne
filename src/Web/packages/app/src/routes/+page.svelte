<script lang="ts">
  import {
    CurrentBGDisplay,
    BGStatisticsCards,
    GlucoseChartCard,
    RecentEntriesCard,
    RecentTreatmentsCard,
    BatteryStatusCard,
  } from "$lib/components/dashboard";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { getSettingsStore } from "$lib/stores/settings-store.svelte";

  const realtimeStore = getRealtimeStore();
  const settingsStore = getSettingsStore();

  // Dashboard widget visibility settings with defaults (show all if not configured)
  const widgets = $derived({
    glucoseChart:
      settingsStore.features?.dashboardWidgets?.glucoseChart ?? true,
    statistics: settingsStore.features?.dashboardWidgets?.statistics ?? true,
    treatments: settingsStore.features?.dashboardWidgets?.treatments ?? true,
    predictions: settingsStore.features?.dashboardWidgets?.predictions ?? true,
    dailyStats: settingsStore.features?.dashboardWidgets?.dailyStats ?? true,
    batteryStatus:
      settingsStore.features?.dashboardWidgets?.batteryStatus ?? true,
  });

  // Get focusHours setting for chart default time range
  const focusHours = $derived(settingsStore.features?.display?.focusHours ?? 3);
</script>

<div class="p-6 space-y-6">
  <CurrentBGDisplay />

  {#if widgets.statistics || widgets.batteryStatus}
    <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
      {#if widgets.statistics}
        <div class="md:col-span-3">
          <BGStatisticsCards />
        </div>
      {/if}
      {#if widgets.batteryStatus}
        <BatteryStatusCard />
      {/if}
    </div>
  {/if}

  {#if widgets.glucoseChart}
    <GlucoseChartCard
      entries={realtimeStore.entries}
      treatments={realtimeStore.treatments}
      showPredictions={widgets.predictions}
      defaultFocusHours={focusHours}
    />
  {/if}

  {#if widgets.dailyStats}
    <RecentEntriesCard />
  {/if}

  {#if widgets.treatments}
    <RecentTreatmentsCard />
  {/if}
</div>
