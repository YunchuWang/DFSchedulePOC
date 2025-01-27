// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;

public class ScheduleConfiguration
{
    public ScheduleConfiguration(string orchestrationName, string scheduleId)
    {
        if (string.IsNullOrEmpty(orchestrationName))
        {
            throw new ArgumentNullException(nameof(orchestrationName));
        }
        this.orchestrationName = orchestrationName;
        this.scheduleId = scheduleId ?? Guid.NewGuid().ToString("N");
        this.Version++;
    }

    private string orchestrationName = string.Empty;
    private string scheduleId = string.Empty;

    public string OrchestrationName
    {
        get => this.orchestrationName;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }
            this.orchestrationName = value;
        }
    }

    public string ScheduleId
    {
        get => this.scheduleId;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }
            this.scheduleId = value;
        }
    }

    public string? OrchestrationInput { get; set; }

    public string? OrchestrationInstanceId { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset? StartAt { get; set; }

    public DateTimeOffset? EndAt { get; set; }

    TimeSpan? interval;

    public TimeSpan? Interval
    {
        get => this.interval;
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            if (value.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException("Interval must be positive", nameof(value));
            }

            if (value.Value.TotalSeconds < 1)
            {
                throw new ArgumentException("Interval must be at least 1 second", nameof(value));
            }

            this.interval = value;
        }
    }

    public string? CronExpression { get; set; }

    public int MaxOccurrence { get; set; }

    public bool? StartImmediatelyIfLate { get; set; }

    internal int Version { get; set; } // Tracking schedule config version
}

enum ScheduleStatus
{
    Uninitialized, // Schedule has not been created
    Active,       // Schedule is active and running
    Paused,       // Schedule is paused
    Deleted      // Schedule has been deleted
}