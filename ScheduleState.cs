// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

public class ScheduleState
{
    internal ScheduleStatus Status { get; set; } = ScheduleStatus.Uninitialized;

    internal string ExecutionToken { get; set; } = Guid.NewGuid().ToString("N");

    internal DateTimeOffset? LastRunAt { get; set; }

    internal DateTimeOffset? NextRunAt { get; set; }

    internal ScheduleConfiguration? ScheduleConfiguration { get; set; }

    public HashSet<string> UpdateConfig(ScheduleConfiguration scheduleUpdateConfig)
    {
        if (this.ScheduleConfiguration == null)
        {
            throw new InvalidOperationException("Schedule configuration is not initialized.");
        }

        if (scheduleUpdateConfig == null)
        {
            throw new ArgumentNullException(nameof(scheduleUpdateConfig));
        }

        HashSet<string> updatedFields = new HashSet<string>();

        this.ScheduleConfiguration.Version++;

        if (!string.IsNullOrEmpty(scheduleUpdateConfig.OrchestrationName))
        {
            this.ScheduleConfiguration.OrchestrationName = scheduleUpdateConfig.OrchestrationName;
            updatedFields.Add(nameof(this.ScheduleConfiguration.OrchestrationName));
        }

        if (!string.IsNullOrEmpty(scheduleUpdateConfig.ScheduleId))
        {
            this.ScheduleConfiguration.ScheduleId = scheduleUpdateConfig.ScheduleId;
            updatedFields.Add(nameof(this.ScheduleConfiguration.ScheduleId));
        }

        if (scheduleUpdateConfig.OrchestrationInput == null)
        {
            this.ScheduleConfiguration.OrchestrationInput = scheduleUpdateConfig.OrchestrationInput;
            updatedFields.Add(nameof(this.ScheduleConfiguration.OrchestrationInput));
        }

        if (scheduleUpdateConfig.StartAt.HasValue)
        {
            this.ScheduleConfiguration.StartAt = scheduleUpdateConfig.StartAt;
            updatedFields.Add(nameof(this.ScheduleConfiguration.StartAt));
        }

        if (scheduleUpdateConfig.EndAt.HasValue)
        {
            this.ScheduleConfiguration.EndAt = scheduleUpdateConfig.EndAt;
            updatedFields.Add(nameof(this.ScheduleConfiguration.EndAt));
        }

        if (scheduleUpdateConfig.Interval.HasValue)
        {
            this.ScheduleConfiguration.Interval = scheduleUpdateConfig.Interval;
            updatedFields.Add(nameof(this.ScheduleConfiguration.Interval));
        }

        if (!string.IsNullOrEmpty(scheduleUpdateConfig.CronExpression))
        {
            this.ScheduleConfiguration.CronExpression = scheduleUpdateConfig.CronExpression;
            updatedFields.Add(nameof(this.ScheduleConfiguration.CronExpression));
        }

        if (scheduleUpdateConfig.MaxOccurrence != 0)
        {
            this.ScheduleConfiguration.MaxOccurrence = scheduleUpdateConfig.MaxOccurrence;
            updatedFields.Add(nameof(this.ScheduleConfiguration.MaxOccurrence));
        }

        // Only update if the customer explicitly set a value
        if (scheduleUpdateConfig.StartImmediatelyIfLate.HasValue)
        {
            this.ScheduleConfiguration.StartImmediatelyIfLate = scheduleUpdateConfig.StartImmediatelyIfLate.Value;
            updatedFields.Add(nameof(this.ScheduleConfiguration.StartImmediatelyIfLate));
        }

        return updatedFields;
    }

    // To stop potential runSchedule operation scheduled after the schedule update/pause, invalidate the execution token and let it exit gracefully
    // This could incur little overhead as ideally the runSchedule with old token should be killed immediately
    // but there is no support to cancel pending entity operations currently, can be a todo item
    public void RefreshScheduleRunExecutionToken()
    {
        this.ExecutionToken = Guid.NewGuid().ToString("N");
    }
}