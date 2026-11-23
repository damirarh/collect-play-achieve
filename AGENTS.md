# Repository guidance

## Build, run, and checks

- Build the .NET solution from the repository root: `dotnet build api/CollectPlayAchieve.slnx`.
- Run the full app through its Aspire AppHost: `dotnet run --project api/CollectPlayAchieve.AppHost`. The SDK version is pinned in `api/global.json`.
- Run API tests: `dotnet test api/CollectPlayAchieve.Api.Tests/CollectPlayAchieve.Api.Tests.csproj`.
- Install the repository's pre-commit hooks with `dotnet tool restore` and `dotnet husky install`.
- Format C# with `dotnet csharpier format ./api`; check formatting with `dotnet csharpier check ./api`.
- Frontend-only work: use Node from `web/.node-version`, then run `pnpm install --frozen-lockfile` and `pnpm dev` in `web/`.
- Frontend checks: `pnpm lint` and `pnpm format:check`; apply fixes with `pnpm lint:fix` and `pnpm format`.
- No frontend test scripts are currently configured.

## Architecture

- `api/CollectPlayAchieve.AppHost` is the Aspire entry point. It starts the ASP.NET API and the Nuxt/Vite web app, then configures YARP as the external gateway.
- The gateway forwards `/` to the web app and `/api/{**catch-all}` to the API, removing the `/api` prefix before forwarding. Keep API calls on this route when working with the integrated app.
- `api/CollectPlayAchieve.Api/Program.cs` is the minimal ASP.NET Core host; OpenAPI is exposed only in Development.
- The API uses vertical feature folders under `api/CollectPlayAchieve.Api/Features`. Keep feature endpoints and application types in their feature namespace.
- Database configuration and migration conventions are documented in `docs/database.md`.
- `web/` is the Nuxt application; app-level UI lives under `web/app/`. Aspire manages its Vite process when running the AppHost.
- `bruno/` contains a separate Bruno collection for Steam Web API requests. Its environment declares the Steam API key and Steam ID as secret variables; do not put their values in collection files.

## API endpoint and application structure

- Register HTTP endpoints in feature-specific classes that implement `IEndpoint` and provide `MapEndpoint(IEndpointRouteBuilder)`. Group related endpoints in a single feature endpoint class and use route tags or route groups for a feature area.
- Endpoint classes are transport adapters. Register routes and thinly map HTTP parameters and `Result` values; do not place business logic in them.
- Represent writes with `ICommand` or `ICommand<TResponse>` types and reads with `IQuery<TResponse>` types. Implement behavior in `ICommandHandler<...>` or `IQueryHandler<...>` classes in the feature folder.
- Handlers, validators, and endpoint classes are discovered automatically by the Scrutor assembly scan in `Program.cs`.
- Return `Result` or `Result<T>` from handlers. Keep HTTP status and `ProblemDetails` mapping in endpoint/result infrastructure.

## Validation and errors

- Define input validation with FluentValidation `AbstractValidator<T>` classes.
- Scrutor registers validators and applies command/query validation decorators automatically. Validation failures must be returned through `Result.Error`, not thrown directly by handlers.
- Define all application errors centrally in `Error`, with feature-specific nested classes such as `Error.Registration`. Set the error type, code, description, and any validation errors consistently.
- Do not introduce endpoint-local error constants or ad hoc validation dictionaries.
- Map known failures through `ErrorType` and `ResultExtensions.ToProblemDetails`. `UnhandledExceptionHandler` handles unexpected exceptions centrally.

## OpenAPI contract

- Every mapped endpoint must be fully documented in OpenAPI. Set a tag, summary, and description; declare all responses and document every path/query parameter and request body.
- Use `Description` attributes or equivalent schema metadata for OpenAPI object schemas and every response/request property.
- OpenAPI is exposed only in Development at `/openapi/v1.json`.
- `OpenApiTests.DevelopmentDocument_DescribesEveryApplicationOperation` validates that every application route appears in the development document with complete operations, parameters, request bodies, responses, and descriptions.
- When changing an endpoint, keep route metadata and request/response types synchronized with the OpenAPI contract. Include at least one explicit 2xx response and an explicit 500 response.
- Production must not expose `/openapi/{documentName}.json`.

## Testing

- Tests use TUnit and are placed in `api/CollectPlayAchieve.Api.Tests`.
- API test classes should derive from `ApiTestBase`. It creates an isolated temporary SQLite data directory and test web host, initializes them before each test, and disposes them afterward.
- Use `Factory.CreateClient()` for HTTP integration tests and `Factory.Services` for application-service tests. Do not create a separate test host when `ApiTestBase` is sufficient.
- Mock dependencies with `TUnit.Mocks`. Follow the existing `*.Mock()` and `.Returns(...)` pattern.
- Use TUnit assertions and attributes such as `[Test]`, `[Arguments(...)]`, `[Before(Test)]`, and `[After(Test)]`.
- Add focused tests for endpoint behavior and handler behavior. Integration tests should cover success and relevant failure responses; handler tests should cover state changes and result/error mapping.

## Repository-specific conventions

- .NET targets version specified in `api/global.json`, enables nullable reference types and implicit usings, and treats compiler/analyzer warnings as errors. Keep package versions centralized in `api/Directory.Packages.props` and preserve projects' NuGet lock files when changing dependencies.
- Follow existing C# formatting and organization. Format C# with CSharpier; do not hand-format generated EF migration files.
- Follow EF Core code-first conventions in `docs/database.md`. Generate migrations for model changes and run migrations during API startup.
- Use pnpm for the web workspace (`web/pnpm-lock.yaml`). `web/README.md` contains generic Nuxt alternatives, but this repository's actual workspace and hooks use pnpm.
- The pre-commit hook runs CSharpier on `api/`, then Prettier and ESLint with fixes on `web/`. Hook tasks are in `.husky/task-runner.json`; local hook installation is documented in the root `README.md`.
