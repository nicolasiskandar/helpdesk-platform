export type Role = "admin" | "agent" | "employee" | "manager"

export type TicketCategory =
  | "Hardware"
  | "Software"
  | "Network"
  | "Email"
  | "Access Request"
  | "Other"

export type TicketPriority = "Low" | "Medium" | "High" | "Critical"

export type TicketStatus =
  | "Open"
  | "In Progress"
  | "Pending"
  | "Resolved"
  | "Closed"

export interface User {
  id: string
  name: string
  email: string
  role: Role
  department: string
  avatar?: string
  title: string
  status: "active" | "inactive"
  joinedAt: string
}

export interface Comment {
  id: string
  authorId: string
  body: string
  createdAt: string
  internal: boolean
}

export interface ActivityEntry {
  id: string
  actorId: string
  action: string
  detail?: string
  createdAt: string
}

export interface Attachment {
  id: string
  name: string
  size: string
  type: string
}

export interface Ticket {
  id: string
  reference: string
  subject: string
  description: string
  category: TicketCategory
  priority: TicketPriority
  status: TicketStatus
  requesterId: string
  assigneeId: string | null
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
  comments: Comment[]
  activity: ActivityEntry[]
  attachments: Attachment[]
  slaHours: number
}

export interface NotificationItem {
  id: string
  type: "assignment" | "comment" | "status" | "mention" | "sla"
  title: string
  body: string
  ticketRef?: string
  createdAt: string
  read: boolean
}

export interface KbArticle {
  id: string
  title: string
  category: TicketCategory
  excerpt: string
  body: string
  views: number
  updatedAt: string
  status: "published" | "draft"
  author: string
}
