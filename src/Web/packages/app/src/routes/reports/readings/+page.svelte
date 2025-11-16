<script lang="ts">
  import { GlucoseChart } from "$lib/components/glucose-chart";

  let { data } = $props();
</script>

<GlucoseChart
  entries={data.entries}
  treatments={data.treatments}
  dateRange={data.dateRange}
/>
