"use client"

import * as React from "react"
import { toast } from "sonner"
import { SaveIcon, RefreshCwIcon } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { RoleGuard } from "@/components/role-guard"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import { apiGetSettings, apiUpdateSettings } from "@/lib/api"
import type { SettingResponse } from "@/lib/api"

type SettingValue = string

export default function AdminSettingsPage() {
  const [settings, setSettings] = React.useState<SettingResponse[]>([])
  const [values, setValues] = React.useState<Record<string, SettingValue>>({})
  const [loading, setLoading] = React.useState(true)
  const [saving, setSaving] = React.useState(false)

  const load = React.useCallback(async () => {
    setLoading(true)
    try {
      const data = await apiGetSettings()
      setSettings(data)
      const map: Record<string, string> = {}
      for (const s of data) map[s.key] = s.value
      setValues(map)
    } catch {
      toast.error("Failed to load settings")
    } finally {
      setLoading(false)
    }
  }, [])

  React.useEffect(() => { load() }, [load])

  const hasChanges = React.useMemo(() => {
    if (settings.length === 0) return false
    return settings.some((s) => values[s.key] !== s.value)
  }, [settings, values])

  async function handleSave() {
    setSaving(true)
    try {
      const changed: { key: string; value: string }[] = []
      for (const s of settings) {
        if (values[s.key] !== s.value) changed.push({ key: s.key, value: values[s.key] })
      }
      if (changed.length === 0) {
        toast.info("No changes to save")
        return
      }
      await apiUpdateSettings(changed)
      toast.success("Settings saved")
      await load()
    } catch (err: any) {
      toast.error("Failed to save settings", {
        description: err?.message || "Please try again.",
      })
    } finally {
      setSaving(false)
    }
  }

  function set(key: string, value: string) {
    setValues((prev) => ({ ...prev, [key]: value }))
  }

  function setting(key: string) {
    return settings.find((s) => s.key === key)
  }

  if (loading) {
    return (
      <RoleGuard allowedRoles={["admin"]}>
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold tracking-tight">Admin Settings</h1>
            <p className="text-sm text-muted-foreground">Configure system-wide preferences.</p>
          </div>
          <Skeleton className="h-80 w-full" />
        </div>
      </RoleGuard>
    )
  }

  return (
    <RoleGuard allowedRoles={["admin"]}>
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold tracking-tight">Admin Settings</h1>
            <p className="text-sm text-muted-foreground">Configure system-wide preferences.</p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={load} disabled={loading}>
              <RefreshCwIcon data-icon="inline-start" className={loading ? "animate-spin" : ""} />
              Refresh
            </Button>
            <Button onClick={handleSave} disabled={saving || !hasChanges}>
              <SaveIcon data-icon="inline-start" />
              {saving ? "Saving..." : "Save Changes"}
            </Button>
          </div>
        </div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          <TextField
            label="Auto-close (days)"
            description={setting("ticket_auto_close_days")?.description ?? ""}
            value={values["ticket_auto_close_days"] ?? ""}
            onChange={(v) => set("ticket_auto_close_days", v)}
          />

          <SelectField
            label="Default Priority"
            description={setting("default_ticket_priority")?.description ?? ""}
            value={values["default_ticket_priority"] ?? "Medium"}
            onChange={(v) => set("default_ticket_priority", v ?? "Medium")}
            options={[
              { value: "Low", label: "Low" },
              { value: "Medium", label: "Medium" },
              { value: "High", label: "High" },
              { value: "Critical", label: "Critical" },
            ]}
          />

          <TextField
            label="Max Agent Tickets"
            description={setting("max_agent_active_tickets")?.description ?? ""}
            value={values["max_agent_active_tickets"] ?? ""}
            onChange={(v) => set("max_agent_active_tickets", v)}
          />

          <TextField
            label="High Priority SLA (hours)"
            description={setting("sla_high_hours")?.description ?? ""}
            value={values["sla_high_hours"] ?? ""}
            onChange={(v) => set("sla_high_hours", v)}
          />

          <TextField
            label="Critical Priority SLA (hours)"
            description={setting("sla_critical_hours")?.description ?? ""}
            value={values["sla_critical_hours"] ?? ""}
            onChange={(v) => set("sla_critical_hours", v)}
          />

          <SwitchField
            label="Employee Ticket Creation"
            description={setting("allow_employee_ticket_create")?.description ?? ""}
            checked={values["allow_employee_ticket_create"] === "true"}
            onChange={(v) => set("allow_employee_ticket_create", v ? "true" : "false")}
          />
        </div>
      </div>
    </RoleGuard>
  )
}

function TextField({
  label,
  description,
  value,
  onChange,
}: {
  label: string
  description: string
  value: string
  onChange: (v: string) => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium">{label}</CardTitle>
        <p className="text-xs text-muted-foreground">{description}</p>
      </CardHeader>
      <CardContent>
        <Input value={value} onChange={(e) => onChange(e.target.value)} />
      </CardContent>
    </Card>
  )
}

function SelectField({
  label,
  description,
  value,
  onChange,
  options,
}: {
  label: string
  description: string
  value: string
  onChange: (v: string | null) => void
  options: { value: string; label: string }[]
}) {
  const items = React.useMemo(() => {
    const map: Record<string, string> = {}
    for (const o of options) map[o.value] = o.label
    return map
  }, [options])

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium">{label}</CardTitle>
        <p className="text-xs text-muted-foreground">{description}</p>
      </CardHeader>
      <CardContent>
        <Select items={items} value={value} onValueChange={onChange}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {options.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </CardContent>
    </Card>
  )
}

function SwitchField({
  label,
  description,
  checked,
  onChange,
}: {
  label: string
  description: string
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium">{label}</CardTitle>
        <p className="text-xs text-muted-foreground">{description}</p>
      </CardHeader>
      <CardContent>
        <Switch checked={checked} onCheckedChange={onChange} />
      </CardContent>
    </Card>
  )
}
