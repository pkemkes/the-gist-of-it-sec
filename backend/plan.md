# Plan: Suppress HTTP Client Logs While Keeping Web Request Logs

## Problem Analysis
The application is generating verbose logs for outbound HTTP requests to ChromaDB with:
- Source Context: `System.Net.Http.HttpClient.Default.LogicalHandler`
- Event ID: 100 (RequestPipelineStart)
- These logs appear for every HTTP POST to ChromaDB API endpoints

## Goal
- Suppress HTTP client logs for outbound requests (to ChromaDB, OpenAI, etc.)
- Keep ASP.NET Core web request logs for incoming HTTP requests
- Maintain all other application logs at current levels

## Solution Strategy

### Step 1: Configure Serilog Minimum Level Overrides
Add logging level overrides in the Serilog configuration to specifically target the HTTP client logger source contexts:

- `System.Net.Http.HttpClient` - Controls HttpClient logging
- `System.Net.Http.HttpClient.Default.LogicalHandler` - Controls detailed request pipeline logs
- `System.Net.Http.HttpClient.Default.ClientHandler` - Controls low-level HTTP handler logs

### Step 2: Preserve ASP.NET Core Request Logging
Ensure that incoming web request logs are preserved by keeping these loggers at Information level:
- `Microsoft.AspNetCore.Hosting.Diagnostics` - Incoming request logs
- `Microsoft.AspNetCore.Routing` - Route matching logs
- `Microsoft.AspNetCore.Mvc` - Controller action logs

### Step 3: Implementation Details

#### Modify Program.cs
Update the Serilog configuration in `Program.cs` to include minimum level overrides:

```csharp
configuration
    .Enrich.FromLogContext()
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient.Default.LogicalHandler", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient.Default.ClientHandler", LogEventLevel.Warning)
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
```

#### Alternative: More Granular Control
If the above suppresses too much, use more specific overrides:
- Set `System.Net.Http.HttpClient.Default.LogicalHandler` to `Error` to only show actual errors
- Keep other HTTP client loggers at `Warning` for important issues

### Step 4: Verification Steps

1. **Before Changes**: Run the application and observe the current log volume
2. **After Changes**: Verify that:
   - HTTP client logs (POST to ChromaDB) are suppressed
   - Incoming web requests to your API endpoints are still logged
   - Application errors and warnings are still visible
   - Other important logs (business logic, exceptions) remain intact

### Step 5: Alternative Solutions (if needed)

#### Option A: Custom Log Filter
If minimum level overrides don't provide enough control, implement a custom Serilog filter:
- Use `.Filter.ByExcluding()` with conditions based on source context and message templates
- More granular control over which specific log messages to suppress

#### Option B: HttpClient-Specific Configuration
Configure logging specifically for the HttpClient instances:
- Use `services.Configure<HttpClientFactoryOptions>()` to set logging options
- Apply logging configuration per named HttpClient

#### Option C: Custom Serilog Enricher
Create a custom enricher to modify or suppress logs based on context properties:
- Check for specific URI patterns (ChromaDB endpoints)
- Suppress logs based on HTTP method and destination

### Step 6: Testing Scenarios

1. **HTTP Client Logs**: Make requests to ChromaDB - should not see detailed request pipeline logs
2. **Web Request Logs**: Make requests to your API endpoints - should see incoming request logs
3. **Error Logs**: Simulate HTTP client errors - should still see error-level logs
4. **Application Logs**: Verify business logic logs are unaffected

## Expected Outcome
- Significantly reduced log noise from HTTP client operations
- Maintained visibility into incoming web requests and application behavior
- Preserved error visibility for troubleshooting
- Cleaner, more focused log output for production monitoring
