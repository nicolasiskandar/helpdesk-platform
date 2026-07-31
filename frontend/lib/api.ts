const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface UserResponse {
  id: string
  email: string
  fullName: string
  role: string
  isActive: boolean
  createdAt: string
  lastLoginAt: string | null
}

export interface ApiError {
  message: string
  errors?: Record<string, string[]>
}

// ---------- Ticket API types (backend shape) ----------

export interface TicketResponse {
  id: string
  referenceNumber: string
  title: string
  description: string
  categoryName: string
  priorityName: string
  statusName: string
  createdByUserId: string
  createdAt: string
  updatedAt: string
  assigneeUserId?: string
}

export interface TicketListResponse {
  tickets: TicketResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AssignmentResponse {
  id: string
  agentUserId: string
  assignedByUserId: string
  assignedAt: string
  unassignedAt: string | null
}

export interface CommentResponse {
  id: string
  authorUserId: string
  content: string
  isPrivate: boolean
  parentCommentId: string | null
  recipientUserIds: string[]
  createdAt: string
}

export interface AuditLogEntryResponse {
  id: string
  changedByUserId: string
  changedByType: string
  fieldChanged: string
  oldValue: string | null
  newValue: string | null
  changedAt: string
}

export interface AuditLogListResponse {
  entries: AuditLogEntryResponse[]
  totalCount: number
}

export interface AttachmentResponse {
  id: string
  fileName: string
  fileUrl: string
  uploadedByUserId: string
  uploadedAt: string
}

export interface CategoryResponse {
  id: number
  name: string
}

export interface PriorityResponse {
  id: number
  name: string
  level: number
}

export interface StatusResponse {
  id: number
  name: string
}

// ---------- Auth API ----------

function authHeaders(accessToken: string) {
  return { Authorization: `Bearer ${accessToken}` }
}

async function handleResponse<T>(res: Response): Promise<T> {
  const text = await res.text()
  if (!res.ok) {
    let parsed: any
    try { parsed = JSON.parse(text) } catch { /* empty/error body */ }
    throw Object.assign(parsed || { message: `Request failed with status ${res.status}` }, { status: res.status })
  }
  if (!text) return undefined as T
  return JSON.parse(text) as T
}

export async function apiRegister(
  email: string,
  password: string,
  fullName: string
): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, fullName }),
  })
  return handleResponse<AuthResponse>(res)
}

export async function apiLogin(
  email: string,
  password: string
): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  })
  return handleResponse<AuthResponse>(res)
}

export async function apiRefresh(
  refreshToken: string
): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  })
  return handleResponse<AuthResponse>(res)
}

export async function apiLogout(refreshToken: string): Promise<void> {
  await fetch(`${API_BASE}/api/auth/logout`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  })
}

export async function apiGetMe(
  accessToken: string
): Promise<UserResponse> {
  const res = await fetch(`${API_BASE}/api/auth/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  return handleResponse<UserResponse>(res)
}

export async function apiUpdateProfile(request: {
  fullName: string
  email: string
}): Promise<UserResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/auth/me`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<UserResponse>(res)
}

export async function apiChangePassword(request: {
  currentPassword: string
  newPassword: string
}): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/auth/change-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<void>(res)
}

export async function apiForgotPassword(email: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/auth/forgot-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email }),
  })
  return handleResponse<void>(res)
}

export async function apiResetPassword(
  token: string,
  newPassword: string
): Promise<void> {
  const res = await fetch(`${API_BASE}/api/auth/reset-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ token, newPassword }),
  })
  return handleResponse<void>(res)
}

// ---------- Ticket API ----------

export function getAccessToken(): string {
  if (typeof window === "undefined") return ""
  return sessionStorage.getItem("accessToken") || ""
}

export async function apiGetTickets(
  page = 1,
  pageSize = 50,
  createdFrom?: string,
  createdTo?: string
): Promise<TicketListResponse> {
  const token = getAccessToken()
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (createdFrom) params.set("createdFrom", createdFrom)
  if (createdTo) params.set("createdTo", createdTo)
  const res = await fetch(
    `${API_BASE}/api/tickets?${params}`,
    { headers: authHeaders(token) }
  )
  return handleResponse<TicketListResponse>(res)
}

export async function apiGetMyTickets(
  page = 1,
  pageSize = 50,
  createdFrom?: string,
  createdTo?: string
): Promise<TicketListResponse> {
  const token = getAccessToken()
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (createdFrom) params.set("createdFrom", createdFrom)
  if (createdTo) params.set("createdTo", createdTo)
  const res = await fetch(
    `${API_BASE}/api/tickets/my?${params}`,
    { headers: authHeaders(token) }
  )
  return handleResponse<TicketListResponse>(res)
}

export async function apiGetTicketById(
  id: string
): Promise<TicketResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${id}`, {
    headers: authHeaders(token),
  })
  return handleResponse<TicketResponse>(res)
}

