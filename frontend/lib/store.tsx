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
  apiDeleteTicket,
  apiGetComments,
  apiAddComment,
  apiGetAttachments,
  apiGetAuditLog,
  apiGetOpenUnassignedTickets,
  apiClaimTicket,
  apiGetUsers,
  apiGetNotifications,
  apiGetUnreadCount,
  apiMarkNotificationRead as apiMarkNotifRead,
  apiMarkAllNotificationsRead as apiMarkAllNotifsRead,
  type TicketResponse,
  type CommentResponse,
  type AuditLogEntryResponse,
  type AttachmentResponse,
  type NotificationResponse,
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
import { formatFileSize } from "./analytics"

interface NewTicketInput {
  subject: string
  description: string
  category: TicketCategory
  priority: TicketPriority
}

interface StoreValue {
  currentUserId: string
  role: Role
  userMap: Record<string, string>
  tickets: Ticket[]
  ticketsLoading: boolean
  refreshTickets: () => Promise<void>
  notifications: NotificationItem[]
  unreadCount: number
  createTicket: (input: NewTicketInput) => Promise<Ticket>
  updateTicket: (id: string, patch: Partial<Ticket>, activity?: string, detail?: string) => Promise<void>
  addComment: (ticketId: string, body: string, parentCommentId?: string, recipientUserIds?: string[]) => Promise<void>
  assignTicket: (ticketId: string, assigneeId: string | null) => Promise<void>
  claimTicket: (ticketId: string) => Promise<void>
  deleteTicket: (id: string) => Promise<void>
  markNotificationRead: (id: string) => void
  markAllNotificationsRead: () => void
  refreshNotifications: () => Promise<void>
  loadTicketDetail: (id: string) => Promise<Ticket | null>
  loadComments: (ticketId: string) => Promise<Comment[]>
  loadAuditLog: (ticketId: string) => Promise<AuditLogEntryResponse[]>
  loadAttachments: (ticketId: string) => Promise<AttachmentResponse[]>
  openUnassignedTickets: Ticket[]
  openUnassignedTicketsLoading: boolean
  fetchOpenUnassignedTickets: () => Promise<void>
}

const StoreContext = React.createContext<StoreValue | null>(null)

const CATEGORY_MAP: Record<string, TicketCategory> = {
  Hardware: "Hardware",
  Software: "Software",
  Network: "Network",
  Access: "Access",
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
  "Resolved - Pending Confirmation": "Pending Resolution",
  Closed: "Closed",
  "Resolved by AI": "Closed",
}

const SLA_HOURS: Record<TicketPriority, number> = {
  Critical: 2,
  High: 8,
  Medium: 24,
  Low: 48,
}

function mapTicket(res: TicketResponse, userMap: Record<string, string> = {}): Ticket {
  return {
    id: res.id,
    reference: res.referenceNumber,
    subject: res.title,
    description: res.description,
    category: CATEGORY_MAP[res.categoryName] || "Other",
    priority: PRIORITY_MAP[res.priorityName] || "Medium",
    status: STATUS_MAP[res.statusName] || "Open",
    requesterId: res.createdByUserId,
    assigneeId: res.assigneeUserId ?? null,
    assigneeIds: res.assigneeUserId ? [res.assigneeUserId] : [],
    assigneeName: res.assigneeUserId ? (userMap[res.assigneeUserId] || undefined) : undefined,
    createdAt: res.createdAt,
    updatedAt: res.updatedAt,
    resolvedAt: null,
    slaHours: SLA_HOURS[PRIORITY_MAP[res.priorityName] || "Medium"],
    timeWorkedMinutes: res.timeWorkedMinutes ?? null,
    timeToCloseMinutes: res.timeToCloseMinutes ?? null,
    comments: [],
    activity: [],
    attachments: [],
  }
}

const NOTIF_TYPE_MAP: Record<string, NotificationItem["type"]> = {
  created: "comment",
  assigned: "assignment",
  unassigned: "assignment",
  status_changed: "status",
  comment: "comment",
}

function mapNotification(res: NotificationResponse): NotificationItem {
  return {
    id: res.id,
    type: NOTIF_TYPE_MAP[res.type] || "status",
    title: res.title,
    body: res.message,
    ticketId: res.ticketId ?? undefined,
    commentId: res.commentId ?? undefined,
    ticketRef: res.ticketReferenceNumber ?? undefined,
    createdAt: res.createdAt,
    read: res.isRead,
  }
}

