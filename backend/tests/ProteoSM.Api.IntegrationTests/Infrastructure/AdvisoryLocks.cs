using Microsoft.EntityFrameworkCore;

namespace ProteoSM.Api.IntegrationTests.Infrastructure;

/// <summary>For race tests with a fixed order: one transaction holds the lock, the call under test must wait for it.</summary>
internal static class AdvisoryLocks
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>Returns once the call waits on an advisory lock or on an uncommitted row of another transaction, or once it finished without waiting (the red case).</summary>
    public static async Task WaitUntilBlockedOrDoneAsync(ApiFactory factory, Task call, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + WaitTimeout;
        while (!call.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            await using var probe = factory.Database.CreateDbContext();
            var waiting = await probe.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype IN ('advisory', 'transactionid') AND NOT granted")
                .SingleAsync(cancellationToken);
            if (waiting > 0)
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        Assert.True(call.IsCompleted, "The call neither waited on the lock nor finished.");
    }
}
