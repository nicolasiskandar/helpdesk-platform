"use client"

import { useEffect, useRef, useCallback, useState } from "react"
import * as signalR from "@microsoft/signalr"
import { getAccessToken } from "./api"

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"

export interface RealtimeNotification {
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

export function useSignalR(onNotification: (n: RealtimeNotification) => void, onUnreadCount: (count: number) => void) {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const [connected, setConnected] = useState(false)

  const start = useCallback(async () => {
    const token = getAccessToken()
    if (!token) return

    if (connectionRef.current) {
      await connectionRef.current.stop().catch(() => {})
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/notifications`, {
        accessTokenFactory: () => getAccessToken(),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on("NewNotification", (notification: RealtimeNotification) => {
      onNotification(notification)
    })

    connection.on("UnreadCountUpdated", (count: number) => {
      onUnreadCount(count)
    })

    connection.onreconnected(() => setConnected(true))
    connection.onclose(() => setConnected(false))

    try {
      await connection.start()
      connectionRef.current = connection
      setConnected(true)
    } catch {
      setConnected(false)
    }
  }, [onNotification, onUnreadCount])

  useEffect(() => {
    start()
    return () => {
      connectionRef.current?.stop()
    }
  }, [start])

  return { connected }
}
