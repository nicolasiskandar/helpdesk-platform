"use client"

import * as React from "react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { apiUpdateUser, apiGetUsers } from "@/lib/api"
import type { UserResponse } from "@/lib/api"

const ALL_ROLE_OPTIONS = [
  { value: "1", label: "Admin" },
  { value: "2", label: "IT Support Agent" },
  { value: "3", label: "Employee" },
  { value: "4", label: "Manager" },
]

const ROLE_MAP: Record<string, number> = {
  Admin: 1,
  "IT Support Agent": 2,
  Employee: 3,
  Manager: 4,
}

interface EditUserDialogProps {
  user: UserResponse | null
  open: boolean
  onOpenChange: (open: boolean) => void
  onUpdated: () => void
}

export function EditUserDialog({ user, open, onOpenChange, onUpdated }: EditUserDialogProps) {
  const [fullName, setFullName] = React.useState("")
  const [email, setEmail] = React.useState("")
  const [roleId, setRoleId] = React.useState("")
  const [saving, setSaving] = React.useState(false)
  const [roleOptions, setRoleOptions] = React.useState(ALL_ROLE_OPTIONS)

  React.useEffect(() => {
    if (user) {
      setFullName(user.fullName)
      setEmail(user.email)
      const userRoleId = String(ROLE_MAP[user.role] ?? 3)
      setRoleId(userRoleId)

      apiGetUsers(undefined, 1, undefined, 1, 1)
        .then((data) => {
          if (data.totalCount > 0 && userRoleId !== "1") {
            setRoleOptions(ALL_ROLE_OPTIONS.filter((r) => r.value !== "1"))
          } else {
            setRoleOptions(ALL_ROLE_OPTIONS)
          }
        })
        .catch(() => setRoleOptions(ALL_ROLE_OPTIONS))
    }
  }, [user])

  async function handleSave() {
    if (!user || !fullName.trim() || !email.trim() || !roleId) return
    setSaving(true)
    try {
      await apiUpdateUser(user.id, {
        fullName: fullName.trim(),
        email: email.trim(),
        roleId: Number(roleId),
      })
      toast.success("User updated")
      onOpenChange(false)
      onUpdated()
    } catch (err: any) {
      toast.error("Failed to update user", {
        description: err?.message || "Please try again.",
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit User</DialogTitle>
          <DialogDescription>
            Update the user&apos;s profile information and role.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-fullname">Full Name</Label>
            <Input
              id="edit-fullname"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              maxLength={200}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-email">Email</Label>
            <Input
              id="edit-email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label>Role</Label>
            <Select items={roleOptions.reduce((acc, r) => ({ ...acc, [r.value]: r.label }), {} as Record<string, string>)} value={roleId} onValueChange={setRoleId}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {roleOptions.map((r) => (
                  <SelectItem key={r.value} value={r.value}>
                    {r.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={saving || !fullName.trim() || !email.trim() || !roleId}>
            {saving ? "Saving..." : "Save Changes"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
