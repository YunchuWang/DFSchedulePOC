using Microsoft.Extensions.Logging;

public interface ISchedule
{
    void CreateSchedule(ScheduleConfiguration scheduleCreationConfig);
    void UpdateSchedule(ScheduleConfiguration scheduleUpdateConfig);
    void PauseSchedule();
    void ResumeSchedule();
    void RunSchedule(string executionToken);
    // void Delete();
}