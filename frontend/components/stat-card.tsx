import { cn } from "@/lib/utils"
import { Card, CardContent } from "@/components/ui/card"
import { ArrowDownRight, ArrowUpRight } from "lucide-react"

interface StatCardProps {
  label: string
  value: string | number
  icon: React.ComponentType<{ className?: string }>
  trend?: { value: string; direction: "up" | "down"; positive: boolean }
  accent?: "primary" | "info" | "warning" | "success" | "destructive"
  hint?: string
}

const accentMap: Record<NonNullable<StatCardProps["accent"]>, string> = {
  primary: "bg-primary/10 text-primary",
  info: "bg-info/10 text-info",
  warning: "bg-warning/15 text-warning-foreground",
  success: "bg-success/12 text-success",
  destructive: "bg-destructive/10 text-destructive",
}

export function StatCard({
  label,
  value,
  icon: Icon,
  trend,
  accent = "primary",
  hint,
}: StatCardProps) {
  return (
    <Card className="gap-0 py-0">
      <CardContent className="flex items-start justify-between gap-3 p-4">
        <div className="flex flex-col gap-1">
          <span className="text-sm text-muted-foreground">{label}</span>
          <span className="text-2xl font-semibold tabular-nums tracking-tight">
            {value}
          </span>
          {trend ? (
            <span
              className={cn(
                "mt-0.5 inline-flex items-center gap-1 text-xs font-medium",
                trend.positive ? "text-success" : "text-destructive"
              )}
            >
              {trend.direction === "up" ? (
                <ArrowUpRight className="size-3.5" />
              ) : (
                <ArrowDownRight className="size-3.5" />
              )}
              {trend.value}
              {hint ? (
                <span className="font-normal text-muted-foreground">
                  {hint}
                </span>
              ) : null}
            </span>
          ) : hint ? (
            <span className="mt-0.5 text-xs text-muted-foreground">{hint}</span>
          ) : null}
        </div>
        <div
          className={cn(
            "flex size-9 shrink-0 items-center justify-center rounded-lg",
            accentMap[accent]
          )}
        >
          <Icon className="size-5" />
        </div>
      </CardContent>
    </Card>
  )
}
