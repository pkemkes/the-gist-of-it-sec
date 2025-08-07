# Exception

```
System.InvalidOperationException: MariaDB connection parameters are not set.
at GistBackend.Handlers.MariaDbHandler.MariaDbHandlerOptions.CheckIfConfigIsSet() in /src/GistBackend/Handlers/MariaDbHandler/MariaDbHandlerOptions.cs:line 27
at GistBackend.Handlers.MariaDbHandler.MariaDbHandlerOptions.GetConnectionString() in /src/GistBackend/Handlers/MariaDbHandler/MariaDbHandlerOptions.cs:line 14
at GistBackend.Handlers.MariaDbHandler.MariaDbHandler..ctor(IOptions`1 options, IDateTimeHandler dateTimeHandler, ILogger`1 logger) in /src/GistBackend/Handlers/MariaDbHandler/MariaDbHandler.cs:line 57
at GistBackend.StartUp.<>c.<ConfigureServices>b__4_6(IServiceProvider provider, Object _) in /src/GistBackend/StartUp.cs:line 96
at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSiteMain(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, ServiceProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteRuntimeResolver.Resolve(ServiceCallSite callSite, ServiceProviderEngineScope scope)
at Microsoft.Extensions.DependencyInjection.ServiceLookup.DynamicServiceProviderEngine.<>c__DisplayClass2_0.<RealizeService>b__0(ServiceProviderEngineScope scope)
at Microsoft.Extensions.DependencyInjection.ServiceProvider.GetService(ServiceIdentifier serviceIdentifier, ServiceProviderEngineScope serviceProviderEngineScope)
at Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceProviderEngineScope.GetKeyedService(Type serviceType, Object serviceKey)
at lambda_method58(Closure, IServiceProvider, Object[])
at Microsoft.AspNetCore.Mvc.Controllers.ControllerFactoryProvider.<>c__DisplayClass6_0.<CreateControllerFactory>g__CreateController|0(ControllerContext controllerContext)
at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeInnerFilterAsync()
--- End of stack trace from previous location ---
at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Logged|17_1(ResourceInvoker invoker)
at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Logged|17_1(ResourceInvoker invoker)
at Microsoft.AspNetCore.Routing.EndpointMiddleware.<Invoke>g__AwaitRequestTask|7_0(Endpoint endpoint, Task requestTask, ILogger logger)
at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.ProcessRequests[TContext](IHttpApplication`1 application)
```

## Problem Analysis

The exception occurs because the `GistsControllerMariaDbHandlerOptions` configuration is missing from the StartUp.cs services configuration, NOT from docker-compose.yaml.

**Root Cause:**
- The docker-compose.yaml correctly includes all environment variables including `GistsControllerMariaDbHandlerOptions`
- However, the StartUp.cs `ConfigureServices` method is missing the `services.Configure<MariaDbHandlerOptions>` call for `GistsControllerMariaDbHandlerOptionsName`
- The StartUp.cs configures 4 MariaDbHandler instances but missing the 5th one:
  - `GistMariaDbHandlerOptions` ✓ (configured in StartUp.cs)
  - `RecapMariaDbHandlerOptions` ✓ (configured in StartUp.cs)
  - `CleanupMariaDbHandlerOptions` ✓ (configured in StartUp.cs)
  - `TelegramMariaDbHandlerOptions` ✓ (configured in StartUp.cs)
  - `GistsControllerMariaDbHandlerOptions` ❌ (MISSING from StartUp.cs services.Configure calls)

**Why it fails:**
When the GistsController tries to inject the keyed MariaDbHandler, the configuration system can't find the `GistsControllerMariaDbHandlerOptions` section because it was never registered with `services.Configure`, causing the options to have default empty values.

## Solution Steps

1. **Add missing configuration call** to StartUp.cs in the `ConfigureServices` method:
   ```csharp
   services.Configure<MariaDbHandlerOptions>(GistsControllerMariaDbHandlerOptionsName,
       configuration.GetSection(GistsControllerMariaDbHandlerOptionsName));
   ```

2. **Verify the environment variables** `DB_GISTSCONTROLLER_USERNAME` and `DB_GISTSCONTROLLER_PASSWORD` exist in your .env file

3. **Test the configuration** by making a request to the `/api/v1/gists` endpoint to confirm the GistsController can initialize properly

# docker-compose.yaml configuration

```yaml
  backend:
    image: pkemkes/gist-backend
    container_name: backend
    depends_on:
      database:
        condition: service_healthy
      chromadb:
        condition: service_healthy
      prometheus:
        condition: service_healthy
    environment:
      - EmbeddingClientHandlerOptions__ApiKey=${OPENAI_API_KEY}
      - EmbeddingClientHandlerOptions__ProjectId=${OPENAI_PROJECT_KEY}
      - ChatClientHandlerOptions__ApiKey=${OPENAI_API_KEY}
      - ChatClientHandlerOptions__ProjectId=${OPENAI_PROJECT_KEY}
      - ChromaDbHandlerOptions__Server=chromadb
      - ChromaDbHandlerOptions__ServerAuthnCredentials=${CHROMA_SERVER_AUTHN_CREDENTIALS}
      - CustomSearchApiHandlerOptions__ApiKey=${GOOGLE_API_KEY}
      - CustomSearchApiHandlerOptions__EngineId=${GOOGLE_SEARCH_ENGINE_ID}
      - TelegramBotClientHandlerOptions__BotToken=${TELEGRAM_API_KEY}
      - TelegramServiceOptions__AppBaseUrl=http://localhost:8081  # NOTE: Change this to your base URL
      - GistMariaDbHandlerOptions__Server=database
      - GistMariaDbHandlerOptions__User=${DB_GISTSERVICE_USERNAME}
      - GistMariaDbHandlerOptions__Password=${DB_GISTSERVICE_PASSWORD}
      - RecapMariaDbHandlerOptions__Server=database
      - RecapMariaDbHandlerOptions__User=${DB_RECAPSERVICE_USERNAME}
      - RecapMariaDbHandlerOptions__Password=${DB_RECAPSERVICE_PASSWORD}
      - CleanupMariaDbHandlerOptions__Server=database
      - CleanupMariaDbHandlerOptions__User=${DB_CLEANUPSERVICE_USERNAME}
      - CleanupMariaDbHandlerOptions__Password=${DB_CLEANUPSERVICE_PASSWORD}
      - TelegramMariaDbHandlerOptions__Server=database
      - TelegramMariaDbHandlerOptions__User=${DB_TELEGRAMSERVICE_USERNAME}
      - TelegramMariaDbHandlerOptions__Password=${DB_TELEGRAMSERVICE_PASSWORD}
      - GistsControllerMariaDbHandlerOptions__Server=database
      - GistsControllerMariaDbHandlerOptions__User=${DB_GISTSCONTROLLER_USERNAME}
      - GistsControllerMariaDbHandlerOptions__Password=${DB_GISTSCONTROLLER_PASSWORD}
    networks:
      - database
      - chromadb
      - prometheus
    ports:
      - "8080:8080"
```
