"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { toast } from "sonner"
import { ArrowLeftIcon } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { apiCreateUser, apiGetUsers } from "@/lib/api"
import type { ApiError } from "@/lib/api"

const ALL_ROLE_OPTIONS = [
  { value: "1", label: "Admin" },
  { value: "2", label: "IT Support Agent" },
  { value: "3", label: "Employee" },
  { value: "4", label: "Manager" },
]

export default function CreateUserPage() {
  const router = useRouter()

  const [fullName, setFullName] = React.useState("")
  const [email, setEmail] = React.useState("")
  const [password, setPassword] = React.useState("")
  const [roleId, setRoleId] = React.useState("")
  const [saving, setSaving] = React.useState(false)
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({})
  const [roleOptions, setRoleOptions] = React.useState(ALL_ROLE_OPTIONS)

  React.useEffect(() => {
    apiGetUsers(undefined, 1, undefined, 1, 1)
      .then((data) => {
        if (data.totalCount > 0) {
          setRoleOptions(ALL_ROLE_OPTIONS.filter((r) => r.value !== "1"))
        }
      })
      .catch(() => {})
  }, [])

  async function handleSubmit() {
    if (!fullName.trim() || !email.trim() || !password || !roleId) return
    setSaving(true)
    setFieldErrors({})
    try {
      await apiCreateUser({
        fullName: fullName.trim(),
        email: email.trim(),
        password,
        roleId: Number(roleId),
      })
      toast.success("User created successfully")
      router.push("/admin/users")
    } catch (err: any) {
      const apiErr = err as ApiError
      if (apiErr?.errors && Object.keys(apiErr.errors).length > 0) {
        setFieldErrors(apiErr.errors)
      } else {
        toast.error("Failed to create user", {
          description: apiErr?.message || "Please try again.",
        })
      }
    } finally {
      setSaving(false)
    }
  }

  function fieldError(name: string): string | undefined {
    return fieldErrors[name]?.[0]
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <button
          onClick={() => router.push("/admin/users")}
          className="inline-flex w-fit items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeftIcon className="size-3" />
          Back to Users
        </button>
        <h1 className="text-2xl font-semibold tracking-tight text-balance">
          Create New User
        </h1>
        <p className="text-sm text-muted-foreground">
          Add a new user to the helpdesk platform.
        </p>
      </div>

      <Card className="max-w-lg p-6">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="fullname">Full Name *</Label>
            <Input
              id="fullname"
              value={fullName}
              onChange={(e) => { setFullName(e.target.value); setFieldErrors((p) => { const n = { ...p }; delete n.FullName; return n }) }}
              placeholder="e.g. Jane Smith"
              maxLength={200}
              className={fieldError("FullName") ? "border-destructive" : ""}
            />
            {fieldError("FullName") && (
              <p className="text-xs text-destructive">{fieldError("FullName")}</p>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="email">Email *</Label>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={(e) => { setEmail(e.target.value); setFieldErrors((p) => { const n = { ...p }; delete n.Email; return n }) }}
              placeholder="jane@company.com"
              className={fieldError("Email") ? "border-destructive" : ""}
            />
            {fieldError("Email") && (
              <p className="text-xs text-destructive">{fieldError("Email")}</p>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="password">Password *</Label>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={(e) => { setPassword(e.target.value); setFieldErrors((p) => { const n = { ...p }; delete n.Password; return n }) }}
              placeholder="Minimum 8 characters"
              className={fieldError("Password") ? "border-destructive" : ""}
            />
            {fieldError("Password") && (
              <p className="text-xs text-destructive">{fieldError("Password")}</p>
            )}
            {!fieldError("Password") && (
              <p className="text-xs text-muted-foreground">
                Must be 8+ characters with uppercase, lowercase, digit, and special character.
              </p>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <Label>Role *</Label>
            <Select items={roleOptions.reduce((acc, r) => ({ ...acc, [r.value]: r.label }), {} as Record<string, string>)} value={roleId} onValueChange={(v) => { setRoleId(v); setFieldErrors((p) => { const n = { ...p }; delete n.RoleId; return n }) }}>
              <SelectTrigger className={fieldError("RoleId") ? "border-destructive" : ""}>
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
            {fieldError("RoleId") && (
              <p className="text-xs text-destructive">{fieldError("RoleId")}</p>
            )}
          </div>
        </div>

        <div className="mt-6 flex items-center gap-3">
          <Button
            variant="outline"
            onClick={() => router.push("/admin/users")}
            disabled={saving}
          >
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={saving || !fullName.trim() || !email.trim() || !password || !roleId}
          >
            {saving ? "Creating..." : "Create User"}
          </Button>
        </div>
      </Card>
    </div>
  )
}
