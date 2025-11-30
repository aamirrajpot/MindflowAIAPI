# Demo Payloads Reference

This directory contains `demo-payloads.json` - a collection of ready-to-use JSON payloads for testing the Mindflow AI API.

## Quick Start

1. **Copy a payload** from `demo-payloads.json`
2. **Replace placeholder values** (like GUIDs, dates) with actual values
3. **Send the request** to the appropriate endpoint

## File Structure

The `demo-payloads.json` file contains:

### 1. Brain Dump Payloads
- **Endpoint**: `POST /brain-dump/suggestions`
- **Examples**: 6 different scenarios (positive, stressed, relationships, health goals, etc.)
- **Required**: `text` (min 3 characters)
- **Optional**: `context`, `mood`, `stress`, `purpose` (all 0-10 scale)

### 2. Wellness Check-In Payloads
- **Endpoint**: `PATCH /api/wellness/check-in`
- **Create Examples**: Initial setup scenarios
- **Update Examples**: Partial update scenarios
- **Required for creation**: `moodLevel`
- **Optional**: Time slots, focus areas, questions dictionary, etc.

### 3. Add to Calendar Payloads
- **Endpoint**: `POST /brain-dump/add-to-calendar`
- **Examples**: Single tasks, recurring tasks, scheduled tasks
- **Required**: `task`, `frequency`, `duration`

## Usage Examples

### Using Brain Dump Payload

```bash
# Copy the "Work Stress - Overwhelmed" payload
curl -X POST https://your-api.com/brain-dump/suggestions \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "I'\''m so overwhelmed right now...",
    "context": "Work stress, time management issues",
    "mood": 4,
    "stress": 9,
    "purpose": 5
  }'
```

### Using Wellness Check-In Payload

```bash
# Copy the "Update Mood and Stress Notes" payload
curl -X PATCH https://your-api.com/api/wellness/check-in \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "moodLevel": "Overwhelmed",
    "questions": {
      "stressNotes": "Had a difficult meeting with my manager today..."
    }
  }'
```

## Important Notes

- **Authentication**: All endpoints require a Bearer token in the Authorization header
- **Date Formats**: Use ISO 8601 format (e.g., `2024-01-20T14:00:00Z`)
- **Time Formats**: Use `HH:mm:ss` format (e.g., `14:00:00`)
- **GUIDs**: Replace placeholder GUIDs with actual values from your system
- **Timezone**: Use IANA timezone IDs (e.g., `America/New_York`, `Europe/London`)

## Field Reference

### Brain Dump Request
- `text` (required): The brain dump content (min 3 chars)
- `context` (optional): Additional context
- `mood` (optional): Mood score 0-10
- `stress` (optional): Stress score 0-10
- `purpose` (optional): Purpose score 0-10

### Wellness Check-In
- `moodLevel` (required for creation): One of: "Stressed", "Anxious", "Neutral", "Grateful", "Overwhelmed", etc.
- `reminderEnabled` (optional): Boolean
- `reminderTime` (optional): Time string (e.g., "09:00")
- `ageRange` (optional): "Under 18", "18-24", "25-34", "35-44", "45-54", "55+"
- `focusAreas` (optional): Array of focus areas
- `questions` (optional): Dictionary of dynamic questions
- `timezoneId` (optional): IANA timezone identifier

### Add to Calendar
- `task` (required): Task description
- `frequency` (required): "once", "daily", "weekly", "bi-weekly", "monthly", "weekdays"
- `duration` (required): Duration string (e.g., "30 minutes", "1 hour")
- `notes` (optional): Additional notes
- `date` (optional): ISO 8601 date
- `time` (optional): TimeSpan format
- `reminderEnabled` (optional): Boolean
- `brainDumpEntryId` (optional): Link to brain dump entry

## Tips

1. **Start Simple**: Use minimal payloads first to test basic functionality
2. **Incremental Testing**: Add optional fields one at a time
3. **Error Handling**: Check API responses for validation errors
4. **Update Payloads**: Modify the examples to match your specific use cases

## Need Help?

- Check the API documentation for endpoint details
- Review the `notes` section in `demo-payloads.json` for field-specific guidance
- Test with minimal payloads first, then add complexity

