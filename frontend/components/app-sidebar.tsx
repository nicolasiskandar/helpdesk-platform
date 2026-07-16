"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import {
  LayoutDashboard,
  Ticket,
  PlusCircle,
  BarChart3,
  BookOpen,
  Bell,
  Bot,
  Settings,
  Users,
  LifeBuoy,
} from "lucide-react"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuBadge,
} from "@/components/ui/sidebar"
import { useStore } from "@/lib/store"
import type { Role } from "@/lib/types"

interface NavItem {
  title: string
  href: string
  icon: React.ComponentType<{ className?: string }>
  roles: Role[]
  badge?: "unread" | "open"
}

const mainNav: NavItem[] = [
  {
    title: "Dashboard",
    href: "/dashboard",
    icon: LayoutDashboard,
    roles: ["admin", "agent", "manager", "employee"],
  },
  {
    title: "Tickets",
    href: "/tickets",
    icon: Ticket,
    roles: ["admin", "agent", "manager", "employee"],
    badge: "open",
  },
  {
    title: "Create Ticket",
    href: "/tickets/new",
    icon: PlusCircle,
    roles: ["admin", "agent", "manager", "employee"],
  },
  {
    title: "Reports",
    href: "/reports",
    icon: BarChart3,
    roles: ["admin", "manager"],
  },
]

const supportNav: NavItem[] = [
  {
    title: "Knowledge Base",
    href: "/knowledge-base",
    icon: BookOpen,
    roles: ["admin", "agent", "manager", "employee"],
  },
  {
    title: "AI Assistant",
    href: "/assistant",
    icon: Bot,
    roles: ["admin", "agent", "manager", "employee"],
  },
  {
    title: "Notifications",
    href: "/notifications",
    icon: Bell,
    roles: ["admin", "agent", "manager", "employee"],
    badge: "unread",
  },
]

const adminNav: NavItem[] = [
  {
    title: "User Management",
    href: "/admin/users",
    icon: Users,
    roles: ["admin"],
  },
  {
    title: "Admin Settings",
    href: "/admin/settings",
    icon: Settings,
    roles: ["admin"],
  },
]

export function AppSidebar() {
  const pathname = usePathname()
  const { role, tickets, unreadCount } = useStore()

  const openCount = tickets.filter(
    (t) => t.status === "Open" || t.status === "In Progress"
  ).length

  function isActive(href: string) {
    if (href === "/tickets") {
      return pathname === "/tickets" || /^\/tickets\/HLX-/.test(pathname)
    }
    return pathname === href
  }

  function renderItems(items: NavItem[]) {
    return items
      .filter((item) => item.roles.includes(role))
      .map((item) => {
        const badgeValue =
          item.badge === "unread"
            ? unreadCount
            : item.badge === "open"
              ? openCount
              : 0
        return (
          <SidebarMenuItem key={item.href}>
            <SidebarMenuButton
              isActive={isActive(item.href)}
              tooltip={item.title}
              render={
                <Link href={item.href}>
                  <item.icon />
                  <span>{item.title}</span>
                </Link>
              }
            />
            {item.badge && badgeValue > 0 ? (
              <SidebarMenuBadge>{badgeValue}</SidebarMenuBadge>
            ) : null}
          </SidebarMenuItem>
        )
      })
  }

  return (
    <Sidebar>
      <SidebarHeader>
        <div className="flex items-center gap-2.5 px-2 py-1.5">
          <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <LifeBuoy className="size-5" />
          </div>
          <div className="flex flex-col leading-tight">
            <span className="text-sm font-semibold">IT Service Desk</span>
            <span className="text-[11px] text-muted-foreground">
              Support & Ticketing
            </span>
          </div>
        </div>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Workspace</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>{renderItems(mainNav)}</SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
        <SidebarGroup>
          <SidebarGroupLabel>Support</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>{renderItems(supportNav)}</SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
        {role === "admin" ? (
          <SidebarGroup>
            <SidebarGroupLabel>Administration</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>{renderItems(adminNav)}</SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ) : null}
      </SidebarContent>
      <SidebarFooter>
        <div className="rounded-lg border border-sidebar-border bg-sidebar-accent/40 p-3">
          <p className="text-xs font-medium text-sidebar-accent-foreground">
            SLA Compliance
          </p>
          <p className="mt-0.5 text-lg font-semibold tabular-nums">96.4%</p>
          <p className="text-xs text-muted-foreground">Last 30 days</p>
        </div>
      </SidebarFooter>
    </Sidebar>
  )
}
