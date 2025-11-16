<script lang="ts">
  import {
    Table,
    TableBody,
    TableCaption,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
  } from "$lib/components/ui/table";
  import { Button } from "$lib/components/ui/button";
  import { Edit, Trash2 } from "lucide-svelte";
  import { formatNotes } from "$lib/utils/treatment-formatting";
  import { getEventType } from "$lib/constants/event-types";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import type { Treatment } from "$lib/api";

  interface Props {
    treatments: Treatment[];
    onEdit: (treatment: Treatment) => void;
    onDelete: (treatment: Treatment) => void;
    onBulkDelete?: (treatments: Treatment[]) => void;
  }

  let { treatments, onEdit, onDelete, onBulkDelete }: Props = $props();

  // Selection state
  let selectedTreatments = $state<Set<string>>(new Set());
  let isAllSelected = $derived(
    selectedTreatments.size === treatments.length && treatments.length > 0
  );
  let isSomeSelected = $derived(
    selectedTreatments.size > 0 && selectedTreatments.size < treatments.length
  );

  // Selection functions
  function toggleTreatmentSelection(treatmentId: string) {
    const newSelected = new Set(selectedTreatments);
    if (newSelected.has(treatmentId)) {
      newSelected.delete(treatmentId);
    } else {
      newSelected.add(treatmentId);
    }
    selectedTreatments = newSelected;
  }

  function toggleAllTreatments() {
    if (isAllSelected) {
      selectedTreatments = new Set();
    } else {
      selectedTreatments = new Set(treatments.map((t) => t._id));
    }
  }

  function handleBulkDelete() {
    const selectedTreatmentObjects = treatments.filter((t) =>
      selectedTreatments.has(t._id)
    );
    if (onBulkDelete && selectedTreatmentObjects.length > 0) {
      onBulkDelete(selectedTreatmentObjects);
    }
  }
  const allColumns = [
    { key: "select", label: "Select" },
    { key: "time", label: "Time" },
    { key: "eventType", label: "Event Type" },
    { key: "bloodGlucose", label: "Blood Glucose" },
    { key: "insulin", label: "Insulin" },
    { key: "carbs", label: "Carbs/Food/Time" },
    { key: "protein", label: "Protein" },
    { key: "fat", label: "Fat" },
    { key: "duration", label: "Duration" },
    { key: "percent", label: "Percent" },
    { key: "basalValue", label: "Basal Value" },
    { key: "profile", label: "Profile" },
    { key: "enteredBy", label: "Entered By" },
    { key: "notes", label: "Notes" },
    { key: "actions", label: "Actions" },
  ];
  // Function to check if a column has any data
  function hasColumnData(columnKey: string, treatments: Treatment[]): boolean {
    if (
      columnKey === "select" ||
      columnKey === "time" ||
      columnKey === "actions"
    ) {
      return true; // Always show select, time and actions columns
    }

    return treatments.some((treatment) => {
      switch (columnKey) {
        case "eventType":
          return treatment.eventType && treatment.eventType.trim() !== "";
        case "bloodGlucose":
          return treatment.glucose !== undefined && treatment.glucose !== null;
        case "insulin":
          return treatment.insulin !== undefined && treatment.insulin !== null;
        case "carbs":
          return (
            (treatment.carbs !== undefined && treatment.carbs !== null) ||
            (treatment.absorptionTime !== undefined &&
              treatment.absorptionTime !== null)
          );
        case "protein":
          return treatment.protein !== undefined && treatment.protein !== null;
        case "fat":
          return treatment.fat !== undefined && treatment.fat !== null;
        case "duration":
          return (
            treatment.duration !== undefined && treatment.duration !== null
          );
        case "percent":
          return treatment.percent !== undefined && treatment.percent !== null;
        case "basalValue":
          return (
            (treatment.absolute !== undefined && treatment.absolute !== null) ||
            (treatment.rate !== undefined && treatment.rate !== null)
          );
        case "profile":
          return treatment.profile && treatment.profile.trim() !== "";
        case "enteredBy":
          return treatment.enteredBy && treatment.enteredBy.trim() !== "";
        case "notes":
          return (
            (treatment.notes && treatment.notes.trim() !== "") ||
            (treatment.reason && treatment.reason.trim() !== "")
          );
        default:
          return false;
      }
    });
  }

  // Filter columns to only show those with data
  const visibleColumns = $derived(
    allColumns.filter((column) => hasColumnData(column.key, treatments))
  );

  // Format functions
  function formatDate(dateStr: string | undefined): string {
    if (!dateStr) return "-";
    const date = new Date(dateStr);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString();
  }

  // Simple value formatters for protein, insulin, carbs, fat
  function formatTruthy(value: any): string {
    return value ? value : "-";
  }

  // Utility function to get event type styling
  function getEventTypeStyle(eventTypeVal: string): string {
    const eventType = getEventType(eventTypeVal);
    if (!eventType) return "bg-muted/20 text-muted-foreground border-muted/30";

    // Define styles based on event type categories
    const bolusTypes = [
      "Snack Bolus",
      "Meal Bolus",
      "Correction Bolus",
      "Combo Bolus",
    ];
    const basalTypes = ["Temp Basal Start", "Temp Basal End", "Temp Basal"];
    const sensorTypes = ["Sensor Start", "Sensor Change", "Sensor Stop"];
    const pumpTypes = ["Site Change", "Pump Battery Change", "Insulin Change"];
    const profileTypes = ["Profile Switch"];
    const noteTypes = ["Note", "Announcement", "Question", "D.A.D. Alert"];
    const bgTypes = ["BG Check"];
    const carbTypes = ["Carb Correction"];

    if (bolusTypes.includes(eventType.name)) {
      return "bg-blue-100 text-blue-800 border-blue-200 dark:bg-blue-900/30 dark:text-blue-300 dark:border-blue-700";
    } else if (basalTypes.includes(eventType.name)) {
      return "bg-purple-100 text-purple-800 border-purple-200 dark:bg-purple-900/30 dark:text-purple-300 dark:border-purple-700";
    } else if (sensorTypes.includes(eventType.name)) {
      return "bg-green-100 text-green-800 border-green-200 dark:bg-green-900/30 dark:text-green-300 dark:border-green-700";
    } else if (pumpTypes.includes(eventType.name)) {
      return "bg-orange-100 text-orange-800 border-orange-200 dark:bg-orange-900/30 dark:text-orange-300 dark:border-orange-700";
    } else if (profileTypes.includes(eventType.name)) {
      return "bg-indigo-100 text-indigo-800 border-indigo-200 dark:bg-indigo-900/30 dark:text-indigo-300 dark:border-indigo-700";
    } else if (noteTypes.includes(eventType.name)) {
      return "bg-gray-100 text-gray-800 border-gray-200 dark:bg-gray-800/30 dark:text-gray-300 dark:border-gray-600";
    } else if (bgTypes.includes(eventType.name)) {
      return "bg-red-100 text-red-800 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-700";
    } else if (carbTypes.includes(eventType.name)) {
      return "bg-yellow-100 text-yellow-800 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-300 dark:border-yellow-700";
    }

    return "bg-primary/10 text-primary border-primary/20";
  }
