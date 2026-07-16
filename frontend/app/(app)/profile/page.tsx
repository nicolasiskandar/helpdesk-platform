"use client"

import { useRouter } from "next/navigation"
import {
  User as UserIcon,
  Mail,
  Shield,
  Calendar,
  LogOut,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Separator } from "@/components/ui/separator"
import { useAuth } from "@/lib/auth"
import { ROLE_LABELS } from "@/lib/data"

function initials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .slice(0, 2)
    .join("")
}

export default function ProfilePage() {
  const router = useRouter()
  const { user, logout } = useAuth()

  if (!user) {
    return (
      <div className="flex items-center justify-center py-20">
        <p className="text-sm text-muted-foreground">Loading profile...</p>
      </div>
    )
  }

  async function handleLogout() {
    await logout()
    router.push("/login")
  }

  return (
    <div className="mx-auto max-w-2xl">
      <Card>
        <CardHeader>
          <CardTitle>My Profile</CardTitle>
          <CardDescription>Your account information</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-6">
          <div className="flex items-center gap-4">
            <Avatar className="size-16">
              <AvatarFallback className="bg-primary/10 text-lg font-semibold text-primary">
                {initials(user.fullName)}
              </AvatarFallback>
            </Avatar>
            <div className="flex flex-col">
              <h2 className="text-lg font-semibold">{user.fullName}</h2>
              <p className="text-sm text-muted-foreground">{user.email}</p>
            </div>
          </div>

          <Separator />

          <div className="grid gap-4">
            <div className="flex items-center gap-3">
              <div className="flex size-9 items-center justify-center rounded-lg bg-muted">
                <UserIcon className="size-4 text-muted-foreground" />
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Full Name</p>
                <p className="text-sm font-medium">{user.fullName}</p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <div className="flex size-9 items-center justify-center rounded-lg bg-muted">
                <Mail className="size-4 text-muted-foreground" />
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Email</p>
                <p className="text-sm font-medium">{user.email}</p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <div className="flex size-9 items-center justify-center rounded-lg bg-muted">
                <Shield className="size-4 text-muted-foreground" />
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Role</p>
                <p className="text-sm font-medium">
                  {ROLE_LABELS[user.role] || user.role}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <div className="flex size-9 items-center justify-center rounded-lg bg-muted">
                <Calendar className="size-4 text-muted-foreground" />
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Joined</p>
                <p className="text-sm font-medium">
                  {user.createdAt
                    ? new Date(user.createdAt).toLocaleDateString("en-US", {
                        year: "numeric",
                        month: "long",
                        day: "numeric",
                      })
                    : "N/A"}
                </p>
              </div>
            </div>
          </div>

          <Separator />

          <div className="flex justify-end">
            <Button variant="destructive" onClick={handleLogout}>
              <LogOut />
              Sign out
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
