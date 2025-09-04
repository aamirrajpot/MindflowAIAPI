using Mindflow_Web_API.Services;
using Mindflow_Web_API.Models;
using Mindflow_Web_API.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Mindflow_Web_API.EndPoints
{
    public static class RunPodEndpoints
    {
        public static void MapRunPodEndpoints(this IEndpointRouteBuilder app)
        {
            var runpodApi = app.MapGroup("/api/runpod").WithTags("RunPod AI");

            bool IsAdmin(HttpContext ctx)
            {
                var roleClaim = ctx.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role");
                return roleClaim != null && string.Equals(roleClaim.Value, "Admin", StringComparison.OrdinalIgnoreCase);
            }

            // Health check endpoint (public)
            runpodApi.MapGet("/health", () =>
            {
                return Results.Ok(new
                {
                    service = "RunPod AI Service",
                    status = "Healthy",
                    timestamp = DateTime.UtcNow,
                    features = new[]
                    {
                        "Wellness Analysis",
                        "Task Suggestions",
                        "Urgency Assessment",
                        "Custom Prompts"
                    }
                });
            })
            .WithOpenApi(op =>
            {
                op.Summary = "RunPod service health check";
                op.Description = "Returns the health status of the RunPod AI service";
                return op;
            });

            // Wellness Analysis endpoint
            runpodApi.MapPost("/analyze-wellness", async (
                IRunPodService runPodService,
                IWellnessCheckInService wellnessService,
                HttpContext context,
                [FromQuery] int maxTokens = 1000,
                [FromQuery] double temperature = 0.7) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                // Fetch the wellness check-in from database
                var checkInDto = await wellnessService.GetAsync(userId);
                if (checkInDto == null)
                    throw ApiExceptions.NotFound("Wellness check-in not found. Please complete a wellness check-in first.");

                // Convert DTO to model
                var checkIn = WellnessCheckIn.Create(
                    checkInDto.UserId,
                    checkInDto.MoodLevel,
                    checkInDto.CheckInDate,
                    checkInDto.ReminderEnabled,
                    checkInDto.ReminderTime,
                    checkInDto.AgeRange,
                    checkInDto.FocusAreas,
                    checkInDto.StressNotes,
                    checkInDto.ThoughtTrackingMethod,
                    checkInDto.SupportAreas,
                    checkInDto.SelfCareFrequency,
                    checkInDto.ToughDayMessage,
                    checkInDto.CopingMechanisms,
                    checkInDto.JoyPeaceSources,
                    checkInDto.WeekdayStartTime,
                    checkInDto.WeekdayStartShift,
                    checkInDto.WeekdayEndTime,
                    checkInDto.WeekdayEndShift,
                    checkInDto.WeekendStartTime,
                    checkInDto.WeekendStartShift,
                    checkInDto.WeekendEndTime,
                    checkInDto.WeekendEndShift
                );

                var result = await runPodService.AnalyzeWellnessAsync(checkIn, maxTokens, temperature);
                return Results.Ok(result);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Analyze wellness check-in with RunPod AI";
                op.Description = "Analyzes wellness check-in data using RunPod AI to provide personalized insights and suggestions";
                return op;
            });

            // Task Suggestions endpoint
            runpodApi.MapPost("/task-suggestions", async (
                IRunPodService runPodService,
                IWellnessCheckInService wellnessService,
                HttpContext context,
                [FromQuery] int maxTokens = 1000,
                [FromQuery] double temperature = 0.7) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                // Fetch the wellness check-in from database
                var checkInDto = await wellnessService.GetAsync(userId);
                if (checkInDto == null)
                    throw ApiExceptions.NotFound("Wellness check-in not found. Please complete a wellness check-in first.");

                // Convert DTO to model
                var checkIn = WellnessCheckIn.Create(
                    checkInDto.UserId,
                    checkInDto.MoodLevel,
                    checkInDto.CheckInDate,
                    checkInDto.ReminderEnabled,
                    checkInDto.ReminderTime,
                    checkInDto.AgeRange,
                    checkInDto.FocusAreas,
                    checkInDto.StressNotes,
                    checkInDto.ThoughtTrackingMethod,
                    checkInDto.SupportAreas,
                    checkInDto.SelfCareFrequency,
                    checkInDto.ToughDayMessage,
                    checkInDto.CopingMechanisms,
                    checkInDto.JoyPeaceSources,
                    checkInDto.WeekdayStartTime,
                    checkInDto.WeekdayStartShift,
                    checkInDto.WeekdayEndTime,
                    checkInDto.WeekdayEndShift,
                    checkInDto.WeekendStartTime,
                    checkInDto.WeekendStartShift,
                    checkInDto.WeekendEndTime,
                    checkInDto.WeekendEndShift
                );

                var tasks = await runPodService.GetTaskSuggestionsAsync(checkIn, maxTokens, temperature);
                return Results.Ok(tasks);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Get personalized task suggestions";
                op.Description = "Generates personalized task suggestions based on wellness check-in data using RunPod AI";
                return op;
            });

            // Urgency Assessment endpoint
            runpodApi.MapPost("/assess-urgency", async (
                IRunPodService runPodService,
                IWellnessCheckInService wellnessService,
                HttpContext context,
                [FromQuery] int maxTokens = 1000,
                [FromQuery] double temperature = 0.7) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    throw ApiExceptions.Unauthorized("Invalid user token");

                // Fetch the wellness check-in from database
                var checkInDto = await wellnessService.GetAsync(userId);
                if (checkInDto == null)
                    throw ApiExceptions.NotFound("Wellness check-in not found. Please complete a wellness check-in first.");

                // Convert DTO to model
                var checkIn = WellnessCheckIn.Create(
                    checkInDto.UserId,
                    checkInDto.MoodLevel,
                    checkInDto.CheckInDate,
                    checkInDto.ReminderEnabled,
                    checkInDto.ReminderTime,
                    checkInDto.AgeRange,
                    checkInDto.FocusAreas,
                    checkInDto.StressNotes,
                    checkInDto.ThoughtTrackingMethod,
                    checkInDto.SupportAreas,
                    checkInDto.SelfCareFrequency,
                    checkInDto.ToughDayMessage,
                    checkInDto.CopingMechanisms,
                    checkInDto.JoyPeaceSources,
                    checkInDto.WeekdayStartTime,
                    checkInDto.WeekdayStartShift,
                    checkInDto.WeekdayEndTime,
                    checkInDto.WeekdayEndShift,
                    checkInDto.WeekendStartTime,
                    checkInDto.WeekendStartShift,
                    checkInDto.WeekendEndTime,
                    checkInDto.WeekendEndShift
                );

                var assessment = await runPodService.AssessUrgencyAsync(checkIn, maxTokens, temperature);
                return Results.Ok(assessment);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Assess urgency level";
                op.Description = "Assesses the urgency level of a wellness check-in using RunPod AI";
                return op;
            });

            // Custom Prompt endpoint
            runpodApi.MapPost("/custom-prompt", async (
                CustomPromptRequest request,
                IRunPodService runPodService,
                HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                var response = await runPodService.SendPromptAsync(
                    request.Prompt,
                    request.MaxTokens,
                    request.Temperature);

                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Send custom prompt to RunPod AI";
                op.Description = "Sends a custom prompt to RunPod AI and returns the response";
                return op;
            });

            // Example: Wellness Analysis with sample data
            runpodApi.MapPost("/analyze-wellness-sample", async (
                IRunPodService runPodService,
                HttpContext context,
                [FromQuery] int maxTokens = 1500,
                [FromQuery] double temperature = 0.8) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                // Create a sample wellness check-in for demonstration
                var sampleCheckIn = WellnessCheckIn.Create(
                    userId: Guid.NewGuid(),
                    mood: "Stressed",
                    checkInDate: DateTime.UtcNow,
                    ageRange: "25-34",
                    focusAreas: new[] { "Mental health", "Productivity", "Career/School" },
                    supportAreas: new[] { "Brain dump & organize my thoughts", "Feel more in control" },
                    stressNotes: "Feeling overwhelmed with project deadlines and team conflicts",
                    selfCareFrequency: "Rarely",
                    copingMechanisms: new[] { "Deep breathing", "Exercise / Walking", "Talking to someone" },
                    joyPeaceSources: "Reading, Nature walks, Music",
                    toughDayMessage: "I'm trying my best but it never feels like enough"
                );

                var result = await runPodService.AnalyzeWellnessAsync(sampleCheckIn, maxTokens, temperature);
                return Results.Ok(new
                {
                    message = "Sample wellness analysis completed",
                    sampleData = sampleCheckIn,
                    result = result
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Example: Analyze wellness with sample data";
                op.Description = "Demonstrates wellness analysis using sample wellness check-in data";
                return op;
            });

            // Example: Task Suggestions with sample data
            runpodApi.MapPost("/task-suggestions-sample", async (
                IRunPodService runPodService,
                HttpContext context,
                [FromQuery] int maxTokens = 1000,
                [FromQuery] double temperature = 0.7) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                // Create a sample wellness check-in for demonstration
                var sampleCheckIn = WellnessCheckIn.Create(
                    userId: Guid.NewGuid(),
                    mood: "Stressed",
                    checkInDate: DateTime.UtcNow,
                    ageRange: "18-24",
                    focusAreas: new[] { "Career/School", "Mental health" },
                    supportAreas: new[] { "Make sense of what I'm feeling", "Get help with decisions" },
                    stressNotes: "Upcoming exams and feeling isolated from peers",
                    selfCareFrequency: "Sometimes",
                    copingMechanisms: new[] { "Journaling", "Exercise / Walking", "Music" },
                    joyPeaceSources: "Art, Music, Nature"
                );

                var tasks = await runPodService.GetTaskSuggestionsAsync(sampleCheckIn, maxTokens, temperature);
                return Results.Ok(new
                {
                    message = "Sample task suggestions generated",
                    sampleData = sampleCheckIn,
                    tasks = tasks
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Example: Get task suggestions with sample data";
                op.Description = "Demonstrates task suggestion generation using sample wellness check-in data";
                return op;
            });

            // Example: Urgency Assessment with sample data
            runpodApi.MapPost("/assess-urgency-sample", async (
                IRunPodService runPodService,
                HttpContext context,
                [FromQuery] int maxTokens = 800,
                [FromQuery] double temperature = 0.6) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                // Create a sample wellness check-in for demonstration
                var sampleCheckIn = WellnessCheckIn.Create(
                    userId: Guid.NewGuid(),
                    mood: "Overwhelmed",
                    checkInDate: DateTime.UtcNow,
                    ageRange: "35-44",
                    focusAreas: new[] { "Mental health", "Relationships" },
                    supportAreas: new[] { "Daily mental health check-ins", "Reminders to care for myself" },
                    stressNotes: "Feeling hopeless and considering drastic changes",
                    copingMechanisms: new[] { "Not sure yet" },
                    joyPeaceSources: "Still searching for sources of joy",
                    toughDayMessage: "I don't know how much longer I can keep going"
                );

                var assessment = await runPodService.AssessUrgencyAsync(sampleCheckIn, maxTokens, temperature);
                return Results.Ok(new
                {
                    message = "Sample urgency assessment completed",
                    sampleData = sampleCheckIn,
                    assessment = assessment
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Example: Assess urgency with sample data";
                op.Description = "Demonstrates urgency assessment using sample wellness check-in data";
                return op;
            });

            // Example: Custom Prompt with sample data
            runpodApi.MapPost("/custom-prompt-sample", async (
                IRunPodService runPodService,
                HttpContext context,
                [FromQuery] int maxTokens = 500,
                [FromQuery] double temperature = 0.9) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                if (!IsAdmin(context))
                    throw ApiExceptions.Forbidden("Admin access required");

                var samplePrompt = "[INST] You are a wellness coach. Please provide 3 quick tips for managing stress during a busy workday. Keep each tip under 2 sentences. [/INST]";

                var response = await runPodService.SendPromptAsync(samplePrompt, maxTokens, temperature);
                return Results.Ok(new
                {
                    message = "Sample custom prompt completed",
                    samplePrompt = samplePrompt,
                    response = response
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Example: Send custom prompt with sample data";
                op.Description = "Demonstrates custom prompt functionality using a sample wellness coaching prompt";
                return op;
            });

            // Example: Batch processing demonstration
            runpodApi.MapPost("/batch-processing-sample", async (
                IRunPodService runPodService,
                HttpContext context) =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? true)
                    throw ApiExceptions.Unauthorized("User is not authenticated");

                // Create multiple sample wellness check-ins
                var sampleCheckIns = new[]
                {
                                            WellnessCheckIn.Create(
                            userId: Guid.NewGuid(),
                            mood: "Okay",
                            checkInDate: DateTime.UtcNow,
                            ageRange: "25-34",
                            focusAreas: new[] { "Self-love / Confidence", "Relationships" },
                            supportAreas: new[] { "Feel more in control", "Daily mental health check-ins" },
                            stressNotes: "Feeling good but want to maintain momentum",
                            copingMechanisms: new[] { "Exercise / Walking", "Music", "Journaling" },
                            joyPeaceSources: "Family, Friends, Hobbies"
                        ),
                                            WellnessCheckIn.Create(
                            userId: Guid.NewGuid(),
                            mood: "Stressed",
                            checkInDate: DateTime.UtcNow,
                            ageRange: "35-44",
                            focusAreas: new[] { "Career/School", "Physical health" },
                            supportAreas: new[] { "Get help with decisions", "Reminders to care for myself" },
                            stressNotes: "Work pressure and health concerns",
                            copingMechanisms: new[] { "Exercise / Walking", "Deep breathing", "Talking to someone" },
                            joyPeaceSources: "Family, Hobbies, Nature"
                        )
                };

                var results = new List<object>();

                foreach (var checkIn in sampleCheckIns)
                {
                    try
                    {
                        var wellnessResult = await runPodService.AnalyzeWellnessAsync(checkIn);
                        var taskResult = await runPodService.GetTaskSuggestionsAsync(checkIn);
                        var urgencyResult = await runPodService.AssessUrgencyAsync(checkIn);

                        results.Add(new
                        {
                            userId = checkIn.UserId,
                            mood = checkIn.MoodLevel,
                            wellnessAnalysis = wellnessResult,
                            taskSuggestions = taskResult,
                            urgencyAssessment = urgencyResult
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            userId = checkIn.UserId,
                            mood = checkIn.MoodLevel,
                            error = ex.Message
                        });
                    }
                }

                return Results.Ok(new
                {
                    message = "Batch processing sample completed",
                    totalCheckIns = sampleCheckIns.Length,
                    results = results
                });
            })
            .RequireAuthorization()
            .WithOpenApi(op =>
            {
                op.Summary = "Example: Batch processing demonstration";
                op.Description = "Demonstrates batch processing multiple wellness check-ins through all RunPod services";
                return op;
            });
        }
    }

    public class CustomPromptRequest
    {
        public string Prompt { get; set; } = "";
        public int MaxTokens { get; set; } = 1000;
        public double Temperature { get; set; } = 0.7;
    }
}
