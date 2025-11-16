<script lang="ts">
  import type { Entry } from "$lib/api";
  import { Card, CardContent, CardHeader } from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import { formatTime } from "$lib/utils";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";

  interface ComponentProps {
    entries?: Entry[];
    maxEntries?: number;
  }

  let { entries, maxEntries = 5 }: ComponentProps = $props();

  const realtimeStore = getRealtimeStore();

  // Use realtime store entries as fallback when entries prop not provided
  const displayEntries = $derived(entries ?? realtimeStore.entries);
  const recentEntries = $derived(displayEntries.slice(0, maxEntries));
</script>

<Card>
  <CardHeader>Recent Entries</CardHeader>
  <CardContent>
    {#if recentEntries.length > 0}
      <div class="space-y-3">
        {#each recentEntries as entry (entry._id || entry.mills)}
          <div
            class="flex items-center justify-between p-3 bg-muted rounded-lg"
          >
            <div class="flex items-center gap-3">
              <Badge variant="outline">{entry.direction}</Badge>
              <div>
                <div class="font-medium">
                  {#if entry.sgv}
                    {entry.sgv} mg/dL
                  {/if}
                  {#if entry.notes}
                    - {entry.notes}
                  {/if}
                </div>
                <div class="text-sm text-muted-foreground">
                  {formatTime(entry.mills!)}
                </div>
              </div>
            </div>
            <div class="text-sm text-muted-foreground">
              {entry.delta}
            </div>
          </div>
        {/each}
      </div>
    {:else}
      <p class="text-muted-foreground text-center py-8">No recent entries</p>
    {/if}
  </CardContent>
</Card>
