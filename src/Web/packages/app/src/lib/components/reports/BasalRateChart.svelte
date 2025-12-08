<script lang="ts">
  import { Chart, Svg, Area, Text, Rule } from "layerchart";
  import { scaleTime, scaleLinear } from "d3-scale";
  import { curveStepAfter } from "d3-shape";

  interface BasalDataPoint {
    time: Date;
    rate: number;
    isTemp?: boolean;
  }

  interface Props {
    /** Timeline data with time, rate, and optional isTemp flag */
    data: BasalDataPoint[];
    /** X-axis domain [start, end] */
    xDomain: [Date, Date];
    /** Default/scheduled basal rate for reference line */
    defaultRate?: number;
    /** Show reference line for default rate */
    showDefaultLine?: boolean;
  }

  let {
    data,
    xDomain,
    defaultRate = 0.8,
    showDefaultLine = true,
  }: Props = $props();

  // Calculate max rate for y-domain with some headroom
  const maxRate = $derived(
    Math.max(...data.map((d) => d.rate), defaultRate * 2, 1) * 1.2
  );
</script>

<div class="basal-chart h-[60px] w-full">
  {#if data.length > 0}
    <Chart
      {data}
      x={(d) => d.time}
      y={(d) => d.rate}
      xScale={scaleTime()}
      yScale={scaleLinear()}
      {xDomain}
      yDomain={[0, maxRate]}
      padding={{ top: 4, right: 30, bottom: 0, left: 50 }}
    >
      <Svg>
        {#if showDefaultLine}
          <Rule
            y={defaultRate}
            class="stroke-muted-foreground/50"
            stroke-dasharray="4,4"
          />
        {/if}

        <Area
          y0={0}
          curve={curveStepAfter}
          class="fill-cyan-500/30 stroke-cyan-400 stroke-1"
        />

        <Text x={4} y={4} class="text-[10px] fill-muted-foreground font-medium">
          BASAL
        </Text>

        {#if showDefaultLine}
          <Text
            x={98}
            y={defaultRate}
            textAnchor="end"
            dy={-2}
            class="text-[8px] fill-muted-foreground"
          >
            {defaultRate.toFixed(2)} U/hr
          </Text>
        {/if}
      </Svg>
    </Chart>
  {:else}
    <div
      class="flex h-full items-center justify-center text-xs text-muted-foreground"
    >
      No basal data available
    </div>
  {/if}
</div>
