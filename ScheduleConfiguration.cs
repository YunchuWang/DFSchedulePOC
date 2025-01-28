// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;

[JsonObject(MemberSerialization.OptIn)]
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

    [JsonProperty]
    private string orchestrationName = string.Empty;

    [JsonProperty]
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

    [JsonProperty]
    public string? OrchestrationInput { get; set; }

    [JsonProperty]
    public string? OrchestrationInstanceId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonProperty]
    public DateTimeOffset? StartAt { get; set; }

    [JsonProperty]
    public DateTimeOffset? EndAt { get; set; }

    [JsonProperty]
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

    [JsonProperty]
    public string? CronExpression { get; set; }

    [JsonProperty]
    public int MaxOccurrence { get; set; }

    [JsonProperty]
    public bool? StartImmediatelyIfLate { get; set; }

    [JsonProperty]
    internal int Version { get; set; } // Tracking schedule config version
}

enum ScheduleStatus
{
    Uninitialized, // Schedule has not been created
    Active,       // Schedule is active and running
    Paused,       // Schedule is paused
    Deleted      // Schedule has been deleted
}