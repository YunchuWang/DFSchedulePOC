using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[JsonObject(MemberSerialization.OptIn)]
public class Schedule : ISchedule
{
    [JsonProperty]
    public ScheduleState State { get; set; } = new ScheduleState();

    // Valid schedule status transitions
    [JsonProperty]
    private static readonly Dictionary<ScheduleStatus, HashSet<ScheduleStatus>> ValidTransitions = new()
    {
        { ScheduleStatus.Uninitialized, new HashSet<ScheduleStatus> { ScheduleStatus.Active } },
        { ScheduleStatus.Active, new HashSet<ScheduleStatus> { ScheduleStatus.Paused, ScheduleStatus.Deleted } },
        { ScheduleStatus.Paused, new HashSet<ScheduleStatus> { ScheduleStatus.Active, ScheduleStatus.Deleted } },
        { ScheduleStatus.Deleted, new HashSet<ScheduleStatus>() },
    };

    // Boilerplate (entry point for the functions runtime)
    [FunctionName(nameof(Schedule))]
    public static Task HandleEntityOperation([EntityTrigger] IDurableEntityContext context)
    {
        return context.DispatchAsync<Schedule>();
    }

    public void CreateSchedule(ScheduleConfiguration scheduleCreationConfig, ILogger logger)
    {
        if (State.Status != ScheduleStatus.Uninitialized)
        {
            throw new InvalidOperationException("Schedule is already created.");
        }

        logger.LogInformation($"Creating schedule with options: {scheduleCreationConfig}");

        State.ScheduleConfiguration = scheduleCreationConfig;
        TryStatusTransition(ScheduleStatus.Active);

        // Signal to run schedule immediately after creation
        Entity.Current.SignalEntity(new EntityId(nameof(RunSchedule), State.ScheduleConfiguration.ScheduleId), nameof(RunSchedule), State.ExecutionToken);
    }

    public void UpdateSchedule(ScheduleConfiguration scheduleUpdateConfig, ILogger logger)
    {
        if (State.ScheduleConfiguration == null)
        {
            throw new InvalidOperationException("Schedule configuration is not initialized.");
        }

        logger.LogInformation($"Updating schedule with details: {scheduleUpdateConfig}");

        HashSet<string> updatedFields = State.UpdateConfig(scheduleUpdateConfig);
        if (updatedFields.Count == 0)
        {
            logger.LogInformation("Schedule configuration is up to date.");
            return;
        }

        foreach (string updatedField in updatedFields)
        {
            switch (updatedField)
            {
                case nameof(ScheduleConfiguration.StartAt):
                case nameof(ScheduleConfiguration.Interval):
                    State.NextRunAt = null;
                    break;
            }
        }

        State.RefreshScheduleRunExecutionToken();

        // Signal to run schedule immediately after update
        Entity.Current.SignalEntity(new EntityId(nameof(RunSchedule), State.ScheduleConfiguration.ScheduleId), nameof(RunSchedule), State.ExecutionToken);
    }

    public void PauseSchedule(ILogger logger)
    {
        if (State.Status != ScheduleStatus.Active)
        {
            throw new InvalidOperationException("Schedule must be in Active status to pause.");
        }

        TryStatusTransition(ScheduleStatus.Paused);
        State.NextRunAt = null;
        State.RefreshScheduleRunExecutionToken();

        logger.LogInformation("Schedule paused.");
    }

    public void ResumeSchedule(ILogger logger)
    {
        if (State.ScheduleConfiguration == null)
        {
            throw new InvalidOperationException("Schedule configuration is not initialized.");
        }

        if (State.Status != ScheduleStatus.Paused)
        {
            throw new InvalidOperationException("Schedule must be in Paused state to resume.");
        }

        TryStatusTransition(ScheduleStatus.Active);
        State.NextRunAt = null;
        logger.LogInformation("Schedule resumed.");

        // Signal to run schedule immediately after resume
        Entity.Current.SignalEntity(new EntityId(nameof(RunSchedule), State.ScheduleConfiguration.ScheduleId), nameof(RunSchedule), State.ExecutionToken);
    }

    public void RunSchedule(string executionToken, ILogger logger)
    {
        if (State.ScheduleConfiguration == null || State.ScheduleConfiguration.Interval == null)
        {
            throw new InvalidOperationException("Schedule configuration or interval is not initialized.");
        }

        if (executionToken != State.ExecutionToken)
        {
            logger.LogInformation("Cancel schedule run - execution token {token} has expired", executionToken);
            return;
        }

        if (State.Status != ScheduleStatus.Active)
        {
            throw new InvalidOperationException("Schedule must be in Active status to run.");
        }

        if (!State.NextRunAt.HasValue)
        {
            if (!State.LastRunAt.HasValue)
            {
                State.NextRunAt = State.ScheduleConfiguration.StartAt;
            }
            else
            {
                TimeSpan timeSinceLastRun = DateTimeOffset.UtcNow - State.LastRunAt.Value;
                int intervalsElapsed = (int)(timeSinceLastRun.Ticks / State.ScheduleConfiguration.Interval.Value.Ticks);
                State.NextRunAt = State.LastRunAt.Value + TimeSpan.FromTicks(State.ScheduleConfiguration.Interval.Value.Ticks * (intervalsElapsed + 1));
            }
        }

        DateTimeOffset currentTime = DateTimeOffset.UtcNow;

        if (!State.NextRunAt.HasValue || State.NextRunAt.Value <= currentTime)
        {
            State.NextRunAt = currentTime;
            StartOrchestrationIfNotRunning();
            State.LastRunAt = State.NextRunAt;
            State.NextRunAt = State.LastRunAt.Value + State.ScheduleConfiguration.Interval.Value;
        }

        Entity.Current.SignalEntity(new EntityId(nameof(RunSchedule), State.ScheduleConfiguration.ScheduleId), State.ExecutionToken, State.NextRunAt.Value);
    }

    private void StartOrchestrationIfNotRunning()
    {
        ScheduleConfiguration config = State.ScheduleConfiguration!;
        Entity.Current.StartNewOrchestration(config.OrchestrationName, config.OrchestrationInput, config.OrchestrationInstanceId);
    }

    private void TryStatusTransition(ScheduleStatus to)
    {
        if (!ValidTransitions.TryGetValue(State.Status, out var validTargetStates) || !validTargetStates.Contains(to))
        {
            throw new InvalidOperationException($"Invalid state transition: Cannot transition from {State.Status} to {to}");
        }

        State.Status = to;
    }

    public void Delete(ILogger logger)
    {
        if (State.Status == ScheduleStatus.Deleted)
        {
            throw new InvalidOperationException("Schedule is already deleted.");
        }

        TryStatusTransition(ScheduleStatus.Deleted);
        State.NextRunAt = null;
        State.RefreshScheduleRunExecutionToken();

        logger.LogInformation("Schedule deleted.");
    }

    public void Delete()
    {
        Delete(null!);
    }

    public void CreateSchedule(ScheduleConfiguration scheduleCreationConfig)
    {
        CreateSchedule(scheduleCreationConfig, null!);
    }

    public void UpdateSchedule(ScheduleConfiguration scheduleUpdateConfig)
    {
        UpdateSchedule(scheduleUpdateConfig, null!);
    }

    public void PauseSchedule()
    {
        PauseSchedule(null!);
    }

    public void ResumeSchedule()
    {
        ResumeSchedule(null!);
    }

    public void RunSchedule(string executionToken)
    {
        RunSchedule(executionToken, null!);
    }
}