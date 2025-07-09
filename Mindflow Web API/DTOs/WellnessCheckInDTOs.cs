namespace Mindflow_Web_API.DTOs
{
    public record CreateWellnessCheckInDto(int StressLevel, int MoodLevel, string EnergyLevel, int SpiritualWellness);
    public record WellnessCheckInDto(Guid Id, Guid UserId, int StressLevel, int MoodLevel, string EnergyLevel, int SpiritualWellness, DateTime CheckInDate, DateTimeOffset Created, DateTimeOffset LastModified);
} 