<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/state";
  import { enhance } from "$app/forms";

  import TreatmentsTable from "$lib/components/TreatmentsTable.svelte";
  import type { Treatment } from "$lib/stores/serverSettings.js";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Badge } from "$lib/components/ui/badge";
  import * as Card from "$lib/components/ui/card";
  import * as Alert from "$lib/components/ui/alert";
  import {
    formatInsulinDisplay,
    formatCarbDisplay,
  } from "$lib/utils/calculate/treatment-stats.ts";

  let { data, form } = $props();
  // Component state
  let selectedTreatment = $state<Treatment | null>(null);
  let showEditModal = $state(false);
  let showDeleteConfirm = $state(false);
  let showBulkDeleteConfirm = $state(false);
  let treatmentToDelete = $state<Treatment | null>(null);
  let treatmentsToDelete = $state<Treatment[]>([]);
  let isLoading = $state(false);
  let statusMessage = $state<{
    type: "success" | "error";
    text: string;
  } | null>(null);
  let selectedEventTypes = $state<string[]>([]);
  let searchQuery = $state("");

  // Status message handling for form results
  $effect(() => {
    if (form?.message) {
      showStatus("success", form.message);

      // Handle update result
      if (form.updatedTreatment) {
        const index = data.treatments.findIndex(
          (t) => t._id === form.updatedTreatment._id
        );
        if (index !== -1) {
          data.treatments[index] = { ...form.updatedTreatment };
          data = { ...data }; // Trigger reactivity
        }
        showEditModal = false;
        selectedTreatment = null;
      }

      // Handle delete result
      if (form.deletedTreatmentId) {
        data.treatments = data.treatments.filter(
          (t) => t._id !== form.deletedTreatmentId
        );
        data = { ...data }; // Trigger reactivity
        showDeleteConfirm = false;
        treatmentToDelete = null;
      }

      // Handle bulk delete result
      if (form.deletedTreatmentIds) {
        data.treatments = data.treatments.filter(
          (t) => !form.deletedTreatmentIds.includes(t._id)
        );
        data = { ...data }; // Trigger reactivity
        showBulkDeleteConfirm = false;
        treatmentsToDelete = [];
      }
    } else if (form?.error) {
      showStatus("error", form.error);
    }
  });

  // Filter state
  let filteredTreatments = $derived.by(() => {
    let filtered = data.treatments;

    // Filter by event type
    if (selectedEventTypes.length > 0) {
      filtered = filtered.filter((t) =>
        selectedEventTypes.includes(t.eventType || "")
      );
    }

    // Filter by search query
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (t) =>
          t.eventType?.toLowerCase().includes(query) ||
          t.notes?.toLowerCase().includes(query) ||
          t.enteredBy?.toLowerCase().includes(query) ||
          t.reason?.toLowerCase().includes(query)
      );
    }

    return filtered;
  });

  // Get unique event types for filter
  let eventTypes = $derived.by(() => {
    const types = new Set(
      data.treatments.map((t) => t.eventType).filter(Boolean)
    );
    return Array.from(types).sort();
  });

  // Treatment actions
  function editTreatment(treatment: Treatment) {
    selectedTreatment = treatment;
    showEditModal = true;
  }
  function confirmDelete(treatment: Treatment) {
    treatmentToDelete = treatment;
    showDeleteConfirm = true;
  }

  function confirmBulkDelete(treatments: Treatment[]) {
    treatmentsToDelete = treatments;
    showBulkDeleteConfirm = true;
  }
  // Form action handlers
  function handleUpdateTreatment(updatedTreatment: Treatment) {
    selectedTreatment = updatedTreatment;
  }

  function showStatus(type: "success" | "error", text: string) {
    statusMessage = { type, text };
    setTimeout(() => {
      statusMessage = null;
    }, 5000);
  }

  // Format function for delete confirmation modal
  function formatDate(dateStr: string | undefined): string {
    if (!dateStr) return "-";
    const date = new Date(dateStr);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString();
  }

  // Event type filter handling
  function toggleEventType(eventType: string) {
    if (selectedEventTypes.includes(eventType)) {
      selectedEventTypes = selectedEventTypes.filter((t) => t !== eventType);
    } else {
      selectedEventTypes = [...selectedEventTypes, eventType];
    }
  }

  function clearFilters() {
    selectedEventTypes = [];
    searchQuery = "";
  }
