import * as XLSX from "xlsx"
import { jsPDF } from "jspdf"
import autoTable from "jspdf-autotable"
import type { AnalyticsResponse } from "./api"

export interface AgentPerformanceRow {
  id: string
  name: string
  assigned: number
  resolved: number
  active: number
}

function fmtPct(value: number | null | undefined): string {
  return value == null ? "—" : `${value}%`
}

function fmtHours(value: number | null | undefined): string {
  return value == null ? "—" : `${value}h`
}

export function exportReportExcel(
  stats: AnalyticsResponse,
  performance: AgentPerformanceRow[]
) {
  const wb = XLSX.utils.book_new()

  const o = stats.overview
  const overviewSheet = XLSX.utils.json_to_sheet([
    { Metric: "Total Tickets", Value: o.total },
    { Metric: "Open", Value: o.open },
    { Metric: "In Progress", Value: o.inProgress },
    { Metric: "Pending Resolution", Value: o.pending },
    { Metric: "Resolved", Value: o.resolved },
    { Metric: "Critical Open", Value: o.criticalOpen },
    { Metric: "Unassigned", Value: o.unassigned },
    { Metric: "Resolution Rate", Value: fmtPct(o.resolutionRate) },
    { Metric: "Avg. Resolution Time", Value: fmtHours(o.averageResolutionHours) },
    { Metric: "SLA Compliance", Value: fmtPct(o.slaCompliance) },
  ])
  XLSX.utils.book_append_sheet(wb, overviewSheet, "Overview")

  const volumeSheet = XLSX.utils.json_to_sheet(
    stats.volumeTrend.map((v) => ({ Month: v.month, Created: v.created, Resolved: v.resolved }))
  )
  XLSX.utils.book_append_sheet(wb, volumeSheet, "Ticket Volume")

  const resolutionSheet = XLSX.utils.json_to_sheet(
    stats.resolutionTrend.map((r) => ({ Month: r.month, "Avg. Resolution (hrs)": fmtHours(r.averageHours) }))
  )
  XLSX.utils.book_append_sheet(wb, resolutionSheet, "Resolution Time")

  const performanceSheet = XLSX.utils.json_to_sheet(
    performance.map((a) => ({
      Agent: a.name,
      Assigned: a.assigned,
      Resolved: a.resolved,
      Active: a.active,
      "Resolution Rate": a.assigned > 0 ? `${Math.round((a.resolved / a.assigned) * 100)}%` : "0%",
    }))
  )
  XLSX.utils.book_append_sheet(wb, performanceSheet, "Agent Performance")

  XLSX.writeFile(wb, "helpdesk-report.xlsx")
}

export function exportReportPdf(
  stats: AnalyticsResponse,
  performance: AgentPerformanceRow[]
) {
  const doc = new jsPDF()
  const pageWidth = doc.internal.pageSize.getWidth()

  doc.setFontSize(16)
  doc.text("Helpdesk Reports & Analytics", 14, 18)
  doc.setFontSize(10)
  doc.setTextColor(100)
  doc.text("Performance metrics for the last 6 months", 14, 25)
  doc.setTextColor(0)

  const o = stats.overview
  const overviewRows = [
    ["Total Tickets", String(o.total)],
    ["Open", String(o.open)],
    ["In Progress", String(o.inProgress)],
    ["Pending Resolution", String(o.pending)],
    ["Resolved", String(o.resolved)],
    ["Critical Open", String(o.criticalOpen)],
    ["Unassigned", String(o.unassigned)],
    ["Resolution Rate", fmtPct(o.resolutionRate)],
    ["Avg. Resolution Time", fmtHours(o.averageResolutionHours)],
    ["SLA Compliance", fmtPct(o.slaCompliance)],
  ]
  autoTable(doc, {
    startY: 32,
    head: [["Metric", "Value"]],
    body: overviewRows,
    theme: "grid",
    headStyles: { fillColor: [15, 23, 42] },
  })

  const volumeRows = stats.volumeTrend.map((v) => [v.month, String(v.created), String(v.resolved)])
  autoTable(doc, {
    startY: (doc as any).lastAutoTable.finalY + 10,
    head: [["Month", "Created", "Resolved"]],
    body: volumeRows,
    theme: "grid",
    headStyles: { fillColor: [15, 23, 42] },
  })

  const resolutionRows = stats.resolutionTrend.map((r) => [r.month, fmtHours(r.averageHours)])
  autoTable(doc, {
    startY: (doc as any).lastAutoTable.finalY + 10,
    head: [["Month", "Avg. Resolution (hrs)"]],
    body: resolutionRows,
    theme: "grid",
    headStyles: { fillColor: [15, 23, 42] },
  })

  const performanceRows = performance.map((a) => [
    a.name,
    String(a.assigned),
    String(a.resolved),
    String(a.active),
    a.assigned > 0 ? `${Math.round((a.resolved / a.assigned) * 100)}%` : "0%",
  ])
  autoTable(doc, {
    startY: (doc as any).lastAutoTable.finalY + 10,
    head: [["Agent", "Assigned", "Resolved", "Active", "Resolution Rate"]],
    body: performanceRows,
    theme: "grid",
    headStyles: { fillColor: [15, 23, 42] },
    didDrawPage: () => {},
  })

  const finalY = (doc as any).lastAutoTable?.finalY ?? pageWidth
  doc.setFontSize(9)
  doc.setTextColor(130)
  doc.text(`Generated ${new Date().toLocaleString()}`, 14, finalY + 12)
  doc.setTextColor(0)

  doc.save("helpdesk-report.pdf")
}
