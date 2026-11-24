using Proteone.Application.Features.Changes;
using Proteone.Application.Features.Tasks;
using Proteone.Domain.Changes;
using Proteone.Domain.Common;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Pilot;

/// <summary>Where a seeded change ends up: still open, closed with an outcome after the whole flow, or cancelled once planned.</summary>
internal enum PilotChangePlan
{
    Draft,
    PendingApproval,
    Scheduled,
    Successful,
    RolledBack,
    Failed,
    Cancelled,
}

/// <summary>Where a seeded work task ends up.</summary>
internal enum PilotTaskPlan
{
    Open,
    InProgress,
    Completed,
    Cancelled,
}

/// <summary>Changes (with windows that overlap on shared assets and services) and work tasks of the pilot seed.</summary>
internal sealed partial class PilotSeedSession
{
    // Windows lie around the real "now", so the calendar and "this week" queue have data: past, present and planned.
    private const int FirstWindowDayOffset = -20;
    private const int LastWindowDayOffset = 55;
    private const int WindowStartHourUtc = 15;
    private const int WindowStartHourSpan = 6;
    private static readonly (int Min, int Max) WindowDuration = (60, 6 * 60);

    // A few assets are touched by many changes; every third change reuses the previous window and asset to force a clash.
    private const int SharedAssetPool = 12;
    private const int ForcedOverlapEvery = 3;
    private static readonly TimeSpan ForcedOverlapShift = TimeSpan.FromMinutes(30);
    private const int SecondAssetPercent = 30;
    private const int VerificationTaskPercent = 40;

    // The change is written this long ago (minutes), the task this long ago.
    private static readonly (int Min, int Max) ChangeAge = (MinutesPerDay, 25 * MinutesPerDay);
    private static readonly (int Min, int Max) TaskAge = (MinutesPerDay, 60 * MinutesPerDay);
    private static readonly (int Min, int Max) TaskDueDayOffset = (-30, 21);
    private static readonly (int Min, int Max) TaskStepDelay = (30, 2 * MinutesPerDay);

    // Closed changes were written this long ago (minutes) and ran in a window this many days later, so the whole flow
    // lies in the past and inside the report's default period.
    private static readonly (int Min, int Max) ClosedChangeAge = (10 * MinutesPerDay, 28 * MinutesPerDay);
    private const int ClosedWindowDelayDays = 7;
    private static readonly (int Min, int Max) WorkStepDelay = (10, 60);
    private const int SuccessfulCausedIncidentPercent = 20;

    // One block of each cycle, mixed so every kind shows up early at any scale: mostly scheduled and open work, and per
    // block four successful, one rolled-back, one failed and one cancelled change.
    private static readonly PilotChangePlan[] ChangePlanBlock =
    [
        PilotChangePlan.Scheduled, PilotChangePlan.Successful, PilotChangePlan.PendingApproval, PilotChangePlan.Scheduled, PilotChangePlan.Draft,
        PilotChangePlan.Successful, PilotChangePlan.RolledBack, PilotChangePlan.Scheduled, PilotChangePlan.PendingApproval, PilotChangePlan.Successful,
        PilotChangePlan.Failed, PilotChangePlan.Scheduled, PilotChangePlan.Cancelled, PilotChangePlan.Successful, PilotChangePlan.Scheduled,
    ];

    private static readonly PilotTaskPlan[] TaskPlanBlock =
    [
        PilotTaskPlan.Open, PilotTaskPlan.InProgress, PilotTaskPlan.Completed, PilotTaskPlan.Open, PilotTaskPlan.Cancelled,
        PilotTaskPlan.Completed, PilotTaskPlan.Open, PilotTaskPlan.InProgress, PilotTaskPlan.Completed, PilotTaskPlan.Open,
    ];

    private DateTimeOffset _previousWindowStart;
    private Guid? _previousAssetId;

    private async Task SeedChangesAsync()
    {
        var count = PilotSeedVolumes.Scaled(PilotSeedVolumes.Changes, scale);
        var pool = _assetIds.Take(SharedAssetPool).ToList();
        for (var index = 0; index < count; index++)
        {
            await SeedChangeAsync(index, ChangePlanBlock[index % ChangePlanBlock.Length], pool);
        }
    }

