using Microsoft.EntityFrameworkCore;
using Mindflow_Web_API.DTOs;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Persistence;

namespace Mindflow_Web_API.Services
{
    public class WellnessCheckInService : IWellnessCheckInService
    {
        private readonly MindflowDbContext _dbContext;
        private readonly ILogger<WellnessCheckInService> _logger;

        public WellnessCheckInService(MindflowDbContext dbContext, ILogger<WellnessCheckInService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<WellnessCheckInDto> SubmitAsync(Guid userId, CreateWellnessCheckInDto command)
        {
            var checkIn = WellnessCheckIn.Create(
                userId,
                command.StressLevel,
                command.MoodLevel,
                command.EnergyLevel,
                command.SpiritualWellness,
                DateTime.UtcNow
            );

            await _dbContext.WellnessCheckIns.AddAsync(checkIn);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Wellness check-in submitted for user: {userId}");

            return new WellnessCheckInDto(
                checkIn.Id,
                checkIn.UserId,
                checkIn.StressLevel,
                checkIn.MoodLevel,
                checkIn.EnergyLevel,
                checkIn.SpiritualWellness,
                checkIn.CheckInDate,
                checkIn.Created,
                checkIn.LastModified
            );
        }

        public async Task<WellnessCheckInDto?> GetAsync(Guid userId)
        {
            var checkIn = await _dbContext.WellnessCheckIns
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CheckInDate)
                .FirstOrDefaultAsync();

            if (checkIn == null)
                return null;

            return new WellnessCheckInDto(
                checkIn.Id,
                checkIn.UserId,
                checkIn.StressLevel,
                checkIn.MoodLevel,
                checkIn.EnergyLevel,
                checkIn.SpiritualWellness,
                checkIn.CheckInDate,
                checkIn.Created,
                checkIn.LastModified
            );
        }
    }
} 