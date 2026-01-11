<script lang="ts">
  import { cn } from "$lib/utils";
  import type { HTMLAttributes } from "svelte/elements";

  let {
    ref = $bindable(null),
    class: className,
    children,
    ...restProps
  }: HTMLAttributes<HTMLSpanElement> & {
    ref?: HTMLSpanElement | null;
  } = $props();
</script>

<span
  bind:this={ref}
  class={cn("text-sm font-medium leading-none", className)}
  {...restProps}
>
  {@render children?.()}
</span>
