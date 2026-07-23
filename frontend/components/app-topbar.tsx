"use client"

import Link from "next/link"
import { useRouter } from "next/navigation"
import { Bell, Search, LogOut, User as UserIcon, ChevronsUpDown } from "lucide-react"

import { SidebarTrigger } from "@/components/ui/sidebar"
import { Separator } from "@/components/ui/separator"
import { Button, buttonVariants } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { useStore } from "@/lib/store"
import { useAuth } from "@/lib/auth"
import { ROLE_LABELS } from "@/lib/data"
import type { Role } from "@/lib/types"

function initials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .slice(0, 2)
    .join("")
}

export function AppTopbar({ title }: { title: string }) {
  const router = useRouter()
  const { role, setRole, unreadCount } = useStore()
  const { user: authUser, logout } = useAuth()

  const displayName = authUser?.fullName || "User"
  const displayEmail = authUser?.email || ""

  async function handleLogout() {
    await logout()
    router.push("/login")
  }

  return (
    <header className="sticky top-0 z-30 flex h-14 shrink-0 items-center gap-2 border-b border-border bg-background/95 px-3 backdrop-blur supports-[backdrop-filter]:bg-background/80 md:px-4">
      <SidebarTrigger className="-ml-1" />
      <Separator orientation="vertical" className="mr-1 h-5" />
      <h1 className="text-sm font-semibold md:text-base">{title}</h1>

      <div className="ml-auto flex items-center gap-1.5 md:gap-2">
        <div className="hidden lg:block">
          <InputGroup className="w-64">
            <InputGroupInput
              placeholder="Search tickets, articles..."
              aria-label="Search"
              onKeyDown={(e) => {
                if (e.key === "Enter" && !e.nativeEvent.isComposing) {
                  router.push("/tickets")
                }
              }}
            />
            <InputGroupAddon>
              <Search />
            </InputGroupAddon>
          </InputGroup>
        </div>

        <Select value={role} onValueChange={(v) => setRole(v as Role)}>
          <SelectTrigger
            size="sm"
            className="w-auto gap-2"
            aria-label="Switch role"
          >
            <span className="hidden text-xs text-muted-foreground sm:inline">
              Viewing as
            </span>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(Object.keys(ROLE_LABELS) as Role[]).map((r) => (
              <SelectItem key={r} value={r}>
                {ROLE_LABELS[r]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Button
          variant="ghost"
          size="icon-sm"
          className="relative"
          render={
            <Link href="/notifications" aria-label="Notifications">
              <Bell />
              {unreadCount > 0 ? (
                <Badge className="absolute -top-1 -right-1 size-4 rounded-full p-0 text-[10px] tabular-nums">
                  {unreadCount}
                </Badge>
              ) : null}
            </Link>
          }
        />

        <DropdownMenu>
          <DropdownMenuTrigger
            className={cn(
              buttonVariants({ variant: "ghost" }),
              "h-9 gap-2 px-1.5"
            )}
          >
            <Avatar className="size-7">
              <AvatarFallback className="bg-primary/10 text-xs font-medium text-primary">
                {initials(displayName)}
              </AvatarFallback>
            </Avatar>
            <div className="hidden flex-col items-start leading-tight md:flex">
              <span className="text-xs font-medium">{displayName}</span>
              <span className="text-[11px] text-muted-foreground">
                {ROLE_LABELS[role]}
              </span>
            </div>
            <ChevronsUpDown className="hidden size-3.5 text-muted-foreground md:block" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel>
              <div className="flex flex-col">
                <span className="text-sm font-medium">{displayName}</span>
                <span className="text-xs font-normal text-muted-foreground">
                  {displayEmail}
                </span>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem
                render={
                  <Link href="/profile">
                    <UserIcon />
                    Profile
                  </Link>
                }
              />
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              variant="destructive"
              onClick={handleLogout}
            >
              <LogOut />
              Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}