</script>

<section class="overflow-auto max-h-[70vh]">
  {#if selectedTreatments.size > 0}
    <div
      class="mb-4 p-3 bg-muted/50 rounded-md border flex items-center justify-between"
    >
      <span class="text-sm font-medium">
        {selectedTreatments.size} treatment{selectedTreatments.size !== 1
          ? "s"
          : ""} selected
      </span>
      <div class="flex gap-2">
        <Button
          variant="outline"
          size="sm"
          onclick={() => (selectedTreatments = new Set())}
        >
          Clear Selection
        </Button>
        <Button variant="destructive" size="sm" onclick={handleBulkDelete}>
          <Trash2 size={16} class="mr-2" />
          Delete Selected
        </Button>
      </div>
    </div>
  {/if}

  <Table class="relative">
    <TableCaption class="text-sm text-muted-foreground mt-2">
      {treatments.length} treatment{treatments.length !== 1 ? "s" : ""} found
    </TableCaption>
    <TableHeader>
      <TableRow class="bg-background border-b border-border">
        {#each visibleColumns as column}
          <TableHead
            class="px-4 py-3 text-left text-sm font-medium sticky top-0 bg-background z-10"
          >
            {#if column.key === "select"}
              <Checkbox
                checked={isAllSelected}
                indeterminate={isSomeSelected}
                onCheckedChange={toggleAllTreatments}
                aria-label="Select all treatments"
              />
            {:else}
              {column.label}
            {/if}
          </TableHead>
        {/each}
      </TableRow>
    </TableHeader>
    <TableBody class="">
      {#each treatments as treatment (treatment._id)}
        <TableRow class="border-t border-border hover:bg-muted/50">
          {#each visibleColumns as column}
            <TableCell
              class={`px-4 py-3 text-sm ${column.key === "carbs" ? "max-w-48 truncate" : ""} ${column.key === "notes" ? "max-w-64 truncate" : ""}`}
              title={column.key === "carbs"
                ? formatTruthy(treatment)
                : column.key === "notes"
                  ? formatNotes(treatment)
                  : undefined}
            >
              {#if column.key === "select"}
                <Checkbox
                  checked={selectedTreatments.has(treatment._id)}
                  onCheckedChange={() =>
                    toggleTreatmentSelection(treatment._id)}
                  aria-label={`Select treatment ${treatment._id}`}
                />
              {:else if column.key === "time"}
                {formatDate(treatment.created_at)}
              {:else if column.key === "eventType"}
                {#if treatment.eventType}
                  {@const eventType = getEventType(treatment.eventType)}
                  <span
                    class="px-2 py-1 text-xs rounded-full border {getEventTypeStyle(
                      treatment.eventType
                    )}"
                    title={eventType?.name || treatment.eventType}
                  >
                    {eventType?.name || treatment.eventType}
                  </span>
                {:else}
                  <span class="text-muted-foreground">-</span>
                {/if}
              {:else if column.key === "bloodGlucose"}
                <!-- {formatBloodGlucose(treatment)} -->
              {:else if ["insulin", "carbs", "protein", "fat"].includes(column.key)}
                {formatTruthy(treatment.insulin)}
              {:else if column.key === "duration"}
                {treatment.duration !== undefined && treatment.duration !== null
                  ? `${treatment.duration.toFixed(0)} min`
                  : "-"}
              {:else if column.key === "percent"}
                {treatment.percent !== undefined && treatment.percent !== null
                  ? `${treatment.percent}%`
                  : "-"}
              {:else if column.key === "basalValue"}
                {treatment.absolute !== undefined && treatment.absolute !== null
                  ? formatTruthy(treatment.absolute)
                  : treatment.rate !== undefined && treatment.rate !== null
                    ? `${treatment.rate} U/hr`
                    : "-"}
              {:else if column.key === "profile"}
                {treatment.profile || "-"}
              {:else if column.key === "enteredBy"}
                {treatment.enteredBy || "-"}
              {:else if column.key === "notes"}
                {formatNotes(treatment)}
              {:else if column.key === "actions"}
                <div class="flex gap-2">
                  <Button
                    variant="ghost"
                    size="sm"
                    onclick={() => onEdit(treatment)}
                    class="h-8 w-8 p-0"
                    title="Edit treatment"
                  >
                    <Edit size={16} />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onclick={() => onDelete(treatment)}
                    class="h-8 w-8 p-0 text-destructive hover:text-destructive"
                    title="Delete treatment"
                  >
                    <Trash2 size={16} />
                  </Button>
                </div>
              {/if}
            </TableCell>
          {/each}
        </TableRow>
      {/each}
    </TableBody>
  </Table>
</section>
