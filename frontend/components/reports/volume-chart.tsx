"use client"

import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  ChartLegend,
  ChartLegendContent,
  type ChartConfig,
} from "@/components/ui/chart"

const config = {
  created: { label: "Created", color: "var(--chart-1)" },
  resolved: { label: "Resolved", color: "var(--chart-3)" },
} satisfies ChartConfig

export function VolumeChart({
  data,
}: {
  data: { month: string; created: number; resolved: number }[]
}) {
  return (
    <ChartContainer config={config} className="h-[280px] w-full">
      <AreaChart data={data} margin={{ left: 4, right: 12, top: 8 }}>
        <defs>
          <linearGradient id="fillCreated" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="var(--color-created)" stopOpacity={0.3} />
            <stop offset="95%" stopColor="var(--color-created)" stopOpacity={0.03} />
          </linearGradient>
          <linearGradient id="fillResolved" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="var(--color-resolved)" stopOpacity={0.3} />
            <stop offset="95%" stopColor="var(--color-resolved)" stopOpacity={0.03} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} strokeDasharray="3 3" />
        <XAxis
          dataKey="month"
          tickLine={false}
          axisLine={false}
          tickMargin={8}
        />
        <YAxis tickLine={false} axisLine={false} width={32} />
        <ChartTooltip content={<ChartTooltipContent />} />
        <ChartLegend content={<ChartLegendContent />} />
        <Area
          dataKey="created"
          type="monotone"
          fill="url(#fillCreated)"
          stroke="var(--color-created)"
          strokeWidth={2}
        />
        <Area
          dataKey="resolved"
          type="monotone"
          fill="url(#fillResolved)"
          stroke="var(--color-resolved)"
          strokeWidth={2}
        />
      </AreaChart>
    </ChartContainer>
  )
}
