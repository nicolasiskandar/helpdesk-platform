import { cn } from "@/lib/utils"
import { Badge } from "@/components/ui/badge"
import { statusBadgeClass, priorityMeta } from "@/lib/store"
import type { TicketStatus, TicketPriority } from "@/lib/types"

export function StatusBadge({
  status,
  className,
}: {
  status: TicketStatus
  className?: string
}) {
  return (
    <Badge
      variant="outline"
      className={cn("gap-1.5 font-medium", statusBadgeClass(status), className)}
    >
      {status}
    </Badge>
  )
}

export function PriorityIndicator({
  priority,
  className,
}: {
  priority: TicketPriority
  className?: string
}) {
  const meta = priorityMeta(priority)
  return (
    <span className={cn("inline-flex items-center gap-2 text-sm", className)}>
      <span className={cn("size-2 rounded-full", meta.dot)} aria-hidden />
      {meta.label}
    </span>
  )
}
