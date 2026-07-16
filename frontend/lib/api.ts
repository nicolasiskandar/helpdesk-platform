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
  const data = await res.json()
  if (!res.ok) throw data as ApiError
  return data as AuthResponse
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
  const data = await res.json()
  if (!res.ok) throw data as ApiError
  return data as AuthResponse
}

export async function apiRefresh(
  refreshToken: string
): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  })
  const data = await res.json()
  if (!res.ok) throw data as ApiError
  return data as AuthResponse
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
  const data = await res.json()
  if (!res.ok) throw data as ApiError
  return data as UserResponse
}
