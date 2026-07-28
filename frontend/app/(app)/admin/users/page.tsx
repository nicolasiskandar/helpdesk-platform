"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { toast } from "sonner"
import { PlusIcon, SearchIcon, PencilIcon, TrashIcon, UserMinusIcon, UserPlusIcon } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Badge } from "@/components/ui/badge"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { EditUserDialog } from "@/components/edit-user-dialog"
import { DeleteUserDialog } from "@/components/delete-user-dialog"
import { RoleGuard } from "@/components/role-guard"
import { useAuth } from "@/lib/auth"
import { apiGetUsers } from "@/lib/api"
import type { UserResponse } from "@/lib/api"

const ROLE_OPTIONS = [
  { value: "1", label: "Admin" },
  { value: "2", label: "IT Support Agent" },
  { value: "3", label: "Employee" },
  { value: "4", label: "Manager" },
]

const ROLE_BADGE: Record<string, string> = {
  Admin: "bg-violet-100 text-violet-800 dark:bg-violet-900/30 dark:text-violet-400",
  "IT Support Agent": "bg-sky-100 text-sky-800 dark:bg-sky-900/30 dark:text-sky-400",
  Employee: "bg-neutral-100 text-neutral-800 dark:bg-neutral-900/30 dark:text-neutral-400",
  Manager: "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400",
}

function initials(name: string) {
  return name
    .split(" ")
    .map((w) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2)
}

export default function UsersPage() {
  const router = useRouter()
  const { user: currentUser } = useAuth()

  const [users, setUsers] = React.useState<UserResponse[]>([])
  const [totalCount, setTotalCount] = React.useState(0)
  const [page, setPage] = React.useState(1)
  const [loading, setLoading] = React.useState(true)
  const [search, setSearch] = React.useState("")
  const [roleFilter, setRoleFilter] = React.useState<string>("")
  const [statusFilter, setStatusFilter] = React.useState<string>("")

  const [editUser, setEditUser] = React.useState<UserResponse | null>(null)
  const [editOpen, setEditOpen] = React.useState(false)

  const [deleteUser, setDeleteUser] = React.useState<UserResponse | null>(null)
  const [deleteMode, setDeleteMode] = React.useState<"deactivate" | "activate" | "delete">("deactivate")
  const [deleteOpen, setDeleteOpen] = React.useState(false)

  const pageSize = 10

  const fetchUsers = React.useCallback(async () => {
    setLoading(true)
    try {
      const roleId = roleFilter ? Number(roleFilter) : undefined
      const isActive = statusFilter === "active" ? true : statusFilter === "inactive" ? false : undefined
      const data = await apiGetUsers(search || undefined, roleId, isActive, page, pageSize)
      setUsers(data.users)
      setTotalCount(data.totalCount)
    } catch {
      toast.error("Failed to load users")
      setUsers([])
    } finally {
      setLoading(false)
    }
  }, [search, roleFilter, statusFilter, page])

  React.useEffect(() => {
    fetchUsers()
  }, [fetchUsers])

  React.useEffect(() => {
    setPage(1)
  }, [search, roleFilter, statusFilter])

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  function openEdit(user: UserResponse) {
    setEditUser(user)
    setEditOpen(true)
  }

  function openDeactivate(user: UserResponse) {
    setDeleteUser(user)
    setDeleteMode("deactivate")
    setDeleteOpen(true)
  }

  function openActivate(user: UserResponse) {
    setDeleteUser(user)
    setDeleteMode("activate")
    setDeleteOpen(true)
  }

  function openDelete(user: UserResponse) {
    setDeleteUser(user)
    setDeleteMode("delete")
    setDeleteOpen(true)
  }

  return (
    <RoleGuard allowedRoles={["admin"]}>
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">
            User Management
          </h1>
          <p className="text-sm text-muted-foreground">
            Create, edit, and manage user accounts and roles.
          </p>
        </div>
        <Button onClick={() => router.push("/admin/users/new")}>
          <PlusIcon data-icon="inline-start" />
          Add User
        </Button>
      </div>

      <Card className="flex flex-col gap-4 p-4">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
          <div className="relative flex-1 lg:max-w-xs">
            <SearchIcon className="absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search name or email..."
              className="pl-8"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <div className="flex flex-1 flex-wrap gap-3">
            <Select items={ROLE_OPTIONS.reduce((acc, r) => ({ ...acc, [r.value]: r.label }), {} as Record<string, string>)} value={roleFilter} onValueChange={setRoleFilter}>
              <SelectTrigger className="w-[160px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">All Roles</SelectItem>
                {ROLE_OPTIONS.map((r) => (
                  <SelectItem key={r.value} value={r.value}>
                    {r.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger className="w-[140px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Showing <span className="font-medium text-foreground">{users.length}</span>{" "}
            of {totalCount} users
          </p>
        </div>

        {loading ? (
          <div className="flex flex-col gap-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : users.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">No users found.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>User</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Joined</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.map((u) => {
                const isSelf = currentUser?.id === u.id
                return (
                  <TableRow key={u.id}>
                    <TableCell>
                      <div className="flex items-center gap-3">
                        <div className="flex size-8 items-center justify-center rounded-full bg-muted text-xs font-medium">
                          {initials(u.fullName)}
                        </div>
                        <div className="flex flex-col">
                          <span className="font-medium">{u.fullName}</span>
                          <span className="text-xs text-muted-foreground">{u.email}</span>
                        </div>
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="secondary" className={ROLE_BADGE[u.role] ?? ""}>
                        {u.role}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Badge variant={u.isActive ? "default" : "secondary"}>
                        {u.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {new Date(u.createdAt).toLocaleDateString()}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-1">
                        {!isSelf && (
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            onClick={() => openEdit(u)}
                            title="Edit"
                          >
                            <PencilIcon />
                          </Button>
                        )}
                        {!isSelf && (
                          <>
                            {u.isActive ? (
                              <Button
                                variant="ghost"
                                size="icon-sm"
                                onClick={() => openDeactivate(u)}
                                title="Deactivate"
                              >
                                <UserMinusIcon />
                              </Button>
                            ) : (
                              <Button
                                variant="ghost"
                                size="icon-sm"
                                onClick={() => openActivate(u)}
                                title="Activate"
                              >
                                <UserPlusIcon />
                              </Button>
                            )}
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              onClick={() => openDelete(u)}
                              title="Delete"
                              className="text-destructive hover:text-destructive"
                            >
                              <TrashIcon />
                            </Button>
                          </>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        )}

        {totalPages > 1 && (
          <div className="flex items-center justify-end gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              Previous
            </Button>
            <span className="text-sm text-muted-foreground">
              Page {page} of {totalPages}
            </span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
            >
              Next
            </Button>
          </div>
        )}
      </Card>

      <EditUserDialog
        user={editUser}
        open={editOpen}
        onOpenChange={setEditOpen}
        onUpdated={fetchUsers}
      />

      <DeleteUserDialog
        user={deleteUser}
        mode={deleteMode}
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        onAction={fetchUsers}
      />
    </div>
    </RoleGuard>
  )
}
