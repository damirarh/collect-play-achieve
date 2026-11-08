# Database access

## Stack

- SQLite
- EF Core

## Rules

- Code first approach
- Generate migrations for each commit with model changes
- Run migrations during API project startup
  - Restore the local tools with `dotnet tool restore`.
  - Then add migrations from the repository root with `dotnet tool run dotnet-ef migrations add <MigrationName> --project api/CollectPlayAchieve.Api --startup-project api/CollectPlayAchieve.Api --output-dir Data/Migrations`.
  - Do not make any modifications to the generated files.
- Database file location
  - Data folder is read from the optional `App:DataFolder` setting in `appsettings.json`. Only absolute paths and paths starting with `~` as home-directory shorthand are allowed.
  - When setting is not specified, the data folder defaults to `data` under the process working directory.
  - The database file is `db/collect-play-achieve.db` inside the resolved data folder.
