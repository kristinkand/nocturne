<script lang="ts">
  import { Text, PieChart } from "layerchart";
  import type { Treatment } from "$lib/api";
  import { TreatmentEditDialog } from "$lib/components/treatments";

  interface Props {
    treatments: Treatment[];
    basal: number;
    href?: string;
  }

  let { treatments, basal }: Props = $props();

  // Dialog state
  let selectedTreatment = $state<Treatment | null>(null);
  let editDialogOpen = $state(false);

  // Colors - generate shades of blue for individual boluses
  const BASAL_COLOR = "hsl(38, 92%, 50%)"; // Amber for basal

  function getBolusColor(index: number, total: number): string {
    const baseHue = 217;
    const baseSat = 91;
    const lightness = 45 + (index / Math.max(1, total - 1)) * 20;
    return `hsl(${baseHue}, ${baseSat}%, ${lightness}%)`;
  }

  // Get bolus treatments only
  const bolusTreatments = $derived(
    treatments.filter((t) => (t.insulin ?? 0) > 0)
  );

  // Create segment data
  interface SegmentData {
    key: string;
    value: number;
    color: string;
    treatment?: Treatment;
    startAngle: number;
    endAngle: number;
  }

  const segmentData = $derived.by(() => {
    const segments: SegmentData[] = [];
    let currentAngle = 0;

    // Calculate total for angles
    const totalValue =
      bolusTreatments.reduce((sum, t) => sum + (t.insulin ?? 0), 0) + basal;
    if (totalValue === 0) return segments;

    // Add individual bolus segments
    bolusTreatments.forEach((t, i) => {
      const value = t.insulin ?? 0;
      const angleSize = (value / totalValue) * Math.PI * 2;
      segments.push({
        key: "Bolus",
        value,
        color: getBolusColor(i, bolusTreatments.length),
        treatment: t,
        startAngle: currentAngle,
        endAngle: currentAngle + angleSize,
      });
      currentAngle += angleSize;
    });

    // Add basal as a single segment
    if (basal > 0) {
      const angleSize = (basal / totalValue) * Math.PI * 2;
      segments.push({
        key: "Basal",
        value: basal,
        color: BASAL_COLOR,
        startAngle: currentAngle,
        endAngle: currentAngle + angleSize,
      });
    }

    return segments;
  });

  // Calculate total
  const totalBolus = $derived(
    bolusTreatments.reduce((sum, t) => sum + (t.insulin ?? 0), 0)
  );
  const total = $derived(totalBolus + basal);

  // Handle treatment dialog close
  function handleDialogClose() {
    editDialogOpen = false;
    selectedTreatment = null;
  }

  // Handle treatment save (placeholder)
  function handleTreatmentSave(updatedTreatment: Treatment) {
    console.log("Saving treatment:", updatedTreatment);
    editDialogOpen = false;
    selectedTreatment = null;
  }
</script>

<div class="flex flex-col items-center">
  {#if total > 0}
    <div class="h-[140px] w-[140px]">
      <PieChart
        data={segmentData}
        key="key"
        value="value"
        cRange={["var(--iob-basal)", "var(--iob-bolus)"]}
        innerRadius={-30}
        cornerRadius={3}
        padAngle={0.02}
        renderContext={"svg"}
      >
        {#snippet aboveMarks()}
          <Text
            value={`Total: `}
            textAnchor="middle"
            verticalAnchor="middle"
            dy={-8}
            class="fill-muted-foreground tabular-nums"
          />
          <Text
            value={`${total.toFixed(1)}U`}
            textAnchor="middle"
            verticalAnchor="middle"
            dy={16}
            class="fill-muted-foreground tabular-nums"
          />
        {/snippet}
      </PieChart>
    </div>
  {:else}
    <div class="h-[140px] w-[140px] flex items-center justify-center">
      <div class="text-2xl font-bold text-muted-foreground">—</div>
    </div>
  {/if}
</div>

<!-- Treatment Edit Dialog -->
<TreatmentEditDialog
  bind:open={editDialogOpen}
  treatment={selectedTreatment}
  onClose={handleDialogClose}
  onSave={handleTreatmentSave}
/>
