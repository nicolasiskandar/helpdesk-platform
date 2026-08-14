"use client"

import * as React from "react"
import { CheckCheck, Settings } from "lucide-react"
import { useRouter } from "next/navigation"
import Link from "next/link"
import { useStore } from "@/lib/store"
import { useSignalR } from "@/lib/signalr"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Skeleton } from "@/components/ui/skeleton"

const NOTIF_ICONS: Record<string, string> = {
  assignment: "\u{1F4CB}",
  comment: "\u{1F4AC}",
  status: "\u{1F504}",
  mention: "\u{1F514}",
  sla: "\u{23F0}",
}

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return "just now"
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  const days = Math.floor(hrs / 24)
  return `${days}d ago`
}

export default function NotificationsPage() {
  const {
    notifications,
    unreadCount,
    markNotificationRead,
    markAllNotificationsRead,
    refreshNotifications,
  } = useStore()
  const router = useRouter()
  const [loading, setLoading] = React.useState(true)

  React.useEffect(() => {
    refreshNotifications().finally(() => setLoading(false))
  }, [refreshNotifications])

  const handleRealtimeNotification = React.useCallback(
    () => {
      refreshNotifications()
    },
    [refreshNotifications]
  )

  const handleRealtimeUnreadCount = React.useCallback(() => {
    refreshNotifications()
  }, [refreshNotifications])

  useSignalR(handleRealtimeNotification, handleRealtimeUnreadCount)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Notifications</h1>
          <p className="text-muted-foreground">
            {unreadCount > 0
              ? `You have ${unreadCount} unread notification${unreadCount !== 1 ? "s" : ""}`
              : "All caught up"}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="icon" render={<Link href="/notifications/settings" />}>
              <Settings className="size-4" />
              <span className="sr-only">Notification settings</span>
          </Button>
          {unreadCount > 0 && (
            <Button variant="outline" size="sm" onClick={markAllNotificationsRead}>
              <CheckCheck className="mr-1.5 size-4" />
              Mark all read
            </Button>
          )}
        </div>
      </div>

      <Card>
        <CardContent className="p-0">
          {loading ? (
            <div className="divide-y">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="flex items-start gap-3 p-4">
                  <Skeleton className="size-8 shrink-0 rounded-full" />
                  <div className="flex-1 space-y-1.5">
                    <Skeleton className="h-4 w-48" />
                    <Skeleton className="h-3.5 w-64" />
                  </div>
                  <Skeleton className="h-3 w-12 shrink-0" />
                </div>
              ))}
            </div>
          ) : notifications.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-muted-foreground">
              <p className="text-sm">No notifications yet.</p>
            </div>
          ) : (
            <ScrollArea className="h-[calc(100vh-20rem)]">
              <div className="divide-y">
                {notifications.map((n) => (
                  <button
                    key={n.id}
                    onClick={() => {
                      if (!n.read) markNotificationRead(n.id)
                      if (n.commentId && n.ticketId) {
                        router.push(`/tickets/${n.ticketId}?comment=${n.commentId}`)
                      } else if (n.ticketId) {
                        router.push(`/tickets/${n.ticketId}`)
                      }
                    }}
                    className={`flex w-full items-start gap-3 p-4 text-left transition-colors hover:bg-muted/50 ${
                      !n.read ? "bg-primary/5" : ""
                    }`}
                  >
                    <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-muted text-base">
                      {NOTIF_ICONS[n.type] || "\u{1F514}"}
                    </span>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className={`text-sm ${!n.read ? "font-semibold" : ""}`}>
                          {n.title}
                        </span>
                        {!n.read && (
                          <span className="size-2 shrink-0 rounded-full bg-primary" />
                        )}
                      </div>
                      <p className="text-sm text-muted-foreground line-clamp-1">{n.body}</p>
                      {n.ticketRef && (
                        <span className="mt-1 inline-block text-xs font-medium text-primary">
                          {n.ticketRef}
                        </span>
                      )}
                    </div>
                    <span className="shrink-0 text-xs text-muted-foreground whitespace-nowrap">
                      {timeAgo(n.createdAt)}
                    </span>
                  </button>
                ))}
              </div>
            </ScrollArea>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
