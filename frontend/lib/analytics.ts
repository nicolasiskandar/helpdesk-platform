import type { Ticket } from "./types"
import type { TicketCategory, TicketPriority } from "./types"
import { RESOLVED_STATUSES } from "./types"

const CATEGORIES: TicketCategory[] = [
  "Hardware",
  "Software",
  "Network",
  "Access",
  "Other",
]

const PRIORITIES: TicketPriority[] = ["Low", "Medium", "High", "Critical"]

const RESOLVED = new Set<Ticket["status"]>(RESOLVED_STATUSES)

export function ticketStats(tickets: Ticket[]) {
  const open = tickets.filter((t) => t.status === "Open").length
  const inProgress = tickets.filter((t) => t.status === "In Progress").length
  const pending = tickets.filter((t) => t.status === "Resolved - Pending Confirmation").length
  const resolved = tickets.filter((t) => RESOLVED.has(t.status)).length
  const critical = tickets.filter(
    (t) => t.priority === "Critical" && !RESOLVED.has(t.status)
  ).length
  const unassigned = tickets.filter((t) => !t.assigneeId).length
  return { open, inProgress, pending, resolved, critical, unassigned, total: tickets.length }
}

export function byCategory(tickets: Ticket[]) {
  return CATEGORIES.map((category) => ({
    category,
    count: tickets.filter((t) => t.category === category).length,
  }))
}

export function byPriority(tickets: Ticket[]) {
  return PRIORITIES.map((priority) => ({
    priority,
    count: tickets.filter((t) => t.priority === priority).length,
  }))
}

export function byStatus(tickets: Ticket[]) {
  return [
    { status: "Open" as const, key: "open" },
    { status: "In Progress" as const, key: "inProgress" },
    { status: "Resolved - Pending Confirmation" as const, key: "pending" },
    { status: "Closed" as const, key: "closed" },
    { status: "Resolved by AI" as const, key: "resolved" },
  ].map(({ status }) => ({
    status,
    count: tickets.filter((t) => t.status === status).length,
  }))
}

export function agentPerformance(tickets: Ticket[], userMap: Record<string, string> = {}) {
  // Group by assigneeId and compute stats
  const assignees = new Map<string, { assigned: number; resolved: number }>()
  for (const t of tickets) {
    if (!t.assigneeId) continue
    const existing = assignees.get(t.assigneeId) || { assigned: 0, resolved: 0 }
    existing.assigned++
    if (RESOLVED.has(t.status)) existing.resolved++
    assignees.set(t.assigneeId, existing)
  }
  return Array.from(assignees.entries()).map(([id, stats]) => ({
    id,
    name: userMap[id] || id.slice(0, 8),
    assigned: stats.assigned,
    resolved: stats.resolved,
    active: stats.assigned - stats.resolved,
  }))
}

// Date formatting helpers.

function parseUtcDate(iso: string): Date {
  const dateStr = /[Zz]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : iso + "Z"
  return new Date(dateStr)
}

export function formatRelative(iso: string): string {
  const then = parseUtcDate(iso).getTime()
  const now = Date.now()
  const diff = Math.max(0, now - then)
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return "just now"
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`
  return new Date(iso).toLocaleDateString()
}

export function formatDate(iso: string): string {
  return parseUtcDate(iso).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  })
}

export function formatDateTime(iso: string): string {
  return parseUtcDate(iso).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  })
}

export function formatDuration(minutes: number | null | undefined): string {
  if (minutes == null || minutes < 0) return "—"
  const total = Math.round(minutes)
  const hours = Math.floor(total / 60)
  const mins = total % 60
  if (hours === 0) return `${mins}m`
  return mins === 0 ? `${hours}h` : `${hours}h ${mins}m`
}

export function formatFileSize(bytes: number): string {
  if (bytes <= 0) return "—"
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