    private async Task SeedChangeAsync(int index, PilotChangePlan plan, IReadOnlyList<Guid> assetPool)
    {
        var inIntake = index % 2 == 0;
        var teamId = inIntake ? _intakeTeamId : _fieldTeamId;
        var owner = Pick(inIntake ? _intakeMembers : _fieldMembers);
        var implemented = plan is PilotChangePlan.Successful or PilotChangePlan.RolledBack or PilotChangePlan.Failed;
        var age = implemented ? ClosedChangeAge : ChangeAge;
        var at = Clamp(realNow.AddMinutes(-_random.Next(age.Min, age.Max)));

        // A closed change ran in its own past window; the forced clashes stay among the open ones.
        var forceOverlap = !implemented && index > 0 && index % ForcedOverlapEvery == 0 && _previousAssetId is not null;
        var start = implemented ? PastWindowStart(at) : forceOverlap ? _previousWindowStart + ForcedOverlapShift : RandomWindowStart();
        var end = start.AddMinutes(_random.Next(WindowDuration.Min, WindowDuration.Max));
        var assets = new List<Guid> { forceOverlap ? _previousAssetId!.Value : Pick(assetPool) };
        if (_random.Next(Hundred) < SecondAssetPercent)
        {
            var second = Pick(assetPool);
            if (!assets.Contains(second))
            {
                assets.Add(second);
            }
        }

        if (!implemented)
        {
            _previousWindowStart = start;
            _previousAssetId = assets[0];
        }

        var draft = new ChangeDraft(
            $"{Pick(PilotSeedChangeData.ChangeTitles)} ({index + 1})", PilotSeedChangeData.ChangeJustification, Pick(PilotSeedChangeData.AffectedServices),
            Pick(Enum.GetValues<Level>()), Pick(Enum.GetValues<Level>()), start, end,
            PilotSeedChangeData.ImplementationPlan, PilotSeedChangeData.VerificationPlan, PilotSeedChangeData.BackoutPlan);
        var created = await ActAsync<CreateChangeHandler, ChangeDetail>(owner, at, (h, ct) => h.HandleAsync(new CreateChangeCommand(draft, teamId, owner, assets), ct));
        await MaybeSetChangeServiceAsync(index, created, owner, at);
        // A problem's fixing change is one still planned or one that worked, never a cancelled or backed-out one.
        if (inIntake && plan is not (PilotChangePlan.RolledBack or PilotChangePlan.Failed or PilotChangePlan.Cancelled))
        {
            _intakeChangeNumbers.Add(created.Number);
        }

        if (plan == PilotChangePlan.Draft)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        var implementationTask = await AddChangeTaskAsync(created.Number, ChangePhase.Implementation, PilotSeedChangeData.ImplementationTaskTitle, teamId, owner, at);
        TaskDetail? verificationTask = null;
        if (_random.Next(Hundred) < VerificationTaskPercent)
        {
            verificationTask = await AddChangeTaskAsync(created.Number, ChangePhase.Verification, PilotSeedChangeData.VerificationTaskTitle, teamId, owner, at);
        }

        var ready = (await ActAsync<GetChangeHandler, ChangeView>(owner, at, (h, ct) => h.HandleAsync(new GetChangeQuery(created.Number), ct))).Detail!;
        at = Later(at, TaskStepDelay);
        var submitted = await ActAsync<SubmitChangeHandler, ChangeDetail>(owner, at, (h, ct) => h.HandleAsync(new SubmitChangeCommand(ready.Number, ready.Version), ct));
        if (plan == PilotChangePlan.PendingApproval)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        var decision = new DecideChangeApprovalCommand(submitted.Number, ChangeApprovalDecision.Approve, submitted.Approval!.Version, null);
        await ActAsync<DecideChangeApprovalHandler, ChangeDetail>(Pick(_approvers), at, (h, ct) => h.HandleAsync(decision, ct));
        switch (plan)
        {
            case PilotChangePlan.Scheduled:
                return;
            case PilotChangePlan.Cancelled:
                at = Later(at, TaskStepDelay);
                var cancel = new CancelChangeCommand(submitted.Number, await ChangeVersionAsync(submitted.Number, owner, at), PilotSeedChangeData.ChangeCancelReason);
                await ActAsync<CancelChangeHandler, ChangeDetail>(owner, at, (h, ct) => h.HandleAsync(cancel, ct));
                return;
            default:
                await RunAndCloseAsync(new PilotChangeRun(submitted.Number, plan, teamId, owner, implementationTask, verificationTask), start);
                return;
        }
    }

