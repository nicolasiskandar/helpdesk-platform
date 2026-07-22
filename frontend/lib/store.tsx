"use client"

import * as React from "react"
import {
  apiGetTickets,
  apiGetMyTickets,
  apiGetTicketById,
  apiGetAssignments,
  apiCreateTicket,
  apiUpdateTicket,
  apiChangeStatus,
  apiAssignAgent,
  apiUnassignAgent,
  apiGetComments,
  apiAddComment,
  apiGetAttachments,
  apiGetAuditLog,
  type TicketResponse,
  type CommentResponse,
  type AuditLogEntryResponse,
  type AttachmentResponse,
} from "./api"
import { useAuth } from "./auth"
import type {
  Ticket,
  NotificationItem,
  Role,
  TicketStatus,
  TicketPriority,
  TicketCategory,
  Comment,
  ActivityEntry,
  Attachment,
} from "./types"

interface NewTicketInput {
  subject: string
  description: string
  category: TicketCategory
  priority: TicketPriority
}

interface StoreValue {
  currentUserId: string
  role: Role
  setRole: (role: Role) => void
  tickets: Ticket[]
  ticketsLoading: boolean
  refreshTickets: () => Promise<void>
  notifications: NotificationItem[]
  unreadCount: number
  createTicket: (input: NewTicketInput) => Promise<Ticket>
  updateTicket: (id: string, patch: Partial<Ticket>, activity?: string, detail?: string) => Promise<void>
  addComment: (ticketId: string, body: string, internal: boolean) => Promise<void>
  assignTicket: (ticketId: string, assigneeId: string | null) => Promise<void>
  markNotificationRead: (id: string) => void
  markAllNotificationsRead: () => void
  loadTicketDetail: (id: string) => Promise<Ticket | null>
  loadComments: (ticketId: string) => Promise<Comment[]>
  loadAuditLog: (ticketId: string) => Promise<AuditLogEntryResponse[]>
  loadAttachments: (ticketId: string) => Promise<AttachmentResponse[]>
}

const StoreContext = React.createContext<StoreValue | null>(null)

const CATEGORY_MAP: Record<string, TicketCategory> = {
  Hardware: "Hardware",
  Software: "Software",
  Network: "Network",
  Email: "Email",
  "Access Request": "Access Request",
  Other: "Other",
}

const PRIORITY_MAP: Record<string, TicketPriority> = {
  Low: "Low",
  Medium: "Medium",
  High: "High",
  Critical: "Critical",
}

const STATUS_MAP: Record<string, TicketStatus> = {
  Open: "Open",
  "In Progress": "In Progress",
  Pending: "Pending",
  Resolved: "Resolved",
  Closed: "Closed",
}

const SLA_HOURS: Record<TicketPriority, number> = {
  Critical: 2,
  High: 8,
  Medium: 24,
  Low: 48,
}

function mapTicket(res: TicketResponse): Ticket {
  return {
    id: res.id,
    reference: res.referenceNumber,
    subject: res.title,
    description: res.description,
    category: CATEGORY_MAP[res.categoryName] || "Other",
    priority: PRIORITY_MAP[res.priorityName] || "Medium",
    status: STATUS_MAP[res.statusName] || "Open",
    requesterId: res.createdByUserId,
    assigneeId: null,
    createdAt: res.createdAt,
    updatedAt: res.updatedAt,
    resolvedAt: null,
    slaHours: SLA_HOURS[PRIORITY_MAP[res.priorityName] || "Medium"],
    comments: [],
    activity: [],
    attachments: [],
  }
}

function buildActivity(auditLog: AuditLogEntryResponse[]): ActivityEntry[] {
  return auditLog.map((e) => ({
    id: e.id,
    actorId: e.changedByUserId,
    action: `${e.fieldChanged.toLowerCase()} changed`,
    detail: e.oldValue && e.newValue ? `${e.oldValue} → ${e.newValue}` : e.newValue || undefined,
    createdAt: e.changedAt,
  }))
}

