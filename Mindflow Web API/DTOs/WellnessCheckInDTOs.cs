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
        DateTime? WeekdayStartTimeUtc,  // UTC time as DateTime
        DateTime? WeekdayEndTimeUtc,    // UTC time as DateTime
        DateTime? WeekendStartTimeUtc,  // UTC time as DateTime
        DateTime? WeekendEndTimeUtc,    // UTC time as DateTime
        int? WeekdayStartMinutesUtc,   // Minutes from UTC midnight (0-1439 for same day, can be > 1440 if crosses to next day)
        int? WeekdayEndMinutesUtc,     // Minutes from UTC midnight (can be > 1440 if crosses to next day)
        int? WeekendStartMinutesUtc,   // Minutes from UTC midnight
        int? WeekendEndMinutesUtc,     // Minutes from UTC midnight (can be > 1440 if crosses to next day)
        string? TimezoneId,
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
        Dictionary<string, object>? Questions,  // Dynamic questions to merge
        string? TimezoneId  // IANA timezone ID (e.g., "America/Chicago", "America/New_York"). If null, assumes times are already in UTC.
    );
} 