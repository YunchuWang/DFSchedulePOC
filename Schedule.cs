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
    private readonly ILogger logger;

    public Schedule(ILogger logger)
    {
        this.logger = logger;
    }

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
    public static Task HandleEntityOperation([EntityTrigger] IDurableEntityContext context, ILogger logger)
    {
        return context.DispatchAsync<Schedule>(logger);
    }

    public void CreateSchedule(ScheduleConfiguration scheduleCreationConfig)
    {
        if (State.Status != ScheduleStatus.Uninitialized)
        {
            throw new InvalidOperationException("Schedule is already created.");
        }

        State.ScheduleConfiguration = scheduleCreationConfig;
        TryStatusTransition(ScheduleStatus.Active);

        // Signal to run schedule immediately after creation
        Entity.Current.SignalEntity(Entity.Current.EntityId, nameof(RunSchedule), State.ExecutionToken);

        logger.LogInformation("Schedule being created. State: {State}", JsonConvert.SerializeObject(State));
    }

    public void UpdateSchedule(ScheduleConfiguration scheduleUpdateConfig)
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
        Entity.Current.SignalEntity(Entity.Current.EntityId, nameof(RunSchedule), State.ExecutionToken);
    }

    public void PauseSchedule()
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

    public void ResumeSchedule()
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
        Entity.Current.SignalEntity(Entity.Current.EntityId, nameof(RunSchedule), State.ExecutionToken);
    }

    public void RunSchedule(string executionToken)
    {
        if (State.ScheduleConfiguration == null || State.ScheduleConfiguration.Interval == null)
        {
            throw new InvalidOperationException("Schedule configuration or interval is not initialized.");
        }

        if (executionToken != State.ExecutionToken)
        {
            Console.WriteLine("Execution token has expired.");
            return;
        }

        if (State.Status != ScheduleStatus.Active)
        {
            throw new InvalidOperationException("Schedule must be in Active status to run.");
        }

        // log state before running
        logger.LogInformation("Schedule running. State: {State}", JsonConvert.SerializeObject(State));

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

        Entity.Current.SignalEntity(Entity.Current.EntityId, State.NextRunAt.Value.UtcDateTime, nameof(RunSchedule), State.ExecutionToken);
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

    // public void Delete()
    // {
    //     if (State.Status == ScheduleStatus.Deleted)
    //     {
    //         throw new InvalidOperationException("Schedule is already deleted.");
    //     }

    //     TryStatusTransition(ScheduleStatus.Deleted);
    //     State.NextRunAt = null;
    //     State.RefreshScheduleRunExecutionToken();
    //     Entity.Current.SetState(null);
    //     logger.LogInformation("Schedule deleted.");
    // }
}