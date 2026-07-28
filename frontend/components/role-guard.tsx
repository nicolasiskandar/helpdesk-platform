"use client"

import { useStore } from "@/lib/store"
import { Skeleton } from "@/components/ui/skeleton"
import { ShieldOffIcon } from "lucide-react"
import type { Role } from "@/lib/types"

export function RoleGuard({
  allowedRoles,
  children,
}: {
  allowedRoles: Role[]
  children: React.ReactNode
}) {
  const { role, ticketsLoading } = useStore()

  if (ticketsLoading) {
    return (
      <div className="flex h-[50vh] items-center justify-center">
        <div className="flex flex-col gap-4 w-64">
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-4 w-3/4" />
        </div>
      </div>
    )
  }

  if (!allowedRoles.includes(role)) {
    return (
      <div className="flex h-[50vh] flex-col items-center justify-center gap-4 text-center">
        <div className="flex size-16 items-center justify-center rounded-full bg-destructive/10">
          <ShieldOffIcon className="size-8 text-destructive" />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight">Access Denied</h1>
          <p className="text-sm text-muted-foreground max-w-sm">
            You don&apos;t have permission to access this page. Contact your administrator if you believe this is a mistake.
          </p>
        </div>
      </div>
    )
  }

  return <>{children}</>
}