function normalizeRole(role: string): Role {
  const normalized = role.toLowerCase()
  if (normalized === "admin") return "admin"
  if (normalized === "manager") return "manager"
  if (normalized === "it support agent" || normalized === "agent") return "agent"
  return "employee"
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

export function StoreProvider({ children }: { children: React.ReactNode }) {
  const { user: authUser } = useAuth()
  const [tickets, setTickets] = React.useState<Ticket[]>([])
  const [ticketsLoading, setTicketsLoading] = React.useState(true)
  const [notifications, setNotifications] = React.useState<NotificationItem[]>([])
  const [unreadCount, setUnreadCount] = React.useState(0)
  const [openUnassignedTickets, setOpenUnassignedTickets] = React.useState<Ticket[]>([])
  const [openUnassignedTicketsLoading, setOpenUnassignedTicketsLoading] = React.useState(false)
  const [userMap, setUserMap] = React.useState<Record<string, string>>({})

  const role: Role = authUser ? normalizeRole(authUser.role) : "employee"

  const currentUserId = authUser?.id || ""

  const fetchTickets = React.useCallback(async () => {
    setTicketsLoading(true)
    try {
      const isEmployee = role === "employee"
      const data = isEmployee
        ? await apiGetMyTickets(1, 500)
        : await apiGetTickets(1, 500)
      setTickets(data.tickets.map((t) => mapTicket(t, userMap)))
    } catch {
      setTickets([])
    } finally {
      setTicketsLoading(false)
    }
  }, [role, userMap])

  React.useEffect(() => {
    if (authUser) {
      fetchTickets()
    } else {
      setTickets([])
      setTicketsLoading(false)
    }
  }, [authUser, fetchTickets])

  React.useEffect(() => {
    if (authUser) {
      apiGetUsers(undefined, undefined, true, 1, 500)
        .then((res) => {
          const map: Record<string, string> = {}
          for (const u of res.users) map[u.id] = u.fullName
          setUserMap(map)
        })
        .catch(() => {})
    } else {
      setUserMap({})
    }
  }, [authUser])

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
        Access: 4,
        Other: 5,
      }
      const created = await apiCreateTicket({
        title: input.subject,
        description: input.description,
        categoryId: CATEGORY_IDS[input.category],
        priorityId: PRIORITY_IDS[input.priority],
      })
      const ticket = mapTicket(created, userMap)
      setTickets((prev) => [ticket, ...prev])
      return ticket
    },
    [userMap]
  )

  const updateTicket = React.useCallback(
    async (id: string, patch: Partial<Ticket>) => {
      if (patch.status) {
        const STATUS_IDS: Record<TicketStatus, number> = {
          Open: 1, "In Progress": 2, "Pending Resolution": 3, Closed: 4,
        }
        const updated = await apiChangeStatus(id, STATUS_IDS[patch.status])
        setTickets((prev) =>
          prev.map((t) => (t.id === id ? { ...t, ...mapTicket(updated, userMap) } : t))
        )
        return
      }
      const request: { title?: string; description?: string; categoryId?: number; priorityId?: number } = {}
      if (patch.subject) request.title = patch.subject
      if (patch.description) request.description = patch.description
      if (patch.category) {
        const CATEGORY_IDS: Record<TicketCategory, number> = {
          Hardware: 1, Software: 2, Network: 3, Access: 4, Other: 5,
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
        prev.map((t) => (t.id === id ? { ...t, ...mapTicket(updated, userMap) } : t))
      )
    },
    [userMap]
  )

  const addComment = React.useCallback(
    async (ticketId: string, body: string, parentCommentId?: string, recipientUserIds?: string[]) => {
      await apiAddComment(ticketId, body, parentCommentId, recipientUserIds)
    },
    []
  )

  const assignTicket = React.useCallback(
    async (ticketId: string, assigneeId: string | null) => {
      if (assigneeId) {
        // Optimistic update: immediately assign and transition to In Progress
        const prevTickets = tickets
        setTickets((prev) =>
          prev.map((t) => {
            if (t.id !== ticketId) return t
            return {
              ...t,
              assigneeId,
              assigneeIds: [assigneeId],
              assigneeName: userMap[assigneeId] || undefined,
              status: t.status === "Open" ? "In Progress" : t.status,
            }
          })
        )
        try {
          await apiAssignAgent(ticketId, assigneeId)
        } catch {
          setTickets(prevTickets)
          throw new Error("Failed to assign ticket")
        }
      } else {
        // Unassign: optimistic update — clear assignee and revert to Open
        const prevTickets = tickets
        setTickets((prev) =>
          prev.map((t) => {
            if (t.id !== ticketId) return t
            return {
              ...t,
              assigneeId: null,
              assigneeIds: [],
              assigneeName: undefined,
              status: t.status === "In Progress" ? "Open" : t.status,
            }
          })
        )
        try {
          const current = tickets.find((t) => t.id === ticketId)
          if (current?.assigneeId) {
            await apiUnassignAgent(ticketId, current.assigneeId)
          }
        } catch {
          setTickets(prevTickets)
          throw new Error("Failed to unassign ticket")
        }
      }
    },
    [tickets, role, userMap]
  )

  const deleteTicket = React.useCallback(
    async (id: string) => {
      await apiDeleteTicket(id)
      setTickets((prev) => prev.filter((t) => t.id !== id))
    },
    []
  )

  const claimTicket = React.useCallback(
    async (ticketId: string) => {
      // Optimistic update: remove from unassigned list and assign to current user
      const prevTickets = tickets
      const prevUnassigned = openUnassignedTickets
      setOpenUnassignedTickets((prev) => prev.filter((t) => t.id !== ticketId))
      setTickets((prev) =>
        prev.map((t) => {
          if (t.id !== ticketId) return t
          return {
            ...t,
            assigneeId: currentUserId,
            assigneeIds: [currentUserId],
            assigneeName: userMap[currentUserId] || undefined,
            status: t.status === "Open" ? "In Progress" : t.status,
          }
        })
      )
      try {
        await apiClaimTicket(ticketId)
      } catch {
        setTickets(prevTickets)
        setOpenUnassignedTickets(prevUnassigned)
        throw new Error("Failed to claim ticket")
      }
    },
    [tickets, openUnassignedTickets, currentUserId, userMap]
  )

  const fetchOpenUnassignedTickets = React.useCallback(async () => {
    setOpenUnassignedTicketsLoading(true)
    try {
      const data = await apiGetOpenUnassignedTickets(1, 500)
      setOpenUnassignedTickets(data.tickets.map((t) => mapTicket(t, userMap)))
    } catch {
      setOpenUnassignedTickets([])
    } finally {
      setOpenUnassignedTicketsLoading(false)
    }
  }, [userMap])

  const loadTicketDetail = React.useCallback(
    async (id: string): Promise<Ticket | null> => {
      try {
        const res = await apiGetTicketById(id)
        const ticket = mapTicket(res, userMap)
        // Load assignments to get assigneeId
        try {
          const assignments = await apiGetAssignments(id)
          const active = assignments.filter((a) => !a.unassignedAt)
          ticket.assigneeIds = active.map((a) => a.agentUserId)
          ticket.assigneeId = ticket.assigneeIds[0] ?? null
        } catch { /* ignore */ }
        // Load comments
        try {
          const comments = await apiGetComments(id)
          ticket.comments = comments.map((c) => ({
            id: c.id,
            authorId: c.authorUserId,
            body: c.content,
            createdAt: c.createdAt,
            isPrivate: c.isPrivate,
            parentId: c.parentCommentId ?? undefined,
            recipientIds: c.recipientUserIds ?? [],
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
            size: a.size > 0 ? formatFileSize(a.size) : "",
            type: a.fileName.split(".").pop() || "",
          }))
        } catch { /* ignore */ }
        return ticket
      } catch (err: any) {
        if (err?.status === 403) throw err
        return null
      }
    },
    [userMap]
  )

  const loadComments = React.useCallback(
    async (ticketId: string): Promise<Comment[]> => {
      const comments = await apiGetComments(ticketId)
      return comments.map((c) => ({
        id: c.id,
        authorId: c.authorUserId,
        body: c.content,
        createdAt: c.createdAt,
        isPrivate: c.isPrivate,
        parentId: c.parentCommentId ?? undefined,
        recipientIds: c.recipientUserIds ?? [],
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

  const markNotificationRead = React.useCallback(
    async (id: string) => {
      setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, read: true } : n)))
      setUnreadCount((prev) => Math.max(0, prev - 1))
      try {
        await apiMarkNotifRead(id)
      } catch { /* revert on failure — re-fetch next cycle */ }
    },
    []
  )

  const markAllNotificationsRead = React.useCallback(async () => {
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })))
    setUnreadCount(0)
    try {
      await apiMarkAllNotifsRead()
    } catch { /* revert on failure */ }
  }, [])

  const refreshNotifications = React.useCallback(async () => {
    try {
      const [notifData, count] = await Promise.all([
        apiGetNotifications(1, 50),
        apiGetUnreadCount(),
      ])
      setNotifications(notifData.notifications.map(mapNotification))
      setUnreadCount(count)
    } catch { /* ignore */ }
  }, [])

  React.useEffect(() => {
    if (authUser) {
      refreshNotifications()
    } else {
      setNotifications([])
      setUnreadCount(0)
    }
  }, [authUser, refreshNotifications])

  const value: StoreValue = {
    currentUserId,
    role,
    userMap,
    tickets,
    ticketsLoading,
    refreshTickets: fetchTickets,
    notifications,
    unreadCount,
    createTicket,
    updateTicket,
    addComment,
    assignTicket,
    claimTicket,
    deleteTicket,
    markNotificationRead,
    markAllNotificationsRead,
    refreshNotifications,
    loadTicketDetail,
    loadComments,
    loadAuditLog,
    loadAttachments,
    openUnassignedTickets,
    openUnassignedTicketsLoading,
    fetchOpenUnassignedTickets,
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
    case "Pending Resolution":
      return "bg-warning/15 text-warning-foreground border-warning/30"
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
