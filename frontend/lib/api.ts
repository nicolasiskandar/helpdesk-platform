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
  isInternal: boolean
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
    throw parsed || { message: `Request failed with status ${res.status}` }
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

// ---------- Ticket API ----------

function getAccessToken(): string {
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
  ticketId: string,
  includeInternal = true
): Promise<CommentResponse[]> {
  const token = getAccessToken()
  const res = await fetch(
    `${API_BASE}/api/tickets/${ticketId}/comments?includeInternal=${includeInternal}`,
    { headers: authHeaders(token) }
  )
  return handleResponse<CommentResponse[]>(res)
}

export async function apiAddComment(
  ticketId: string,
  content: string,
  isInternal: boolean
): Promise<CommentResponse> {
  const token = getAccessToken()
  const res = await fetch(`${API_BASE}/api/tickets/${ticketId}/comments`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token) },
    body: JSON.stringify({ content, isInternal }),
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
