"use client"

import * as React from "react"
import {
  tickets as seedTickets,
  notifications as seedNotifications,
  users,
} from "./data"
import type {
  Ticket,
  NotificationItem,
  Role,
  TicketStatus,
  TicketPriority,
  TicketCategory,
  Comment,
  ActivityEntry,
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
  notifications: NotificationItem[]
  unreadCount: number
  createTicket: (input: NewTicketInput) => Ticket
  updateTicket: (id: string, patch: Partial<Ticket>, activity?: string, detail?: string) => void
  addComment: (ticketId: string, body: string, internal: boolean) => void
  assignTicket: (ticketId: string, assigneeId: string | null) => void
  markNotificationRead: (id: string) => void
  markAllNotificationsRead: () => void
}

const StoreContext = React.createContext<StoreValue | null>(null)

// Default acting user per role, so the demo reflects the selected role.
const ROLE_DEFAULT_USER: Record<Role, string> = {
  admin: "u-1",
  agent: "u-2",
  employee: "u-5",
  manager: "u-7",
}

let refCounter = 4822

export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [role, setRole] = React.useState<Role>("admin")
  const [tickets, setTickets] = React.useState<Ticket[]>(seedTickets)
  const [notifications, setNotifications] =
    React.useState<NotificationItem[]>(seedNotifications)

  const currentUserId = ROLE_DEFAULT_USER[role]

  const createTicket = React.useCallback(
    (input: NewTicketInput) => {
      const now = new Date().toISOString()
      const slaMap: Record<TicketPriority, number> = {
        Critical: 2,
        High: 8,
        Medium: 24,
        Low: 48,
      }
      const ticket: Ticket = {
        id: `t-${Date.now()}`,
        reference: `HLX-${refCounter++}`,
        subject: input.subject,
        description: input.description,
        category: input.category,
        priority: input.priority,
        status: "Open",
        requesterId: currentUserId,
        assigneeId: null,
        createdAt: now,
        updatedAt: now,
        resolvedAt: null,
        slaHours: slaMap[input.priority],
        comments: [],
        activity: [
          {
            id: `a-${Date.now()}`,
            actorId: currentUserId,
            action: "created the ticket",
            createdAt: now,
          },
        ],
        attachments: [],
      }
      setTickets((prev) => [ticket, ...prev])
      return ticket
    },
    [currentUserId]
  )

  const updateTicket = React.useCallback(
    (id: string, patch: Partial<Ticket>, activity?: string, detail?: string) => {
      setTickets((prev) =>
        prev.map((t) => {
          if (t.id !== id) return t
          const now = new Date().toISOString()
          const newActivity: ActivityEntry[] = activity
            ? [
                ...t.activity,
                {
                  id: `a-${Date.now()}`,
                  actorId: currentUserId,
                  action: activity,
                  detail,
                  createdAt: now,
                },
              ]
            : t.activity
          return { ...t, ...patch, updatedAt: now, activity: newActivity }
        })
      )
    },
    [currentUserId]
  )

  const addComment = React.useCallback(
    (ticketId: string, body: string, internal: boolean) => {
      const now = new Date().toISOString()
      const comment: Comment = {
        id: `c-${Date.now()}`,
        authorId: currentUserId,
        body,
        createdAt: now,
        internal,
      }
      setTickets((prev) =>
        prev.map((t) =>
          t.id === ticketId
            ? { ...t, comments: [...t.comments, comment], updatedAt: now }
            : t
        )
      )
    },
    [currentUserId]
  )

  const assignTicket = React.useCallback(
    (ticketId: string, assigneeId: string | null) => {
      const assignee = users.find((u) => u.id === assigneeId)
      updateTicket(
        ticketId,
        { assigneeId, status: assigneeId ? "In Progress" : "Open" },
        assigneeId ? `assigned to ${assignee?.name}` : "unassigned the ticket"
      )
    },
    [updateTicket]
  )

  const markNotificationRead = React.useCallback((id: string) => {
    setNotifications((prev) =>
      prev.map((n) => (n.id === id ? { ...n, read: true } : n))
    )
  }, [])

  const markAllNotificationsRead = React.useCallback(() => {
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })))
  }, [])

  const unreadCount = notifications.filter((n) => !n.read).length

  const value: StoreValue = {
    currentUserId,
    role,
    setRole,
    tickets,
    notifications,
    unreadCount,
    createTicket,
    updateTicket,
    addComment,
    assignTicket,
    markNotificationRead,
    markAllNotificationsRead,
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