    /// <summary>The planned work tasks of a change and who runs it; the owner does every step, as in the team.</summary>
    private sealed record PilotChangeRun(string Number, PilotChangePlan Plan, Guid TeamId, Guid Owner, TaskDetail ImplementationTask, TaskDetail? VerificationTask);

    /// <summary>
    /// Implements the change from its window start and closes it. Successful: Uygulama done, verified, closed. Rolled
    /// back: verification finds a fault, the backout runs. Failed: the backout starts while still implementing. A
    /// backed-out change (and some successful ones) is tied to an incident it caused, before it is closed.
    /// </summary>
    private async Task RunAndCloseAsync(PilotChangeRun run, DateTimeOffset windowStart)
    {
        var (number, owner) = (run.Number, run.Owner);
        var at = windowStart;
        await ChangeStepAsync<StartImplementationHandler>(number, owner, at, (h, version, ct) => h.HandleAsync(new StartImplementationCommand(number, version), ct));
        at = await RunTaskAsync(run.ImplementationTask.Number, owner, Later(at, WorkStepDelay));

        var verification = run.VerificationTask;
        if (run.Plan != PilotChangePlan.Failed)
        {
            at = Later(at, WorkStepDelay);
            await ChangeStepAsync<FinishImplementationHandler>(number, owner, at, (h, version, ct) => h.HandleAsync(new FinishImplementationCommand(number, version), ct));
            if (verification is not null && run.Plan == PilotChangePlan.Successful)
            {
                at = await RunTaskAsync(verification.Number, owner, Later(at, WorkStepDelay));
                verification = null;
            }
        }

        if (run.Plan != PilotChangePlan.Successful)
        {
            at = Later(at, WorkStepDelay);
            await ChangeStepAsync<StartBackoutHandler>(number, owner, at, (h, version, ct) => h.HandleAsync(new StartBackoutCommand(number, version), ct));
            if (verification is not null)
            {
                var task = await TaskAsync(verification.Number, owner, at);
                await ActAsync<CancelTaskHandler, TaskDetail>(owner, at, (h, ct) => h.HandleAsync(new CancelTaskCommand(task.Number, task.Version, PilotSeedChangeData.VerificationCancelReason), ct));
            }

            var backout = await AddChangeTaskAsync(number, ChangePhase.Backout, PilotSeedChangeData.BackoutTaskTitle, run.TeamId, owner, at);
            at = await RunTaskAsync(backout.Number, owner, Later(at, WorkStepDelay));
        }

        var caused = run.Plan != PilotChangePlan.Successful || _random.Next(Hundred) < SuccessfulCausedIncidentPercent;
        if (caused && _incidentNumbers.Count > 0)
        {
            at = Later(at, WorkStepDelay);
            var link = new AddChangeTicketCommand(number, Pick(_incidentNumbers), ChangeTicketLinkKind.Caused);
            await ActAsync<AddChangeTicketHandler, IReadOnlyList<LinkedTicketItem>>(owner, at, (h, ct) => h.HandleAsync(link, ct));
        }

        at = Later(at, WorkStepDelay);
        var outcome = run.Plan switch
        {
            PilotChangePlan.Successful => ChangeOutcome.Successful,
            PilotChangePlan.RolledBack => ChangeOutcome.RolledBack,
            _ => ChangeOutcome.Failed,
        };
        var successful = outcome == ChangeOutcome.Successful;
        string? verificationNote = outcome == ChangeOutcome.Failed ? null : successful ? PilotSeedChangeData.VerificationNote : PilotSeedChangeData.FaultFoundNote;
        var backoutNote = successful ? null : PilotSeedChangeData.BackoutNote;
        var closingNote = successful ? PilotSeedChangeData.ClosingNote : PilotSeedChangeData.FailureClosingNote;
        await ChangeStepAsync<CloseChangeHandler>(number, owner, at, (h, version, ct) =>
            h.HandleAsync(new CloseChangeCommand(number, version, outcome, verificationNote, backoutNote, closingNote, null), ct));
    }

    /// <summary>One change action on a fresh version: a task step touches the change, so it is read right before.</summary>
    private async Task ChangeStepAsync<THandler>(string number, Guid actorId, DateTimeOffset at, Func<THandler, uint, CancellationToken, Task<ChangeDetail>> call)
        where THandler : notnull
    {
        var version = await ChangeVersionAsync(number, actorId, at);
        await ActAsync<THandler, ChangeDetail>(actorId, at, (h, ct) => call(h, version, ct));
    }

