namespace Proteone.Api.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public const int DefaultLoginPermitLimitPerMinute = 10;
    public const int DefaultSessionHours = 8;
    public const int DefaultMaxSessionHours = 24;
    public const int DefaultForgotPasswordPermitLimitPerMinute = 5;
    public const int DefaultSetPasswordPermitLimitPerMinute = 5;
    public const int DefaultMonitoringPermitLimitPerMinute = 60;

    public int LoginPermitLimitPerMinute { get; init; } = DefaultLoginPermitLimitPerMinute;

    public int SessionHours { get; init; } = DefaultSessionHours;

    /// <summary>A session ends this long after sign-in, also while it is used.</summary>
    public int MaxSessionHours { get; init; } = DefaultMaxSessionHours;

    /// <summary>Forgot-password requests per client IP per minute (spec §4: rate limited per IP).</summary>
    public int ForgotPasswordPermitLimitPerMinute { get; init; } = DefaultForgotPasswordPermitLimitPerMinute;

    /// <summary>Set-password requests per client IP per minute: each one hashes a password before the token lookup.</summary>
    public int SetPasswordPermitLimitPerMinute { get; init; } = DefaultSetPasswordPermitLimitPerMinute;

    /// <summary>Monitoring webhook requests per client IP per minute, counted before any check (ADR-0041).</summary>
    public int MonitoringPermitLimitPerMinute { get; init; } = DefaultMonitoringPermitLimitPerMinute;
}
