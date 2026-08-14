"use client"

import * as React from "react"
import { toast } from "sonner"
import {
  Shield,
  Calendar,
  LogOut,
  Save,
  Lock,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { useAuth } from "@/lib/auth"
import { apiUpdateProfile, apiChangePassword } from "@/lib/api"

const ROLE_LABELS: Record<string, string> = {
  Admin: "Administrator",
  "IT Support Agent": "IT Support Agent",
  Employee: "Employee",
  Manager: "Manager",
}

function initials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .slice(0, 2)
    .join("")
}

export default function ProfilePage() {
  const { user, logout, refreshUser } = useAuth()

  const [fullName, setFullName] = React.useState("")
  const [email, setEmail] = React.useState("")
  const [savingProfile, setSavingProfile] = React.useState(false)

  const [currentPassword, setCurrentPassword] = React.useState("")
  const [newPassword, setNewPassword] = React.useState("")
  const [confirmPassword, setConfirmPassword] = React.useState("")
  const [savingPassword, setSavingPassword] = React.useState(false)

  React.useEffect(() => {
    if (user) {
      setFullName(user.fullName)
      setEmail(user.email)
    }
  }, [user])

  if (!user) {
    return (
      <div className="flex items-center justify-center py-20">
        <p className="text-sm text-muted-foreground">Loading profile...</p>
      </div>
    )
  }

  async function handleSaveProfile() {
    if (!fullName.trim() || !email.trim()) return
    setSavingProfile(true)
    try {
      await apiUpdateProfile({ fullName: fullName.trim(), email: email.trim() })
      await refreshUser()
      toast.success("Profile updated")
    } catch (err: any) {
      toast.error("Failed to update profile", {
        description: err?.message || "Please try again.",
      })
    } finally {
      setSavingProfile(false)
    }
  }

  async function handleChangePassword() {
    if (!currentPassword || !newPassword) return
    if (newPassword !== confirmPassword) {
      toast.error("New passwords do not match")
      return
    }
    if (newPassword.length < 8) {
      toast.error("Password must be at least 8 characters")
      return
    }
    setSavingPassword(true)
    try {
      await apiChangePassword({ currentPassword, newPassword })
      toast.success("Password changed")
      setCurrentPassword("")
      setNewPassword("")
      setConfirmPassword("")
    } catch (err: any) {
      toast.error("Failed to change password", {
        description: err?.message || "Please try again.",
      })
    } finally {
      setSavingPassword(false)
    }
  }

  return (
    <div className="mx-auto max-w-2xl flex flex-col gap-6">
      <Card>
        <CardHeader>
          <div className="flex items-center gap-4">
            <Avatar className="size-16">
              <AvatarFallback className="bg-primary/10 text-lg font-semibold text-primary">
                {initials(user.fullName)}
              </AvatarFallback>
            </Avatar>
            <div className="flex flex-col">
              <CardTitle>My Profile</CardTitle>
              <CardDescription>{user.email}</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="flex flex-col gap-6">
          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="profile-fullname">Full Name</Label>
              <Input
                id="profile-fullname"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                maxLength={200}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="profile-email">Email</Label>
              <Input
                id="profile-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
          </div>

          <div className="flex flex-col gap-3">
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
                    ? new Date(user.createdAt.endsWith("Z") || /[+-]\d{2}:\d{2}$/.test(user.createdAt) ? user.createdAt : user.createdAt + "Z").toLocaleDateString("en-US", {
                        year: "numeric",
                        month: "long",
                        day: "numeric",
                      })
                    : "N/A"}
                </p>
              </div>
            </div>
          </div>

          <div className="flex justify-end">
            <Button
              onClick={handleSaveProfile}
              disabled={savingProfile || !fullName.trim() || !email.trim()}
            >
              <Save data-icon="inline-start" />
              {savingProfile ? "Saving..." : "Save Changes"}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Change Password</CardTitle>
          <CardDescription>
            Update your password. You&apos;ll remain signed in after the change.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="current-password">Current Password</Label>
            <Input
              id="current-password"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              placeholder="Enter current password"
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="new-password">New Password</Label>
            <Input
              id="new-password"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="Minimum 8 characters"
            />
            <p className="text-xs text-muted-foreground">
              Must be 8+ characters with uppercase, lowercase, digit, and special character.
            </p>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="confirm-password">Confirm New Password</Label>
            <Input
              id="confirm-password"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Re-enter new password"
            />
          </div>

          <div className="flex justify-end">
            <Button
              variant="outline"
              onClick={handleChangePassword}
              disabled={
                savingPassword ||
                !currentPassword ||
                !newPassword ||
                !confirmPassword
              }
            >
              <Lock data-icon="inline-start" />
              {savingPassword ? "Changing..." : "Change Password"}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="flex justify-end pt-2">
          <Button variant="destructive" onClick={async () => { await logout() }}>
            <LogOut data-icon="inline-start" />
            Sign out
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
