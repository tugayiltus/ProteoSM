using ProteoSM.Application.Common;

namespace ProteoSM.Api.IntegrationTests.Reports;

/// <summary>Company clock fixed to Istanbul for report tests.</summary>
public sealed class TestClock : ICompanyClock
{
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public string Format(DateTimeOffset instant) => instant.ToString("O");
}
