using System;
using System.Collections.Generic;

namespace Mindflow_Web_API.DTOs
{
    // DTO for creating a wellness check-in
    // Core fields are fixed, dynamic questions go in Questions dictionary
    public record CreateWellnessCheckInDto(
        string MoodLevel, 
        bool ReminderEnabled, 
        string? ReminderTime, 
        string? AgeRange, 
        string[]? FocusAreas, 
        string? WeekdayStartTime, 
        string? WeekdayStartShift, 
        string? WeekdayEndTime, 
        string? WeekdayEndShift, 
        string? WeekendStartTime, 
        string? WeekendStartShift, 
        string? WeekendEndTime, 
        string? WeekendEndShift,
        Dictionary<string, object>? Questions  // Dynamic questions based on focus areas
    );
    
    // DTO for wellness check-in response
    public record WellnessCheckInDto(
        Guid Id, 
        Guid UserId, 
        string MoodLevel, 
        DateTime CheckInDate, 
        DateTimeOffset Created, 
        DateTimeOffset LastModified, 
        bool ReminderEnabled, 
        string? ReminderTime, 
        string? AgeRange, 
        string[]? FocusAreas, 
        string? WeekdayStartTime, 
        string? WeekdayStartShift, 
        string? WeekdayEndTime, 
        string? WeekdayEndShift, 
        string? WeekendStartTime, 
        string? WeekendStartShift, 
        string? WeekendEndTime, 
        string? WeekendEndShift,
        string? WeekdayStartTimeUtc,  // UTC time in 24-hour format
        string? WeekdayEndTimeUtc,    // UTC time in 24-hour format
        string? WeekendStartTimeUtc,  // UTC time in 24-hour format
        string? WeekendEndTimeUtc,    // UTC time in 24-hour format
        Dictionary<string, object> Questions  // Dynamic questions
    );
    
    // DTO for patching/updating wellness check-in
    // Questions dictionary will be merged with existing questions
    public record PatchWellnessCheckInDto(
        string? MoodLevel, 
        bool? ReminderEnabled, 
        string? ReminderTime, 
        string? AgeRange, 
        string[]? FocusAreas, 
        string? WeekdayStartTime, 
        string? WeekdayStartShift, 
        string? WeekdayEndTime, 
        string? WeekdayEndShift, 
        string? WeekendStartTime, 
        string? WeekendStartShift, 
        string? WeekendEndTime, 
        string? WeekendEndShift,
        Dictionary<string, object>? Questions  // Dynamic questions to merge
    );
} 