"use client"

import { Pie, PieChart, Cell, Label } from "recharts"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

const config = {
  Critical: { label: "Critical", color: "var(--chart-5)" },
  High: { label: "High", color: "var(--chart-4)" },
  Medium: { label: "Medium", color: "var(--chart-2)" },
  Low: { label: "Low", color: "var(--chart-3)" },
} satisfies ChartConfig

export function PriorityChart({
  data,
}: {
  data: { priority: string; count: number }[]
}) {
  const total = data.reduce((acc, d) => acc + d.count, 0)
  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Tickets by Priority</CardTitle>
        <CardDescription>Current open workload breakdown</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col items-center gap-4">
        <ChartContainer config={config} className="mx-auto aspect-square h-[200px]">
          <PieChart>
            <ChartTooltip content={<ChartTooltipContent nameKey="priority" />} />
            <Pie
              data={data}
              dataKey="count"
              nameKey="priority"
              innerRadius={58}
              outerRadius={82}
              strokeWidth={3}
            >
              {data.map((entry) => (
                <Cell
                  key={entry.priority}
                  fill={`var(--color-${entry.priority})`}
                />
              ))}
              <Label
                content={({ viewBox }) => {
                  if (viewBox && "cx" in viewBox && "cy" in viewBox) {
                    return (
                      <text
                        x={viewBox.cx}
                        y={viewBox.cy}
                        textAnchor="middle"
                        dominantBaseline="middle"
                      >
                        <tspan
                          x={viewBox.cx}
                          y={viewBox.cy}
                          className="fill-foreground text-2xl font-semibold"
                        >
                          {total}
                        </tspan>
                        <tspan
                          x={viewBox.cx}
                          y={(viewBox.cy ?? 0) + 20}
                          className="fill-muted-foreground text-xs"
                        >
                          Total
                        </tspan>
                      </text>
                    )
                  }
                }}
              />
            </Pie>
          </PieChart>
        </ChartContainer>
        <div className="grid w-full grid-cols-2 gap-2">
          {data.map((entry) => (
            <div key={entry.priority} className="flex items-center gap-2 text-sm">
              <span
                className="size-2.5 rounded-full"
                style={{ backgroundColor: `var(--color-${entry.priority})` }}
              />
              <span className="text-muted-foreground">{entry.priority}</span>
              <span className="ml-auto font-medium tabular-nums">
                {entry.count}
              </span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
