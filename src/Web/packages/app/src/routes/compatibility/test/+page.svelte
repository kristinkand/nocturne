<script lang="ts">
  import { runCompatibilityTest } from "./data.remote";
  import { createPatch } from "diff";

  // UI Components
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Textarea } from "$lib/components/ui/textarea";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import * as Select from "$lib/components/ui/select";
  import { ArrowLeft, Play, Loader2 } from "lucide-svelte";

  // Form state
  let nightscoutUrl = $state("");
  let apiSecret = $state("");
  let queryPath = $state("/api/v1/treatments?count=5");
  let method = $state("GET");
  let requestBody = $state("");

  // Options
  let ignoreNocturneFields = $state(true);
  let hideNullValues = $state(true);
  let showSideBySide = $state(false);

  // Known Nocturne-specific fields to ignore
  const nocturneOnlyFields = [
    "sourceConnector",
    "sourceType",
    "syncedAt",
    "nocturneId",
    "additionalProperties",
    "data_source",
    "id",
  ];

  // Result state
  let result = $state<Awaited<ReturnType<typeof runCompatibilityTest>> | null>(
    null
  );
  let isLoading = $state(false);
  let error = $state<string | null>(null);

  // Refs for scroll synchronization
  let leftPanelRef = $state<HTMLPreElement | null>(null);
  let rightPanelRef = $state<HTMLPreElement | null>(null);
  let isScrolling = false;

  function syncScrollLeft() {
    if (isScrolling || !leftPanelRef || !rightPanelRef) return;
    isScrolling = true;
    rightPanelRef.scrollTop = leftPanelRef.scrollTop;
    rightPanelRef.scrollLeft = leftPanelRef.scrollLeft;
    requestAnimationFrame(() => (isScrolling = false));
  }

  function syncScrollRight() {
    if (isScrolling || !leftPanelRef || !rightPanelRef) return;
    isScrolling = true;
    leftPanelRef.scrollTop = rightPanelRef.scrollTop;
    leftPanelRef.scrollLeft = rightPanelRef.scrollLeft;
    requestAnimationFrame(() => (isScrolling = false));
  }

  // Filtered responses (used for both diff and side-by-side view)
  const filteredResponses = $derived.by(() => {
    if (!result?.nightscoutResponse || !result?.nocturneResponse) {
      return { ns: null, nc: null };
    }

    let nsResponse = result.nightscoutResponse;
    let ncResponse = result.nocturneResponse;

    // Apply filters to both responses
    try {
      let nsJson = JSON.parse(nsResponse);
      let ncJson = JSON.parse(ncResponse);

      // Remove null values from Nocturne that don't exist in Nightscout
      if (hideNullValues) {
        ncJson = stripExtraNulls(ncJson, nsJson);
      }

      // Remove Nocturne-specific fields from Nocturne response
      if (ignoreNocturneFields) {
        ncJson = removeNocturneFields(ncJson);
      }

      // Reorder Nocturne keys to match Nightscout's order
      ncJson = reorderToMatch(ncJson, nsJson);

      nsResponse = JSON.stringify(nsJson, null, 2);
      ncResponse = JSON.stringify(ncJson, null, 2);
    } catch {
      // Not valid JSON, skip filtering
    }

    return { ns: nsResponse, nc: ncResponse };
  });

  // Computed diff
  const diffOutput = $derived.by(() => {
    if (!filteredResponses.ns || !filteredResponses.nc) {
      return null;
    }

    return createPatch(
      "Nightscout Response",
      filteredResponses.ns,
      filteredResponses.nc,
      "Nightscout",
      "Nocturne"
    );
  });

  // Strip null values from Nocturne response only if the field doesn't have null in Nightscout
  function stripExtraNulls(nocturneObj: any, nightscoutObj: any): any {
    if (Array.isArray(nocturneObj)) {
      // If both are arrays, process element by element
      if (Array.isArray(nightscoutObj)) {
        return nocturneObj.map((item, index) =>
          stripExtraNulls(item, nightscoutObj[index])
        );
      }
      return nocturneObj.map((item) => stripExtraNulls(item, undefined));
    }

    if (nocturneObj && typeof nocturneObj === "object") {
      const cleaned: Record<string, any> = {};
      for (const [key, value] of Object.entries(nocturneObj)) {
        const nsValue = nightscoutObj?.[key];

        // If the value is null/undefined, only keep it if Nightscout also has null
        if (value === null || value === undefined) {
          if (nsValue === null) {
            // Nightscout has null, keep it
            cleaned[key] = value;
          }
          // Otherwise, skip this field (don't add it to cleaned)
        } else {
          // Value is not null, recursively process
          cleaned[key] = stripExtraNulls(value, nsValue);
        }
      }
      return cleaned;
    }

    return nocturneObj;
  }

  // Remove Nocturne-specific fields recursively
  function removeNocturneFields(obj: any): any {
    if (Array.isArray(obj)) {
      return obj.map(removeNocturneFields);
    }
    if (obj && typeof obj === "object") {
      const cleaned: Record<string, any> = {};
      for (const [key, value] of Object.entries(obj)) {
        if (!nocturneOnlyFields.includes(key)) {
          cleaned[key] = removeNocturneFields(value);
        }
      }
      return cleaned;
    }
    return obj;
  }

  // Reorder Nocturne object keys to match Nightscout's key order
  function reorderToMatch(nocturneObj: any, nightscoutObj: any): any {
    if (Array.isArray(nocturneObj)) {
      if (Array.isArray(nightscoutObj)) {
        return nocturneObj.map((item, index) =>
          reorderToMatch(item, nightscoutObj[index])
        );
      }
      return nocturneObj.map((item) => reorderToMatch(item, undefined));
    }

    if (
      nocturneObj &&
      typeof nocturneObj === "object" &&
      nightscoutObj &&
      typeof nightscoutObj === "object"
    ) {
      const reordered: Record<string, any> = {};
      const nsKeys = Object.keys(nightscoutObj);
      const ncKeys = Object.keys(nocturneObj);

      // First, add keys in Nightscout's order
      for (const key of nsKeys) {
        if (key in nocturneObj) {
          //
          reordered[key] = reorderToMatch(nocturneObj[key], nightscoutObj[key]);
        }
      }

      // Then, add any remaining Nocturne-only keys
      for (const key of ncKeys) {
        if (!(key in reordered)) {
          reordered[key] = nocturneObj[key];
        }
      }

      return reordered;
    }

    return nocturneObj;
  }

  // Parse diff output for display
  const parsedDiff = $derived.by(() => {
    if (!diffOutput) return [];

    const lines = diffOutput.split("\n");
    return lines.map((line, index) => {
      let type: "header" | "add" | "remove" | "context" | "meta" = "context";
      if (line.startsWith("+++") || line.startsWith("---")) {
        type = "meta";
      } else if (line.startsWith("@@")) {
        type = "header";
      } else if (line.startsWith("+")) {
        type = "add";
      } else if (line.startsWith("-")) {
        type = "remove";
      }
      return { line, type, index };
    });
  });

  // Check if responses are identical
  const isIdentical = $derived(
    result?.nightscoutResponse &&
      result?.nocturneResponse &&
      parsedDiff.every((l) => l.type === "context" || l.type === "meta")
  );

  async function runTest() {
    if (!nightscoutUrl || !queryPath) {
      error = "Please enter both Nightscout URL and Query Path";
      return;
    }

    isLoading = true;
    error = null;
    result = null;

    try {
      result = await runCompatibilityTest({
        nightscoutUrl,
        apiSecret: apiSecret || undefined,
        queryPath,
        method,
        requestBody: requestBody || undefined,
      });
    } catch (err) {
      error = err instanceof Error ? err.message : "An error occurred";
    } finally {
      isLoading = false;
    }
  }

  function formatDuration(ms: number | undefined) {
    if (ms === undefined) return "N/A";
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
  }
