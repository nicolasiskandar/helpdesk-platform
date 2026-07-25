"use client"

import * as React from "react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { apiActivateUser, apiDeactivateUser, apiDeleteUser } from "@/lib/api"
import type { UserResponse } from "@/lib/api"

interface DeleteUserDialogProps {
  user: UserResponse | null
  mode: "deactivate" | "activate" | "delete"
  open: boolean
  onOpenChange: (open: boolean) => void
  onAction: () => void
}

export function DeleteUserDialog({ user, mode, open, onOpenChange, onAction }: DeleteUserDialogProps) {
  const [loading, setLoading] = React.useState(false)

  async function handleConfirm() {
    if (!user) return
    setLoading(true)
    try {
      if (mode === "activate") {
        await apiActivateUser(user.id)
        toast.success("User activated")
      } else if (mode === "deactivate") {
        await apiDeactivateUser(user.id)
        toast.success("User deactivated")
      } else {
        await apiDeleteUser(user.id)
        toast.success("User deleted")
      }
      onOpenChange(false)
      onAction()
    } catch (err: any) {
      const label = mode === "activate" ? "activate" : mode === "deactivate" ? "deactivate" : "delete"
      toast.error(`Failed to ${label} user`, {
        description: err?.message || "Please try again.",
      })
    } finally {
      setLoading(false)
    }
  }

  const title =
    mode === "activate" ? "Activate User"
    : mode === "delete" ? "Delete User"
    : "Deactivate User"

  const description =
    mode === "activate" ? (
      <>Are you sure you want to activate <strong>{user?.fullName}</strong>? They will be able to log in again.</>
    ) : mode === "delete" ? (
      <>Are you sure you want to permanently delete <strong>{user?.fullName}</strong>? This action cannot be undone.</>
    ) : (
      <>Are you sure you want to deactivate <strong>{user?.fullName}</strong>? They will no longer be able to log in. You can reactivate them later.</>
    )

  const buttonLabel =
    mode === "activate" ? (loading ? "Activating..." : "Activate")
    : mode === "delete" ? (loading ? "Deleting..." : "Delete")
    : (loading ? "Deactivating..." : "Deactivate")

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={loading}>
            Cancel
          </Button>
          <Button
            variant={mode === "delete" ? "destructive" : "default"}
            onClick={handleConfirm}
            disabled={loading}
          >
            {buttonLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
