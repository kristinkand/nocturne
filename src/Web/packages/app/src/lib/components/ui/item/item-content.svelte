<script lang="ts">
  import { cn } from "$lib/utils";
  import type { HTMLAttributes } from "svelte/elements";

  let {
    ref = $bindable(null),
    class: className,
    children,
    ...restProps
  }: HTMLAttributes<HTMLDivElement> & {
    ref?: HTMLDivElement | null;
  } = $props();
</script>

<div
  bind:this={ref}
  class={cn("flex flex-1 flex-col gap-0.5", className)}
  {...restProps}
>
  {@render children?.()}
</div>
