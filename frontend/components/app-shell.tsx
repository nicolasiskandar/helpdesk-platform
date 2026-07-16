"use client"

import { usePathname } from "next/navigation"
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar"
import { AppSidebar } from "@/components/app-sidebar"
import { AppTopbar } from "@/components/app-topbar"

const TITLES: { match: (p: string) => boolean; title: string }[] = [
  { match: (p) => p === "/dashboard", title: "Dashboard" },
  { match: (p) => p === "/tickets/new", title: "Create Ticket" },
  { match: (p) => /^\/tickets\/HLX-/.test(p), title: "Ticket Details" },
  { match: (p) => p === "/tickets", title: "Tickets" },
  { match: (p) => p === "/reports", title: "Reports & Analytics" },
  { match: (p) => p.startsWith("/knowledge-base"), title: "Knowledge Base" },
  { match: (p) => p === "/assistant", title: "AI Assistant" },
  { match: (p) => p === "/notifications", title: "Notifications" },
  { match: (p) => p === "/profile", title: "My Profile" },
  { match: (p) => p === "/admin/users", title: "User Management" },
  { match: (p) => p === "/admin/settings", title: "Admin Settings" },
]

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const title = TITLES.find((t) => t.match(pathname))?.title ?? "IT Service Desk"

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <AppTopbar title={title} />
        <div className="flex-1 overflow-x-hidden p-4 md:p-6">{children}</div>
      </SidebarInset>
    </SidebarProvider>
  )
}
