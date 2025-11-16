<script lang="ts">
  import { page } from "$app/state";
  import { invalidateAll } from "$app/navigation";
  import Button from "$lib/components/ui/button/button.svelte";
  import DateRangePicker from "$lib/components/ui/date-range-picker.svelte";
  import { ArrowLeftIcon } from "lucide-svelte";

  let { children, data } = $props();

  // Extract report name from the URL
  const reportName = $derived.by(() => {
    const pathSegments = page.url.pathname.split("/");
    const reportSegment = pathSegments[pathSegments.length - 1];

    if (!reportSegment || reportSegment === "reports") {
      return "Reports";
    }

    // Convert kebab-case to title case
    return (
      reportSegment
        .split("-")
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(" ") + " Report"
    );
  });

  // Determine if we should show the date picker (not on main reports page)
  const showDatePicker = $derived(page.url.pathname !== "/reports");
</script>

<svelte:head>
  <title>{reportName} - Nightscout</title>
  <meta
    name="description"
    content="Nightscout {reportName.toLowerCase()} with comprehensive data analysis and filtering capabilities"
  />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
</svelte:head>

<div class="min-h-screen bg-background">
  {#if page.url.pathname !== "/reports"}
    <!-- Report Header -->
    <div class="border-b border-border bg-card">
      <div class="container mx-auto px-4 py-6">
        <div class="flex items-center justify-between mb-6">
          <div>
            <h1 class="text-3xl font-bold text-foreground">{reportName}</h1>
          </div>

          <Button href="/reports" variant="outline" size="sm">
            <ArrowLeftIcon class="w-4 h-4" />
            <span>Back to Reports</span>
          </Button>
        </div>
        <!-- Date Range Picker -->
        {#if showDatePicker}
          <DateRangePicker 
            defaultDays={7} 
            onDateChange={() => {
              // Invalidate all data when date range changes
              invalidateAll();
            }}
          />
        {/if}
        {data.entries.length} entries
      </div>
    </div>
  {/if}

  <!-- Main Content -->
  <main class="container mx-auto px-4 py-6">
    {@render children()}
  </main>
</div>
