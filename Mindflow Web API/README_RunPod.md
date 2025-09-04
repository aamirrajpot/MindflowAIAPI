# RunPod AI Service Integration

This document explains how to use the new RunPod AI service integration in the Mindflow Web API.

## Overview

The RunPod service provides AI-powered wellness analysis, task suggestions, and urgency assessment using RunPod's serverless AI endpoints. It uses the `[INST]` and `[/INST]` format for Llama-style prompts and provides synchronous responses.

## Configuration

### 1. Update appsettings.json

Add your RunPod configuration to `appsettings.json`:

```json
{
  "RunPod": {
    "ApiKey": "your_runpod_api_key_here",
    "Endpoint": "https://api.runpod.ai/v2/your_pod_id/run"
  }
}
```

### 2. Get Your RunPod Credentials

1. Go to [RunPod.io](https://runpod.io)
2. Create an account and get your API key
3. Deploy a serverless pod with your preferred AI model
4. Copy the endpoint URL from your pod

## API Endpoints

### Wellness Analysis
```http
POST /api/runpod/analyze-wellness
Authorization: Bearer {your_jwt_token}
Content-Type: application/json

{
  "userId": "user-guid-here",
  "moodLevel": "Stressed",
  "ageRange": "25-34",
  "focusAreas": ["Work", "Stress Management"],
  "supportAreas": ["Emotional Support", "Time Management"],
  "stressNotes": "Feeling overwhelmed with deadlines",
  "copingMechanisms": ["Deep breathing", "Walking"],
  "joyPeaceSources": "Reading, Nature walks"
}
```

**Query Parameters:**
- `maxTokens` (optional): Maximum response length (default: 1000)
- `temperature` (optional): AI creativity (0.0-1.0, default: 0.7)

### Task Suggestions
```http
POST /api/runpod/task-suggestions
Authorization: Bearer {your_jwt_token}
Content-Type: application/json

{
  "userId": "user-guid-here",
  "moodLevel": "Stressed",
  "ageRange": "25-34",
  "focusAreas": ["Work", "Stress Management"],
  "supportAreas": ["Emotional Support", "Time Management"],
  "stressNotes": "Feeling overwhelmed with deadlines",
  "copingMechanisms": ["Deep breathing", "Walking"],
  "joyPeaceSources": "Reading, Nature walks"
}
```

### Urgency Assessment
```http
POST /api/runpod/assess-urgency
Authorization: Bearer {your_jwt_token}
Content-Type: application/json

{
  "userId": "user-guid-here",
  "moodLevel": "Stressed",
  "stressNotes": "Feeling overwhelmed with deadlines",
  "copingMechanisms": ["Deep breathing", "Walking"]
}
```

### Custom Prompt
```http
POST /api/runpod/custom-prompt
Authorization: Bearer {your_jwt_token}
Content-Type: application/json

{
  "prompt": "[INST] Your custom prompt here [/INST]",
  "maxTokens": 1000,
  "temperature": 0.7
}
```

### Health Check
```http
GET /api/runpod/health
```

## Response Formats

### Wellness Analysis Response
```json
{
  "wellnessAnalysis": {
    "moodAssessment": "Detailed mood analysis",
    "stressLevel": "Stress evaluation",
    "supportNeeds": ["Need 1", "Need 2"],
    "copingStrategies": ["Strategy 1", "Strategy 2"],
    "selfCareSuggestions": ["Suggestion 1", "Suggestion 2"],
    "progressTracking": "Tracking recommendations",
    "urgencyLevel": 5,
    "immediateActions": ["Action 1", "Action 2"],
    "longTermGoals": ["Goal 1", "Goal 2"]
  },
  "rawResponse": "Original RunPod response",
  "isSuccess": true
}
```

### Task Suggestions Response
```json
[
  {
    "task": "Practice Deep Breathing",
    "frequency": "Daily",
    "duration": "10-15 minutes",
    "notes": "Learn and practice deep breathing exercises to help manage stress"
  },
  {
    "task": "Take a Walk",
    "frequency": "Multiple times per day",
    "duration": "10-15 minutes",
    "notes": "Take short walks to clear your mind and boost mood"
  }
]
```

### Urgency Assessment Response
```json
{
  "urgencyLevel": 5,
  "reasoning": "User is experiencing moderate stress with available coping mechanisms",
  "immediateAction": "Practice deep breathing and take a short walk"
}
```

## Usage Examples

### C# Service Usage

```csharp
// Inject the service
private readonly IRunPodService _runPodService;

public async Task<RunPodResponse> AnalyzeWellness(WellnessCheckIn checkIn)
{
    try
    {
        var result = await _runPodService.AnalyzeWellnessAsync(
            checkIn, 
            maxTokens: 1500, 
            temperature: 0.8
        );
        
        if (result.IsSuccess)
        {
            return result;
        }
        
        // Handle case where analysis didn't return structured data
        return result;
    }
    catch (Exception ex)
    {
        // Handle errors
        throw;
    }
}
```

### JavaScript/TypeScript Usage

```typescript
const analyzeWellness = async (checkIn: WellnessCheckIn) => {
  try {
    const response = await fetch('/api/runpod/analyze-wellness?maxTokens=1500&temperature=0.8', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(checkIn)
    });
    
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    const result = await response.json();
    return result;
  } catch (error) {
    console.error('Error analyzing wellness:', error);
    throw error;
  }
};
```

## Error Handling

The service includes comprehensive error handling:

- **HTTP Errors**: RunPod API errors are logged and returned with details
- **Parsing Errors**: JSON parsing failures are handled gracefully
- **Configuration Errors**: Missing API keys or endpoints throw clear exceptions
- **Network Errors**: HttpClient errors are caught and logged

## Logging

The service logs:
- Request details (endpoint, payload)
- Response information
- Error details with stack traces
- Success confirmations

Check your application logs for detailed RunPod service activity.

## Troubleshooting

### Common Issues

1. **401 Unauthorized**: Check your RunPod API key
2. **404 Not Found**: Verify your RunPod endpoint URL
3. **500 Internal Server Error**: Check logs for detailed error information
4. **Parsing Errors**: Ensure your AI model returns valid JSON

### Environment Variables

You can also use environment variables:

```bash
export RunPod__ApiKey="your_api_key"
export RunPod__Endpoint="https://api.runpod.ai/v2/your_pod_id/run"
```

### Testing

Use the health check endpoint to verify service connectivity:

```bash
curl -X GET "https://your-api.com/api/runpod/health"
```

## Security

- All endpoints require JWT authentication
- API keys are stored securely in configuration
- HTTPS is enforced for all communications
- User data is validated before processing

## Performance

- Uses HttpClient with connection pooling
- Configurable token limits for response optimization
- Async/await pattern for non-blocking operations
- Structured logging for monitoring and debugging
