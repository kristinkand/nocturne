<script lang="ts">
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { LoaderCircle } from "lucide-svelte";

  import { page } from "$app/state";
</script>

<Card class="w-full max-w-2xl mx-auto mt-10">
  <CardHeader>
    <CardTitle class="text-2xl font-bold">{page.status} Error</CardTitle>
  </CardHeader>
  <CardContent>
    <p class="text-lg">An error occurred while processing your request.</p>
    <p class="text-sm text-muted-foreground mt-2">{page.error.message}</p>
    <p class="text-xs text-red-500 mt-4">Error ID: {page.errorId}</p>
    <pre>
            <code>{JSON.stringify(page, null, 2)}</code>
        </pre>
  </CardContent>
</Card>
