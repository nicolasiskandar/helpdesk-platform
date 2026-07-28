"use client"

import * as React from "react"
import {
  apiGetNotificationPreferences,
  apiUpdateNotificationPreferences,
  type PreferenceResponse,
} from "@/lib/api"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { toast } from "sonner"

function PreferenceRow({
  label,
  inApp,
  email,
  onInAppChange,
  onEmailChange,
}: {
  label: string
  inApp: boolean
  email: boolean
  onInAppChange: (v: boolean) => void
  onEmailChange: (v: boolean) => void
}) {
  return (
    <div className="flex items-center justify-between gap-4 py-3">
      <span className="text-sm font-medium min-w-[200px]">{label}</span>
      <div className="flex items-center gap-6">
        <div className="flex items-center gap-2">
          <Switch checked={inApp} onCheckedChange={onInAppChange} />
          <Label className="text-xs text-muted-foreground">In-app</Label>
        </div>
        <div className="flex items-center gap-2">
          <Switch checked={email} onCheckedChange={onEmailChange} />
          <Label className="text-xs text-muted-foreground">Email</Label>
        </div>
      </div>
    </div>
  )
}

export default function NotificationSettingsPage() {
  const [prefs, setPrefs] = React.useState<PreferenceResponse | null>(null)
  const [loading, setLoading] = React.useState(true)
  const [saving, setSaving] = React.useState(false)

  React.useEffect(() => {
    apiGetNotificationPreferences()
      .then(setPrefs)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  const update = (patch: Partial<PreferenceResponse>) => {
    setPrefs((prev) => (prev ? { ...prev, ...patch } : prev))
  }

  const save = async () => {
    if (!prefs) return
    setSaving(true)
    try {
      await apiUpdateNotificationPreferences(prefs)
      toast.success("Notification preferences saved")
    } catch {
      toast.error("Failed to save preferences")
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Notification Settings</h1>
        <p className="text-muted-foreground">Choose how you want to be notified.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Event Notifications</CardTitle>
          <CardDescription>
            Control in-app and email notifications for each event type.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading || !prefs ? (
            <div className="space-y-4">
              {Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="flex items-center justify-between">
                  <Skeleton className="h-4 w-40" />
                  <div className="flex gap-6">
                    <Skeleton className="h-5 w-10" />
                    <Skeleton className="h-5 w-10" />
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between gap-4 border-b pb-2 mb-1">
                <span className="text-xs font-medium text-muted-foreground uppercase tracking-wider min-w-[200px]">
                  Event
                </span>
                <div className="flex items-center gap-6">
                  <span className="text-xs font-medium text-muted-foreground uppercase tracking-wider w-16 text-center">
                    In-app
                  </span>
                  <span className="text-xs font-medium text-muted-foreground uppercase tracking-wider w-16 text-center">
                    Email
                  </span>
                </div>
              </div>
              <PreferenceRow
                label="Ticket created"
                inApp={prefs.ticketCreatedInApp}
                email={prefs.ticketCreatedEmail}
                onInAppChange={(v) => update({ ticketCreatedInApp: v })}
                onEmailChange={(v) => update({ ticketCreatedEmail: v })}
              />
              <PreferenceRow
                label="Ticket assigned to you"
                inApp={prefs.ticketAssignedInApp}
                email={prefs.ticketAssignedEmail}
                onInAppChange={(v) => update({ ticketAssignedInApp: v })}
                onEmailChange={(v) => update({ ticketAssignedEmail: v })}
              />
              <PreferenceRow
                label="Ticket unassigned"
                inApp={prefs.ticketUnassignedInApp}
                email={prefs.ticketUnassignedEmail}
                onInAppChange={(v) => update({ ticketUnassignedInApp: v })}
                onEmailChange={(v) => update({ ticketUnassignedEmail: v })}
              />
              <PreferenceRow
                label="Ticket status changed"
                inApp={prefs.ticketStatusChangedInApp}
                email={prefs.ticketStatusChangedEmail}
                onInAppChange={(v) => update({ ticketStatusChangedInApp: v })}
                onEmailChange={(v) => update({ ticketStatusChangedEmail: v })}
              />
              <PreferenceRow
                label="New comment on your ticket"
                inApp={prefs.ticketCommentedInApp}
                email={prefs.ticketCommentedEmail}
                onInAppChange={(v) => update({ ticketCommentedInApp: v })}
                onEmailChange={(v) => update({ ticketCommentedEmail: v })}
              />
              <div className="flex justify-end pt-4 border-t mt-2">
                <Button onClick={save} disabled={saving}>
                  {saving ? "Saving..." : "Save preferences"}
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