const ROLE_DEFAULT_USER: Record<Role, string> = {
  admin: "u-1",
  agent: "u-2",
  employee: "u-5",
  manager: "u-7",
}

export function StoreProvider({ children }: { children: React.ReactNode }) {
  const { user: authUser } = useAuth()
  const [role, setRole] = React.useState<Role>("admin")
  const [tickets, setTickets] = React.useState<Ticket[]>([])
  const [ticketsLoading, setTicketsLoading] = React.useState(true)
  const [notifications] = React.useState<NotificationItem[]>([])

  const currentUserId = authUser?.id || ROLE_DEFAULT_USER[role]

  const fetchTickets = React.useCallback(async () => {
    setTicketsLoading(true)
    try {
      const isEmployee = role === "employee"
      const data = isEmployee
        ? await apiGetMyTickets(1, 500)
        : await apiGetTickets(1, 500)
      setTickets(data.tickets.map(mapTicket))
    } catch {
      // API may be unreachable — show empty state
      setTickets([])
    } finally {
      setTicketsLoading(false)
    }
  }, [role])

  React.useEffect(() => {
    if (authUser) {
      fetchTickets()
    } else {
      setTickets([])
      setTicketsLoading(false)
    }
  }, [authUser, fetchTickets])

  const createTicket = React.useCallback(
    async (input: NewTicketInput): Promise<Ticket> => {
      const PRIORITY_IDS: Record<TicketPriority, number> = {
        Low: 1,
        Medium: 2,
        High: 3,
        Critical: 4,
      }
      const CATEGORY_IDS: Record<TicketCategory, number> = {
        Hardware: 1,
        Software: 2,
        Network: 3,
        Email: 4,
        "Access Request": 5,
        Other: 6,
      }
      const created = await apiCreateTicket({
        title: input.subject,
        description: input.description,
        categoryId: CATEGORY_IDS[input.category],
        priorityId: PRIORITY_IDS[input.priority],
      })
      const ticket = mapTicket(created)
      setTickets((prev) => [ticket, ...prev])
      return ticket
    },
    []
  )

  const updateTicket = React.useCallback(
    async (id: string, patch: Partial<Ticket>) => {
      if (patch.status) {
        const STATUS_IDS: Record<TicketStatus, number> = {
          Open: 1, "In Progress": 2, Pending: 3, Resolved: 4, Closed: 5,
        }
        const updated = await apiChangeStatus(id, STATUS_IDS[patch.status])
        setTickets((prev) =>
          prev.map((t) => (t.id === id ? { ...t, ...mapTicket(updated) } : t))
        )
        return
      }
      const request: { title?: string; description?: string; categoryId?: number; priorityId?: number } = {}
      if (patch.subject) request.title = patch.subject
      if (patch.description) request.description = patch.description
      if (patch.category) {
        const CATEGORY_IDS: Record<TicketCategory, number> = {
          Hardware: 1, Software: 2, Network: 3, Email: 4, "Access Request": 5, Other: 6,
        }
        request.categoryId = CATEGORY_IDS[patch.category]
      }
      if (patch.priority) {
        const PRIORITY_IDS: Record<TicketPriority, number> = {
          Low: 1, Medium: 2, High: 3, Critical: 4,
        }
        request.priorityId = PRIORITY_IDS[patch.priority]
      }
      const updated = await apiUpdateTicket(id, request)
      setTickets((prev) =>
        prev.map((t) => (t.id === id ? { ...t, ...mapTicket(updated) } : t))
      )
    },
    []
  )

  const addComment = React.useCallback(
    async (ticketId: string, body: string, internal: boolean) => {
      await apiAddComment(ticketId, body, internal)
    },
    []
  )

  const assignTicket = React.useCallback(
    async (ticketId: string, assigneeId: string | null) => {
      if (assigneeId) {
        await apiAssignAgent(ticketId, assigneeId)
      }
      // Refetch ticket to get updated state
      try {
        const updated = await apiGetTickets(1, 500)
        setTickets(updated.tickets.map(mapTicket))
      } catch { /* ignore */ }
    },
    []
  )

  const loadTicketDetail = React.useCallback(
    async (id: string): Promise<Ticket | null> => {
      try {
        const res = await apiGetTicketById(id)
        const ticket = mapTicket(res)
        // Load assignments to get assigneeId
        try {
          const assignments = await apiGetAssignments(id)
          const active = assignments.find((a) => !a.unassignedAt)
          if (active) ticket.assigneeId = active.agentUserId
        } catch { /* ignore */ }
        // Load comments
        try {
          const comments = await apiGetComments(id, true)
          ticket.comments = comments.map((c) => ({
            id: c.id,
            authorId: c.authorUserId,
            body: c.content,
            createdAt: c.createdAt,
            internal: c.isInternal,
          }))
        } catch { /* ignore */ }
        // Load audit log
        try {
          const audit = await apiGetAuditLog(id, 1, 100)
          ticket.activity = buildActivity(audit.entries)
        } catch { /* ignore */ }
        // Load attachments
        try {
          const attachments = await apiGetAttachments(id)
          ticket.attachments = attachments.map((a) => ({
            id: a.id,
            name: a.fileName,
            size: "",
            type: a.fileName.split(".").pop() || "",
          }))
        } catch { /* ignore */ }
        return ticket
      } catch {
        return null
      }
    },
    []
  )

  const loadComments = React.useCallback(
    async (ticketId: string): Promise<Comment[]> => {
      const comments = await apiGetComments(ticketId, true)
      return comments.map((c) => ({
        id: c.id,
        authorId: c.authorUserId,
        body: c.content,
        createdAt: c.createdAt,
        internal: c.isInternal,
      }))
    },
    []
  )

  const loadAuditLog = React.useCallback(
    async (ticketId: string): Promise<AuditLogEntryResponse[]> => {
      const audit = await apiGetAuditLog(ticketId, 1, 100)
      return audit.entries
    },
    []
  )

  const loadAttachments = React.useCallback(
    async (ticketId: string): Promise<AttachmentResponse[]> => {
      return apiGetAttachments(ticketId)
    },
    []
  )

  const markNotificationRead = React.useCallback((_id: string) => {
    // Notifications are not backed by API yet — no-op
  }, [])

  const markAllNotificationsRead = React.useCallback(() => {
    // Notifications are not backed by API yet — no-op
  }, [])

  const unreadCount = 0

  const value: StoreValue = {
    currentUserId,
    role,
    setRole,
    tickets,
    ticketsLoading,
    refreshTickets: fetchTickets,
    notifications,
    unreadCount,
    createTicket,
    updateTicket,
    addComment,
    assignTicket,
    markNotificationRead,
    markAllNotificationsRead,
    loadTicketDetail,
    loadComments,
    loadAuditLog,
    loadAttachments,
  }

  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>
}

export function useStore() {
  const ctx = React.useContext(StoreContext)
  if (!ctx) throw new Error("useStore must be used within StoreProvider")
  return ctx
}

// Shared UI helpers for status / priority styling.
export function statusBadgeClass(status: TicketStatus): string {
  switch (status) {
    case "Open":
      return "bg-info/10 text-info border-info/25"
    case "In Progress":
      return "bg-primary/10 text-primary border-primary/25"
    case "Pending":
      return "bg-warning/15 text-warning-foreground border-warning/30"
    case "Resolved":
      return "bg-success/12 text-success border-success/30"
    case "Closed":
      return "bg-muted text-muted-foreground border-border"
  }
}

export function priorityMeta(priority: TicketPriority): {
  dot: string
  label: string
} {
  switch (priority) {
    case "Critical":
      return { dot: "bg-destructive", label: "Critical" }
    case "High":
      return { dot: "bg-warning", label: "High" }
    case "Medium":
      return { dot: "bg-info", label: "Medium" }
    case "Low":
      return { dot: "bg-muted-foreground/50", label: "Low" }
  }
}
