"use client"

import * as React from "react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useAuth } from "@/lib/auth"

export default function RegisterPage() {
  const router = useRouter()
  const { register, user, isLoading } = useAuth()
  const [email, setEmail] = React.useState("")
  const [password, setPassword] = React.useState("")
  const [fullName, setFullName] = React.useState("")
  const [error, setError] = React.useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({})
  const [submitting, setSubmitting] = React.useState(false)

  React.useEffect(() => {
    if (!isLoading && user) router.replace("/dashboard")
  }, [user, isLoading, router])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setFieldErrors({})
    setSubmitting(true)
    try {
      await register(email, password, fullName)
      router.push("/dashboard")
    } catch (err: any) {
      if (err?.errors) {
        setFieldErrors(err.errors)
      } else {
        setError(err?.message || "Registration failed")
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (isLoading || user) return null

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="w-full max-w-sm space-y-6">
        <div className="text-center">
          <h1 className="text-2xl font-bold tracking-tight">IT Service Desk</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Create your account
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {error}
            </div>
          )}

          <div className="space-y-1.5">
            <label htmlFor="fullName" className="text-sm font-medium">
              Full name
            </label>
            <input
              id="fullName"
              type="text"
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="John Doe"
              className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            />
            {fieldErrors.FullName && (
              <p className="text-xs text-destructive">{fieldErrors.FullName[0]}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <label htmlFor="email" className="text-sm font-medium">
              Email
            </label>
            <input
              id="email"
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="admin@example.com"
              className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            />
            {fieldErrors.Email && (
              <p className="text-xs text-destructive">{fieldErrors.Email[0]}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <label htmlFor="password" className="text-sm font-medium">
              Password
            </label>
            <input
              id="password"
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Admin123!@#"
              className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            />
            {fieldErrors.Password && (
              <ul className="text-xs text-destructive">
                {fieldErrors.Password.map((msg, i) => (
                  <li key={i}>{msg}</li>
                ))}
              </ul>
            )}
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="inline-flex h-9 w-full items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground shadow-sm transition-colors hover:bg-primary/90 disabled:pointer-events-none disabled:opacity-50"
          >
            {submitting ? "Creating account..." : "Create account"}
          </button>
        </form>

        <p className="text-center text-sm text-muted-foreground">
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-primary hover:underline">
            Sign in
          </Link>
        </p>
      </div>
    </div>
  )
}
