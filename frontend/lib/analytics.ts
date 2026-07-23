import type { Ticket } from "./types"
import type { TicketCategory, TicketPriority } from "./types"

const CATEGORIES: TicketCategory[] = [
  "Hardware",
  "Software",
  "Network",
  "Access",
  "Other",
]

const PRIORITIES: TicketPriority[] = ["Low", "Medium", "High", "Critical"]

export function ticketStats(tickets: Ticket[]) {
  const open = tickets.filter((t) => t.status === "Open").length
  const inProgress = tickets.filter((t) => t.status === "In Progress").length
  const pending = tickets.filter((t) => t.status === "Pending").length
  const resolved = tickets.filter(
    (t) => t.status === "Resolved" || t.status === "Closed"
  ).length
  const critical = tickets.filter(
    (t) => t.priority === "Critical" && t.status !== "Closed" && t.status !== "Resolved"
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
    { status: "Open", key: "open" },
    { status: "In Progress", key: "inProgress" },
    { status: "Pending", key: "pending" },
    { status: "Resolved", key: "resolved" },
    { status: "Closed", key: "closed" },
  ].map(({ status }) => ({
    status,
    count: tickets.filter((t) => t.status === status).length,
  }))
}

export function agentPerformance(tickets: Ticket[]) {
  // Group by assigneeId and compute stats
  const assignees = new Map<string, { assigned: number; resolved: number }>()
  for (const t of tickets) {
    if (!t.assigneeId) continue
    const existing = assignees.get(t.assigneeId) || { assigned: 0, resolved: 0 }
    existing.assigned++
    if (t.status === "Resolved" || t.status === "Closed") existing.resolved++
    assignees.set(t.assigneeId, existing)
  }
  return Array.from(assignees.entries()).map(([id, stats]) => ({
    id,
    name: id.slice(0, 8),
    assigned: stats.assigned,
    resolved: stats.resolved,
    active: stats.assigned - stats.resolved,
  }))
}

// Synthetic 6-month trend for reports.
export function ticketTrend() {
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"]
  const created = [142, 168, 155, 189, 176, 203]
  const resolved = [138, 160, 151, 182, 171, 197]
  return months.map((month, i) => ({
    month,
    created: created[i],
    resolved: resolved[i],
  }))
}

export function resolutionTimeTrend() {
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"]
  const hours = [9.4, 8.7, 8.9, 7.6, 7.1, 6.8]
  return months.map((month, i) => ({ month, hours: hours[i] }))
}

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