</script>

<div class="space-y-6">
  <!-- Status Messages -->
  {#if statusMessage}
    <div>
      <div class="flex items-center justify-between">
        <span class="text-sm">{statusMessage.text}</span>
        <Button
          variant="ghost"
          size="sm"
          onclick={() => (statusMessage = null)}
        >
          ✕
        </Button>
      </div>
    </div>
  {/if}
  <!-- Show date range info -->
  <div class="text-center text-sm text-muted-foreground mb-6">
    Showing treatments from {new Date(data.dateRange.from).toLocaleDateString()}
    to {new Date(data.dateRange.to).toLocaleDateString()}
  </div>
  <!-- Controls -->
  <div class="bg-card border border-border rounded-lg p-4 space-y-4">
    <!-- Filters -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <!-- Search -->
      <div>
        <Label for="search-input">Search</Label>
        <Input
          id="search-input"
          bind:value={searchQuery}
          type="text"
          placeholder="Search by event type, notes, entered by..."
        />
      </div>

      <!-- Event Type Filter -->
      <div>
        <Label>Event Types</Label>
        <div class="flex flex-wrap gap-2 mt-2">
          {#each eventTypes as eventType (eventType)}
            <Badge
              variant={selectedEventTypes.includes(eventType)
                ? "default"
                : "outline"}
              class="cursor-pointer"
              onclick={() => toggleEventType(eventType)}
            >
              {eventType}
            </Badge>
          {/each}
        </div>
      </div>
    </div>
    <!-- Filter Summary -->
    {#if selectedEventTypes.length > 0 || searchQuery.trim()}
      <div class="flex items-center justify-between">
        <div class="text-sm text-muted-foreground">
          Showing {filteredTreatments.length} of {data.treatments.length}
          treatments
        </div>
        <Button variant="ghost" size="sm" onclick={clearFilters}>
          Clear Filters
        </Button>
      </div>
    {/if}
  </div>
  <!-- Treatments Table -->
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <TreatmentsTable
      treatments={filteredTreatments}
      onEdit={editTreatment}
      onDelete={confirmDelete}
      onBulkDelete={confirmBulkDelete}
    />
  </div>

  <!-- Summary Stats -->
  <div class="bg-card border border-border rounded-lg p-4">
    <h3 class="text-lg font-semibold mb-3">Summary</h3>
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
      <div>
        <div class="text-2xl font-bold text-primary">
          {filteredTreatments.length}
        </div>
        <div class="text-sm text-muted-foreground">Total Treatments</div>
      </div>
      <div>
        <div class="text-2xl font-bold text-blue-600">
          {filteredTreatments.filter((t: Treatment) => t.insulin).length}
        </div>
        <div class="text-sm text-muted-foreground">With Insulin</div>
      </div>
      <div>
        <div class="text-2xl font-bold text-green-600">
          {filteredTreatments.filter((t: Treatment) => t.carbs).length}
        </div>
        <div class="text-sm text-muted-foreground">With Carbs</div>
      </div>
      <div>
        <div class="text-2xl font-bold text-purple-600">
          {eventTypes.length}
        </div>
        <div class="text-sm text-muted-foreground">Event Types</div>
      </div>
    </div>
  </div>
</div>

<!-- Edit Modal -->
{#if showEditModal && selectedTreatment}
  <TreatmentEditModal
    treatment={selectedTreatment}
    onSave={handleUpdateTreatment}
    onCancel={() => {
      showEditModal = false;
      selectedTreatment = null;
    }}
  />
{/if}

<!-- Delete Confirmation Modal -->
{#if showDeleteConfirm && treatmentToDelete}
  <div
    class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
  >
    <Card.Root class="max-w-md w-full">
      <Card.Header>
        <Card.Title>Delete Treatment</Card.Title>
        <Card.Description>
          Are you sure you want to delete this {treatmentToDelete.eventType} treatment?
          This action cannot be undone.
        </Card.Description>
      </Card.Header>

      <Card.Content>
        <Alert.Root>
          <Alert.Title>Treatment Details</Alert.Title>
          <Alert.Description>
            <div class="space-y-1 text-sm">
              <div>
                <strong>Time:</strong>
                {formatDate(treatmentToDelete.created_at)}
              </div>
              <div>
                <strong>Type:</strong>
                {treatmentToDelete.eventType}
              </div>
              {#if treatmentToDelete.insulin}
                <div>
                  <strong>Insulin:</strong>
                  {formatInsulinDisplay(treatmentToDelete.insulin)}U
                </div>
              {/if}
              {#if treatmentToDelete.carbs}
                <div>
                  <strong>Carbs:</strong>
                  {formatCarbDisplay(treatmentToDelete.carbs)}g
                </div>
              {/if}
            </div>
          </Alert.Description>
        </Alert.Root>
      </Card.Content>

      <Card.Footer class="flex gap-3">
        <Button
          type="button"
          variant="secondary"
          class="flex-1"
          onclick={() => {
            showDeleteConfirm = false;
            treatmentToDelete = null;
          }}
          disabled={isLoading}
        >
          Cancel
        </Button>
        <form
          method="POST"
          action="?/deleteTreatment"
          style="flex: 1;"
          use:enhance={() => {
            isLoading = true;
            return async ({ result, update }) => {
              isLoading = false;
              await update();
            };
          }}
        >
          <input
            type="hidden"
            name="treatmentId"
            value={treatmentToDelete._id}
          />
          <Button
            type="submit"
            variant="destructive"
            class="w-full"
            disabled={isLoading}
          >
            {isLoading ? "Deleting..." : "Delete"}
          </Button>
        </form>
      </Card.Footer>
    </Card.Root>
  </div>
{/if}

<!-- Bulk Delete Confirmation Modal -->
{#if showBulkDeleteConfirm && treatmentsToDelete.length > 0}
  <div
    class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
  >
    <Card.Root class="max-w-lg w-full">
      <Card.Header>
        <Card.Title>Delete {treatmentsToDelete.length} Treatments</Card.Title>
        <Card.Description>
          Are you sure you want to delete {treatmentsToDelete.length} selected treatment{treatmentsToDelete.length !==
          1
            ? "s"
            : ""}? This action cannot be undone.
        </Card.Description>
      </Card.Header>

      <Card.Content>
        <Alert.Root>
          <Alert.Title>Selected Treatments</Alert.Title>
          <Alert.Description>
            <div class="space-y-2 text-sm max-h-48 overflow-y-auto">
              {#each treatmentsToDelete.slice(0, 5) as treatment}
                <div
                  class="flex justify-between items-center py-1 border-b border-border last:border-b-0"
                >
                  <div>
                    <div class="font-medium">
                      {treatment.eventType || "Unknown"}
                    </div>
                    <div class="text-xs text-muted-foreground">
                      {formatDate(treatment.created_at)}
                    </div>
                  </div>
                  <div class="text-xs">
                    {#if treatment.insulin}
                      {formatInsulinDisplay(treatment.insulin)}U
                    {/if}
                    {#if treatment.carbs}
                      {formatCarbDisplay(treatment.carbs)}g
                    {/if}
                  </div>
                </div>
              {/each}
              {#if treatmentsToDelete.length > 5}
                <div class="text-xs text-muted-foreground text-center py-2">
                  ... and {treatmentsToDelete.length - 5} more treatments
                </div>
              {/if}
            </div>
          </Alert.Description>
        </Alert.Root>
      </Card.Content>

      <Card.Footer class="flex gap-3">
        <Button
          type="button"
          variant="secondary"
          class="flex-1"
          onclick={() => {
            showBulkDeleteConfirm = false;
            treatmentsToDelete = [];
          }}
          disabled={isLoading}
        >
          Cancel
        </Button>
        <form
          method="POST"
          action="?/bulkDeleteTreatments"
          style="flex: 1;"
          use:enhance={() => {
            isLoading = true;
            return async ({ result, update }) => {
              isLoading = false;
              await update();
            };
          }}
        >
          {#each treatmentsToDelete as treatment}
            <input type="hidden" name="treatmentIds" value={treatment._id} />
          {/each}
          <Button
            type="submit"
            variant="destructive"
            class="w-full"
            disabled={isLoading}
          >
            {isLoading
              ? "Deleting..."
              : `Delete ${treatmentsToDelete.length} Treatment${treatmentsToDelete.length !== 1 ? "s" : ""}`}
          </Button>
        </form>
      </Card.Footer>
    </Card.Root>
  </div>
{/if}
