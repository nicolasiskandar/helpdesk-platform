import type { ReactNode } from "react"
import { StoreProvider } from "@/lib/store"
import { AppShell } from "@/components/app-shell"
import { AuthGuard } from "@/components/auth-guard"

export default function AppGroupLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGuard>
      <StoreProvider>
        <AppShell>{children}</AppShell>
      </StoreProvider>
    </AuthGuard>
  )
}
