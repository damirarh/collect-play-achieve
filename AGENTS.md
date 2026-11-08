# Repository guidance

## Build, run, and checks

- Build the .NET solution from the repository root: `dotnet build api/CollectPlayAchieve.slnx`.
- Run the full app through its Aspire AppHost: `dotnet run --project api/CollectPlayAchieve.AppHost`. The SDK version is pinned in `api/global.json`.
- Install the repository's pre-commit hooks with `dotnet tool restore` and `dotnet husky install`.
- For frontend-only work, use the Node version specified in `web/.node-version` and pnpm from `web/`: `pnpm install --frozen-lockfile`, then `pnpm dev`.
- Frontend checks from `web/`: `pnpm lint` and `pnpm format:check`; apply fixes with `pnpm lint:fix` and `pnpm format`.
- Format C# with `dotnet csharpier format ./api`; use `dotnet csharpier check ./api` to check formatting.
- No test projects or frontend test scripts are currently configured.

## Architecture

- `api/CollectPlayAchieve.AppHost` is the Aspire entry point. It starts the ASP.NET API and the Nuxt/Vite web app, then configures YARP as the external gateway.
- The gateway forwards `/` to the web app and `/api/{**catch-all}` to the API, removing the `/api` prefix before forwarding. Keep API calls on this route when working with the integrated app.
- `api/CollectPlayAchieve.Api/Program.cs` is the minimal ASP.NET Core host; OpenAPI is exposed only in Development. Add API endpoints and services here or in the relevant feature code as the API grows.
- Database configuration and migration conventions are documented in `docs/database.md`.
- The tests for the API project are placed in `api/CollectPlayAchieve.Api.Tests` and use TUnit as test framework.
- `web/` is the Nuxt application; app-level UI lives under `web/app/`. Aspire manages its Vite process when running the AppHost.
- `bruno/` contains a separate Bruno collection for Steam Web API requests. Its environment declares the Steam API key and Steam ID as secret variables; do not put their values in collection files.

## Repository-specific conventions

- .NET targets `net10.0`, enables nullable reference types and implicit usings, and treats compiler/analyzer warnings as errors. Keep package versions centralized in `api/Directory.Packages.props` and preserve the projects' NuGet lock files when changing dependencies.
- Use pnpm for the web workspace (`web/pnpm-lock.yaml`); the `web/README.md` contains generic Nuxt starter alternatives, but this repository's workspace and hooks use pnpm.
- The pre-commit hook runs CSharpier on `api/`, then Prettier and ESLint with fixes on `web/`. Hook tasks are defined in `.husky/task-runner.json`; local installation is documented in the root `README.md`.