export async function apiGetTicketByReference(
  referenceNumber: string
): Promise<TicketResponse> {
  const token = getAccessToken()
  const res = await fetch(
    `${API_BASE}/api/tickets/ref/${encodeURIComponent(referenceNumber)}`,
    { headers: authHeaders(token) }
  )
  return handleResponse<TicketResponse>(res)
}

export async function apiCreateTicket(request: {
  title: string
  description: string
  categoryId: number
  priorityId: number
}): Promise<TicketResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<TicketResponse>(res)
}

export async function apiUpdateTicket(
  id: string,
  request: {
    title?: string
    description?: string
    categoryId?: number
    priorityId?: number
  }
): Promise<TicketResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<TicketResponse>(res)
}

export async function apiDeleteTicket(id: string): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${id}`, {
    method: "DELETE",
    headers: authHeaders(token),
  })
  return handleResponse<void>(res)
}

export async function apiGetOpenUnassignedTickets(
  page = 1,
  pageSize = 20
): Promise<TicketListResponse> {
  const token = getAccessToken()
  const res = await fetch(
    `${API_BASE}/api/tickets/open-unassigned?page=${page}&pageSize=${pageSize}`,
    { headers: authHeaders(token) }
  )
  return handleResponse<TicketListResponse>(res)
}

export async function apiClaimTicket(
  ticketId: string
): Promise<AssignmentResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/claim`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
  })
  return handleResponse<AssignmentResponse>(res)
}

export async function apiEscalateTicket(
  ticketId: string,
  reason?: string
): Promise<TicketResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/escalate`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ reason }),
  })
  return handleResponse<TicketResponse>(res)
}

export async function apiChangeStatus(
  id: string,
  statusId: number,
  comment?: string
): Promise<TicketResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${id}/status`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ statusId, comment }),
  })
  return handleResponse<TicketResponse>(res)
}

export async function apiGetAssignments(
  ticketId: string
): Promise<AssignmentResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/assignments`, {
    headers: authHeaders(token),
  })
  return handleResponse<AssignmentResponse[]>(res)
}

export async function apiAssignAgent(
  ticketId: string,
  agentUserId: string
): Promise<AssignmentResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/assignments`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ agentUserId }),
  })
  return handleResponse<AssignmentResponse>(res)
}

export async function apiUnassignAgent(
  ticketId: string,
  agentUserId: string
): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(
    `${API_BASE}/api/tickets/${ticketId}/assignments/${agentUserId}`,
    { method: "DELETE", headers: authHeaders(token) }
  )
  return handleResponse<void>(res)
}

export async function apiGetComments(
  ticketId: string
): Promise<CommentResponse[]> {
  const token = getAccessToken()
  const res = await fetch(
    `${API_BASE}/api/tickets/${ticketId}/comments`,
    { headers: authHeaders(token) }
  )
  return handleResponse<CommentResponse[]>(res)
}

export async function apiAddComment(
  ticketId: string,
  content: string,
  parentCommentId?: string,
  recipientUserIds?: string[]
): Promise<CommentResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/comments`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ content, parentCommentId, recipientUserIds }),
  })
  return handleResponse<CommentResponse>(res)
}

export async function apiGetAttachments(
  ticketId: string
): Promise<AttachmentResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/attachments`, {
    headers: authHeaders(token),
  })
  return handleResponse<AttachmentResponse[]>(res)
}

export function apiAttachmentDownloadUrl(ticketId: string, attachmentId: string): string {
  const token = getAccessToken()
  return `${API_BASE}/api/tickets/${ticketId}/attachments/${attachmentId}`
}

export async function apiUploadAttachment(
  ticketId: string,
  file: File
): Promise<AttachmentResponse> {
  const token = getAccessToken()
  const formData = new FormData()
  formData.append("file", file)
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/attachments`, {
    method: "POST",
    headers: authHeaders(token),
    body: formData,
  })
  return handleResponse<AttachmentResponse>(res)
}

export async function apiGetAuditLog(
  ticketId: string,
  page = 1,
  pageSize = 50
): Promise<AuditLogListResponse> {
  const token = getAccessToken()
  const res = await fetch(
    `${API_BASE}/api/tickets/${ticketId}/audit?page=${page}&pageSize=${pageSize}`,
    { headers: authHeaders(token) }
  )
  return handleResponse<AuditLogListResponse>(res)
}

export async function apiGetCategories(): Promise<CategoryResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/categories`, {
    headers: authHeaders(token),
  })
  return handleResponse<CategoryResponse[]>(res)
}

export async function apiGetPriorities(): Promise<PriorityResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/priorities`, {
    headers: authHeaders(token),
  })
  return handleResponse<PriorityResponse[]>(res)
}

export async function apiGetStatuses(): Promise<StatusResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/statuses`, {
    headers: authHeaders(token),
  })
  return handleResponse<StatusResponse[]>(res)
}

// ---------- User Management API ----------

