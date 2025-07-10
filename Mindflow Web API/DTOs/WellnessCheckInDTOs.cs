namespace Mindflow_Web_API.DTOs
{
    public record CreateWellnessCheckInDto(int StressLevel, string MoodLevel, string EnergyLevel, int SpiritualWellness);
    public record WellnessCheckInDto(Guid Id, Guid UserId, int StressLevel, string MoodLevel, string EnergyLevel, int SpiritualWellness, DateTime CheckInDate, DateTimeOffset Created, DateTimeOffset LastModified);
} 