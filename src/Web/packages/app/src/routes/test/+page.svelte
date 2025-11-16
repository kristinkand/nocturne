<script>
  import {
    CurrentBGDisplay,
    BGStatisticsCards,
    GlucoseChartCard,
    RecentTreatmentsCard,
  } from "$lib/components/dashboard";
  import RecentEntriesCard from "$lib/components/dashboard/RecentEntriesCard.svelte";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";

  const realtimeStore = getRealtimeStore();
</script>

<div class="p-6 space-y-6">
  <CurrentBGDisplay />

  <BGStatisticsCards />

  <GlucoseChartCard />

  <RecentEntriesCard />

  <RecentTreatmentsCard />
</div>