export interface UserListResponse {
  users: UserResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export async function apiGetUsers(
  search?: string,
  roleId?: number,
  isActive?: boolean,
  page = 1,
  pageSize = 20
): Promise<UserListResponse> {
  const token = getAccessToken()
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set("search", search)
  if (roleId !== undefined) params.set("roleId", String(roleId))
  if (isActive !== undefined) params.set("isActive", String(isActive))
  const res = await fetch(`${API_BASE}/api/users?${params}`, {
    headers: authHeaders(token),
  })
  return handleResponse<UserListResponse>(res)
}

export async function apiGetUserById(id: string): Promise<UserResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/users/${id}`, {
    headers: authHeaders(token),
  })
  return handleResponse<UserResponse>(res)
}

export async function apiCreateUser(request: {
  email: string
  password: string
  fullName: string
  roleId: number
}): Promise<UserResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/users`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<UserResponse>(res)
}

export async function apiUpdateUser(
  id: string,
  request: {
    fullName?: string
    email?: string
    roleId?: number
    isActive?: boolean
  }
): Promise<UserResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/users/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<UserResponse>(res)
}

export async function apiDeactivateUser(id: string): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/users/${id}/deactivate`, {
    method: "PATCH",
    headers: authHeaders(token),
  })
  return handleResponse<void>(res)
}

export async function apiActivateUser(id: string): Promise<UserResponse> {
  return apiUpdateUser(id, { isActive: true })
}

export async function apiDeleteUser(id: string): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/users/${id}`, {
    method: "DELETE",
    headers: authHeaders(token),
  })
  return handleResponse<void>(res)
}

// ---------- Agent Workload API ----------

export interface AgentWorkloadResponse {
  agentUserId: string
  openCount: number
  resolvedCount: number
  openTickets: AgentWorkloadTicketResponse[]
  resolvedTickets: AgentWorkloadTicketResponse[]
}

export interface AgentWorkloadTicketResponse {
  ticketId: string
  referenceNumber: string
  title: string
  categoryName: string
  priorityName: string
  statusName: string
  createdAt: string
  updatedAt: string
}

export async function apiGetAgentWorkload(): Promise<AgentWorkloadResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/agent-workload`, {
    headers: authHeaders(token),
  })
  return handleResponse<AgentWorkloadResponse[]>(res)
}

// ---------- Notification API ----------

export interface NotificationResponse {
  id: string
  type: string
  title: string
  message: string
  ticketId: string | null
  ticketReferenceNumber: string | null
  commentId: string | null
  isRead: boolean
  createdAt: string
}

export interface NotificationListResponse {
  notifications: NotificationResponse[]
  unreadCount: number
  page: number
  pageSize: number
}

export interface PreferenceResponse {
  ticketCreatedInApp: boolean
  ticketCreatedEmail: boolean
  ticketAssignedInApp: boolean
  ticketAssignedEmail: boolean
  ticketUnassignedInApp: boolean
  ticketUnassignedEmail: boolean
  ticketStatusChangedInApp: boolean
  ticketStatusChangedEmail: boolean
  ticketCommentedInApp: boolean
  ticketCommentedEmail: boolean
}

export async function apiGetNotifications(
  page = 1,
  pageSize = 20,
  unreadOnly?: boolean
): Promise<NotificationListResponse> {
  const token = getAccessToken()
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (unreadOnly !== undefined) params.set("unreadOnly", String(unreadOnly))
  const res = await fetch(`${API_BASE}/api/notifications?${params}`, {
    headers: authHeaders(token),
  })
  return handleResponse<NotificationListResponse>(res)
}

export async function apiGetUnreadCount(): Promise<number> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/notifications/unread-count`, {
    headers: authHeaders(token),
  })
  return handleResponse<number>(res)
}

export async function apiMarkNotificationRead(id: string): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/notifications/${id}/read`, {
    method: "PATCH",
    headers: authHeaders(token),
  })
  return handleResponse<void>(res)
}

export async function apiMarkAllNotificationsRead(): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/notifications/read-all`, {
    method: "PATCH",
    headers: authHeaders(token),
  })
  return handleResponse<void>(res)
}

export async function apiGetNotificationPreferences(): Promise<PreferenceResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/notifications/preferences`, {
    headers: authHeaders(token),
  })
  return handleResponse<PreferenceResponse>(res)
}

export async function apiUpdateNotificationPreferences(request: Partial<PreferenceResponse>): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/notifications/preferences`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify(request),
  })
  return handleResponse<void>(res)
}

// ---------- Settings API ----------

export interface SettingResponse {
  key: string
  value: string
  description: string | null
}

export async function apiGetSettings(): Promise<SettingResponse[]> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/settings`, {
    headers: authHeaders(token),
  })
  return handleResponse<SettingResponse[]>(res)
}

export async function apiUpdateSettings(settings: { key: string; value: string }[]): Promise<void> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/settings`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ settings }),
  })
  return handleResponse<void>(res)
}
