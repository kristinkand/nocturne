<script lang="ts">
  import Syringe from "lucide-svelte/icons/syringe";
  import type { IconProps } from "./types";

  let { class: className = "", ...rest }: IconProps = $props();
</script>

<!-- Cannula icon - infusion site/cannula change tracker -->
<Syringe class={className} {...rest} />
