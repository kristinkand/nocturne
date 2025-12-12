<script lang="ts">
  import {
    CurrentBGDisplay,
    BGStatisticsCards,
    GlucoseChartCard,
    RecentEntriesCard,
    RecentTreatmentsCard,
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
  });

  // Get focusHours setting for chart default time range
  const focusHours = $derived(settingsStore.features?.display?.focusHours ?? 3);

  // Algorithm prediction settings - controls whether predictions are calculated and which model to use
  const predictionEnabled = $derived(
    settingsStore.algorithm?.prediction?.enabled ?? true
  );
  const predictionModel = $derived(
    settingsStore.algorithm?.prediction?.model ?? "cone"
  );
</script>

<div class="p-6 space-y-6">
  <CurrentBGDisplay />

  {#if widgets.statistics}
    <BGStatisticsCards />
  {/if}

  {#if widgets.glucoseChart}
    <GlucoseChartCard
      entries={realtimeStore.entries}
      treatments={realtimeStore.treatments}
      showPredictions={widgets.predictions && predictionEnabled}
      defaultFocusHours={focusHours}
      {predictionModel}
    />
  {/if}

  {#if widgets.dailyStats}
    <RecentEntriesCard />
  {/if}

  {#if widgets.treatments}
    <RecentTreatmentsCard />
  {/if}
</div>
