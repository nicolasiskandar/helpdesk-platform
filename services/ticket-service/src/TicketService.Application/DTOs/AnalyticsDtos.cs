namespace TicketService.Application.DTOs;

public record AnalyticsResponse(
    AnalyticsOverview Overview,
    IReadOnlyList<MonthlyVolumeEntry> VolumeTrend,
    IReadOnlyList<MonthlyResolutionEntry> ResolutionTrend
);

public record AnalyticsOverview(
    int Total,
    int Open,
    int InProgress,
    int Pending,
    int Resolved,
    int CriticalOpen,
    int Unassigned,
    double? ResolutionRate,
    double? AverageResolutionHours,
    double? SlaCompliance
);

public record MonthlyVolumeEntry(
    string Month,
    int Created,
    int Resolved
);

public record MonthlyResolutionEntry(
    string Month,
    double? AverageHours
);