</script>

<div class="container mx-auto p-6 space-y-6">
  <!-- Header -->
  <div class="flex justify-between items-center">
    <div>
      <h1 class="text-3xl font-bold">Manual Compatibility Test</h1>
      <p class="text-muted-foreground mt-1">
        Compare API responses between Nightscout and Nocturne
      </p>
    </div>
    <Button variant="outline" href="/compatibility">
      <ArrowLeft class="h-4 w-4 mr-2" />
      Back to Dashboard
    </Button>
  </div>

  <!-- Test Form -->
  <Card.Root>
    <Card.Header>
      <Card.Title>Test Configuration</Card.Title>
      <Card.Description>
        Enter the Nightscout server details and API path to test
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div class="space-y-2">
          <Label for="nightscoutUrl">Nightscout URL</Label>
          <Input
            id="nightscoutUrl"
            type="url"
            bind:value={nightscoutUrl}
            placeholder="https://your-nightscout.herokuapp.com"
          />
        </div>

        <div class="space-y-2">
          <Label for="apiSecret">
            API Secret
            <span class="text-muted-foreground font-normal ml-1">
              (SHA1 hash or plain)
            </span>
          </Label>
          <Input
            id="apiSecret"
            type="password"
            bind:value={apiSecret}
            placeholder="Enter API secret"
          />
        </div>

        <div class="md:col-span-2 space-y-2">
          <Label for="queryPath">Query Path</Label>
          <div class="flex gap-2">
            <Select.Root type="single" bind:value={method}>
              <Select.Trigger class="w-[100px]">
                {method}
              </Select.Trigger>
              <Select.Content>
                <Select.Item value="GET">GET</Select.Item>
                <Select.Item value="POST">POST</Select.Item>
              </Select.Content>
            </Select.Root>
            <Input
              id="queryPath"
              bind:value={queryPath}
              class="flex-1 font-mono"
              placeholder="/api/v1/entries?count=10"
            />
          </div>
        </div>

        {#if method === "POST"}
          <div class="md:col-span-2 space-y-2">
            <Label for="requestBody">Request Body (JSON)</Label>
            <Textarea
              id="requestBody"
              bind:value={requestBody}
              class="font-mono h-24"
              placeholder={"key:value"}
            />
          </div>
        {/if}
      </div>

      <!-- Options -->
      <div class="pt-4 border-t space-y-3">
        <div class="flex items-center gap-2">
          <Checkbox
            id="ignoreNocturneFields"
            checked={ignoreNocturneFields}
            onCheckedChange={(checked) =>
              (ignoreNocturneFields = checked === true)}
          />
          <Label for="ignoreNocturneFields" class="font-normal">
            Ignore Nocturne-specific fields
            <span class="text-muted-foreground ml-1">
              ({nocturneOnlyFields.join(", ")})
            </span>
          </Label>
        </div>
        <div class="flex items-center gap-2">
          <Checkbox
            id="hideNullValues"
            checked={hideNullValues}
            onCheckedChange={(checked) => (hideNullValues = checked === true)}
          />
          <Label for="hideNullValues" class="font-normal">
            Hide null values
          </Label>
        </div>
        <div class="flex items-center gap-2">
          <Checkbox
            id="showSideBySide"
            checked={showSideBySide}
            onCheckedChange={(checked) => (showSideBySide = checked === true)}
          />
          <Label for="showSideBySide" class="font-normal">
            Show side-by-side view
          </Label>
        </div>
      </div>
    </Card.Content>
    <Card.Footer>
      <Button onclick={runTest} disabled={isLoading}>
        {#if isLoading}
          <Loader2 class="h-4 w-4 mr-2 animate-spin" />
          Testing...
        {:else}
          <Play class="h-4 w-4 mr-2" />
          Run Test
        {/if}
      </Button>
    </Card.Footer>
  </Card.Root>

  {#if error}
    <Card.Root class="border-destructive">
      <Card.Content class="py-4">
        <p class="text-destructive">{error}</p>
      </Card.Content>
    </Card.Root>
  {/if}

  <!-- Results -->
  {#if result}
    <!-- Status Cards -->
    <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
      <Card.Root>
        <Card.Content class="pt-6">
          <p class="text-sm text-muted-foreground mb-1">Nightscout Status</p>
          <p
            class="text-2xl font-bold {result.nightscoutStatusCode === 200
              ? 'text-green-600'
              : 'text-destructive'}"
          >
            {result.nightscoutStatusCode ?? "Error"}
          </p>
          {#if result.nightscoutError}
            <p class="text-xs text-destructive mt-1">
              {result.nightscoutError}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <Card.Root>
        <Card.Content class="pt-6">
          <p class="text-sm text-muted-foreground mb-1">Nocturne Status</p>
          <p
            class="text-2xl font-bold {result.nocturneStatusCode === 200
              ? 'text-green-600'
              : 'text-destructive'}"
          >
            {result.nocturneStatusCode ?? "Error"}
          </p>
          {#if result.nocturneError}
            <p class="text-xs text-destructive mt-1">{result.nocturneError}</p>
          {/if}
        </Card.Content>
      </Card.Root>

      <Card.Root>
        <Card.Content class="pt-6">
          <p class="text-sm text-muted-foreground mb-1">Nightscout Time</p>
          <p class="text-2xl font-bold">
            {formatDuration(result.nightscoutResponseTimeMs)}
          </p>
        </Card.Content>
      </Card.Root>

      <Card.Root>
        <Card.Content class="pt-6">
          <p class="text-sm text-muted-foreground mb-1">Nocturne Time</p>
          <p class="text-2xl font-bold">
            {formatDuration(result.nocturneResponseTimeMs)}
          </p>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Match Status -->
    <Card.Root
      class={isIdentical
        ? "border-green-500 bg-green-50 dark:bg-green-900/20"
        : "border-yellow-500 bg-yellow-50 dark:bg-yellow-900/20"}
    >
      <Card.Content class="py-4">
        <p
          class="font-semibold {isIdentical
            ? 'text-green-700 dark:text-green-300'
            : 'text-yellow-700 dark:text-yellow-300'}"
        >
          {isIdentical
            ? "✓ Responses are identical"
            : "⚠ Responses have differences (see diff below)"}
        </p>
      </Card.Content>
    </Card.Root>

    <!-- Diff View -->
    {#if showSideBySide}
      <!-- Side by Side View -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card.Root>
          <Card.Header class="py-3">
            <Card.Title class="text-base">Nightscout Response</Card.Title>
          </Card.Header>
          <Card.Content class="p-0">
            <pre
              bind:this={leftPanelRef}
              onscroll={syncScrollLeft}
              class="p-4 overflow-x-auto text-sm font-mono max-h-[600px] overflow-y-auto bg-muted/50">{filteredResponses.ns ||
                result.nightscoutError ||
                "No response"}</pre>
          </Card.Content>
        </Card.Root>
        <Card.Root>
          <Card.Header class="py-3">
            <Card.Title class="text-base">Nocturne Response</Card.Title>
          </Card.Header>
          <Card.Content class="p-0">
            <pre
              bind:this={rightPanelRef}
              onscroll={syncScrollRight}
              class="p-4 overflow-x-auto text-sm font-mono max-h-[600px] overflow-y-auto bg-muted/50">{filteredResponses.nc ||
                result.nocturneError ||
                "No response"}</pre>
          </Card.Content>
        </Card.Root>
      </div>
    {:else}
      <!-- Unified Diff View -->
      <Card.Root>
        <Card.Header class="py-3 flex-row justify-between items-center">
          <Card.Title class="text-base">Unified Diff</Card.Title>
          <span class="text-sm text-muted-foreground">
            <span class="text-red-600">- Nightscout</span>
            {" / "}
            <span class="text-green-600">+ Nocturne</span>
          </span>
        </Card.Header>
        <Card.Content class="p-0">
          <div class="overflow-x-auto max-h-[600px] overflow-y-auto">
            <pre class="text-sm font-mono">{#each parsedDiff as { line, type }}
                <span
                  class="block px-4 py-0.5 {type === 'add'
                    ? 'bg-green-100 dark:bg-green-900/30 text-green-800 dark:text-green-200'
                    : type === 'remove'
                      ? 'bg-red-100 dark:bg-red-900/30 text-red-800 dark:text-red-200'
                      : type === 'header'
                        ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-200'
                        : type === 'meta'
                          ? 'text-muted-foreground'
                          : ''}">{line}</span>
              {/each}</pre>
          </div>
        </Card.Content>
      </Card.Root>
    {/if}
  {/if}
</div>