    private async Task<uint> ChangeVersionAsync(string number, Guid viewer, DateTimeOffset at) =>
        (await ActAsync<GetChangeHandler, ChangeView>(viewer, at, (h, ct) => h.HandleAsync(new GetChangeQuery(number), ct))).Detail!.Version;

    private Task<TaskDetail> TaskAsync(string number, Guid viewer, DateTimeOffset at) =>
        ActAsync<GetTaskHandler, TaskDetail>(viewer, at, (h, ct) => h.HandleAsync(new GetTaskQuery(number), ct));

    /// <summary>Starts a change task and completes it a little later, as its assignee; returns when it was completed.</summary>
    private async Task<DateTimeOffset> RunTaskAsync(string number, Guid assignee, DateTimeOffset at)
    {
        var task = await TaskAsync(number, assignee, at);
        var started = await ActAsync<StartTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new StartTaskCommand(task.Number, task.Version), ct));
        var done = Later(at, WorkStepDelay);
        await ActAsync<CompleteTaskHandler, TaskDetail>(assignee, done, (h, ct) => h.HandleAsync(new CompleteTaskCommand(started.Number, started.Version, PilotSeedChangeData.TaskResultNote), ct));
        return done;
    }

    /// <summary>A window a week after the change was written (whole hours in the afternoon, UTC), so it lies in the past.</summary>
    private DateTimeOffset PastWindowStart(DateTimeOffset written) =>
        new DateTimeOffset(written.UtcDateTime.Date, TimeSpan.Zero)
            .AddDays(ClosedWindowDelayDays)
            .AddHours(WindowStartHourUtc + _random.Next(WindowStartHourSpan));

    private Task<TaskDetail> AddChangeTaskAsync(string changeNumber, ChangePhase phase, string title, Guid teamId, Guid owner, DateTimeOffset at)
    {
        var command = new CreateTaskCommand(title, null, teamId, owner, Priority.P3, null, null, changeNumber, phase);
        return ActAsync<CreateTaskHandler, TaskDetail>(owner, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private DateTimeOffset RandomWindowStart() =>
        new DateTimeOffset(realNow.UtcDateTime.Date, TimeSpan.Zero)
            .AddDays(_random.Next(FirstWindowDayOffset, LastWindowDayOffset))
            .AddHours(WindowStartHourUtc + _random.Next(WindowStartHourSpan));

    private async Task SeedTasksAsync()
    {
        var count = PilotSeedVolumes.Scaled(PilotSeedVolumes.StandaloneTasks, scale);
        for (var index = 0; index < count; index++)
        {
            await SeedTaskAsync(index, TaskPlanBlock[index % TaskPlanBlock.Length]);
        }
    }

    private async Task SeedTaskAsync(int index, PilotTaskPlan plan)
    {
        var inIntake = index % 2 == 0;
        var teamId = inIntake ? _intakeTeamId : _fieldTeamId;
        var members = inIntake ? _intakeMembers : _fieldMembers;
        var creator = Pick(members);
        var assignee = Pick(members);
        var at = Clamp(realNow.AddMinutes(-_random.Next(TaskAge.Min, TaskAge.Max)));
        var due = DateOnly.FromDateTime(realNow.UtcDateTime).AddDays(_random.Next(TaskDueDayOffset.Min, TaskDueDayOffset.Max));
        var command = new CreateTaskCommand(
            Pick(PilotSeedChangeData.TaskTitles), PilotSeedChangeData.TaskDescription, teamId, assignee, Pick(Enum.GetValues<Priority>()), due, null);
        var task = await ActAsync<CreateTaskHandler, TaskDetail>(creator, at, (h, ct) => h.HandleAsync(command, ct));
        if (plan == PilotTaskPlan.Open)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        if (plan == PilotTaskPlan.Cancelled)
        {
            await ActAsync<CancelTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new CancelTaskCommand(task.Number, task.Version, PilotSeedChangeData.TaskCancelReason), ct));
            return;
        }

        var started = await ActAsync<StartTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new StartTaskCommand(task.Number, task.Version), ct));
        if (plan == PilotTaskPlan.InProgress)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        await ActAsync<CompleteTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new CompleteTaskCommand(started.Number, started.Version, PilotSeedChangeData.TaskResultNote), ct));
    }
}
