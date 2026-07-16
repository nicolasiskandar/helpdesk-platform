"use client"

import * as React from "react"
import {
  apiLogin,
  apiRegister,
  apiRefresh,
  apiLogout,
  apiGetMe,
  type AuthResponse,
  type UserResponse,
} from "./api"

interface AuthValue {
  user: UserResponse | null
  isLoading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, fullName: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = React.createContext<AuthValue | null>(null)

function loadTokens(): { accessToken: string; refreshToken: string } | null {
  if (typeof window === "undefined") return null
  const access = sessionStorage.getItem("accessToken")
  const refresh = sessionStorage.getItem("refreshToken")
  if (access && refresh) return { accessToken: access, refreshToken: refresh }
  return null
}

function saveTokens(auth: AuthResponse) {
  sessionStorage.setItem("accessToken", auth.accessToken)
  sessionStorage.setItem("refreshToken", auth.refreshToken)
}

function clearTokens() {
  sessionStorage.removeItem("accessToken")
  sessionStorage.removeItem("refreshToken")
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = React.useState<UserResponse | null>(null)
  const [isLoading, setIsLoading] = React.useState(true)
  const refreshTimerRef = React.useRef<ReturnType<typeof setTimeout> | null>(null)

  const scheduleRefresh = React.useCallback((expiresAt: string) => {
    if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current)
    const expires = new Date(expiresAt).getTime()
    const delay = Math.max(expires - Date.now() - 60_000, 10_000)
    refreshTimerRef.current = setTimeout(async () => {
      const tokens = loadTokens()
      if (!tokens) return
      try {
        const newAuth = await apiRefresh(tokens.refreshToken)
        saveTokens(newAuth)
        scheduleRefresh(newAuth.expiresAt)
      } catch {
        clearTokens()
        setUser(null)
      }
    }, delay)
  }, [])

  const loadUser = React.useCallback(
    async (accessToken: string) => {
      try {
        const me = await apiGetMe(accessToken)
        setUser(me)
      } catch {
        clearTokens()
        setUser(null)
      }
    },
    []
  )

  React.useEffect(() => {
    const tokens = loadTokens()
    if (tokens) {
      loadUser(tokens.accessToken).finally(() => setIsLoading(false))
    } else {
      setIsLoading(false)
    }
    return () => {
      if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current)
    }
  }, [loadUser])

  const login = React.useCallback(
    async (email: string, password: string) => {
      const auth = await apiLogin(email, password)
      saveTokens(auth)
      scheduleRefresh(auth.expiresAt)
      const me = await apiGetMe(auth.accessToken)
      setUser(me)
    },
    [scheduleRefresh]
  )

  const register = React.useCallback(
    async (email: string, password: string, fullName: string) => {
      const auth = await apiRegister(email, password, fullName)
      saveTokens(auth)
      scheduleRefresh(auth.expiresAt)
      const me = await apiGetMe(auth.accessToken)
      setUser(me)
    },
    [scheduleRefresh]
  )

  const logout = React.useCallback(async () => {
    const tokens = loadTokens()
    if (tokens) {
      await apiLogout(tokens.refreshToken)
    }
    clearTokens()
    setUser(null)
    if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current)
  }, [])

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = React.useContext(AuthContext)
  if (!ctx) throw new Error("useAuth must be used within AuthProvider")
  return ctx
}
