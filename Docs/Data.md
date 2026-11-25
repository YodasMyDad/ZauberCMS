# Data & Migrations

Paths relative to `ZauberCMS.Core/`. **Rule: don't add tables or columns unless genuinely unavoidable** — prefer
existing JSON/property-value storage. Schema changes need sign-off.

## Contexts

| Provider (`Zauber:DatabaseProvider`) | Context | Migrations folder |
| --- | --- | --- |
| `Sqlite` (default) | `Data/SqliteZauberDbContext.cs` | `Data/Migrations/SqLite/` |
| `SqlServer` | `Data/ZauberDbContext.cs` | `Data/Migrations/SqlServer/` |
| `PostgreSql` | `Data/PostgreSqlZauberDbContext.cs` | `Data/Migrations/PostgreSql/` |

- All inherit `Data/ZauberDbContextBase.cs` (`IdentityDbContext<User, Role, Guid, …>`), exposed as `IZauberDbContext`.
- Each context configures itself in `OnConfiguring` from `Zauber:ConnectionString`; history table `ZauberMigrations`;
  split queries by default; `EnableSensitiveDataLogging` in DEBUG only.
- `PendingModelChangesWarning` is **suppressed** → a forgotten migration does not fail anything. Drift is silent.
- Migrations are applied automatically at startup (`ZauberSetup.cs`, `Database.Migrate()`), then every `ISeedData` runs.
- Design-time factory (`Data/ZauberDesignTimeDbContextFactory.cs`) reads `../ZauberCMS/appSettings.json` relative to
  the working directory → run migrations from the **repo root**.

## Making a schema change

1. Model in `<Feature>/Models/X.cs`: `Id = Guid.NewGuid().NewSequentialGuid()`, `DateCreated`/`DateUpdated =
   DateTime.UtcNow`.
2. `<Feature>/Mapping/XDbMapping.cs` implementing `IEntityTypeConfiguration<X>`; table name `Zauber`-prefixed.
   Mappings are picked up by `ApplyConfigurationsFromAssembly` **for the Core assembly only** — plugin assemblies can't
   add entities this way.
3. `DbSet<X>` in **both** `Data/IZauberDbContext.cs` and `Data/ZauberDbContextBase.cs`.
4. `MapTo` in `<Feature>/Mapping/XMappingExtensions.cs` (for service updates).
5. From repo root: `./migrations.ps1 -MigrationName AddXToY` → generates the migration for all three contexts.
6. Review all three migrations + model snapshots, commit together. Never edit a generated migration by hand except to
   add data-preserving SQL that is valid on that provider.
7. Model-only changes (e.g. a concurrency token) still need a migration in all three so snapshots stay aligned —
   see `Data/Migrations/*/…_AddDateUpdatedConcurrencyToken.cs`.

`migrations.ps1` parameters: `-MigrationName` (prompts if missing), `-ProjectPath`/`-StartupProject` (default
`ZauberCMS.Core`). It updates the global `dotnet-ef` tool first. To remove a bad migration, run
`dotnet ef migrations remove --context <Ctx> --project ZauberCMS.Core --startup-project ZauberCMS.Core` for **each** of
the three contexts.

## Mapping conventions

- FKs mostly `DeleteBehavior.NoAction` (avoids SQL Server multiple-cascade-path errors); delete children in code.
- JSON columns: `builder.Property(x => x.Prop).ToJsonConversion(maxLength | null)` (`Extensions/ValueConversionExtensions.cs`);
  `T : class, new()`, null → `new T()`, compared by serialisation, null length = max/text. Examples:
  `Content/Mapping/ContentTypeDbMapping.cs`, `Content/Mapping/ContentDbMapping.cs` (`Path` is JSON `List<Guid>`).
- Identity tables renamed to `Zauber*` in `ZauberDbContextBase.OnModelCreating`.
- Index names: follow the existing table's style; new indexes `IX_<Table>_<Column>`.
- `Content.DateUpdated` is a concurrency token.

## Provider gotchas

- PostgreSQL `timestamp with time zone` requires `DateTimeKind.Utc`.
- PostgreSQL folds unquoted identifiers to lower case; EF creates quoted names. Raw SQL with unquoted `ZauberContent`
  (`Extensions/DbContextExtensions.cs` `WherePathLike` — still used by Restrict access — and `WhereHasCompositionsUsing`) is suspected broken on PostgreSQL —
  tracked in [.audit/report.md](../.audit/report.md).
- Column types differ per provider (`nvarchar(max)` vs `TEXT` vs `text`) — let EF generate them; don't hard-code
  `HasColumnType` unless all three are handled.
- Case-sensitivity of `=`/`LIKE` differs (SQL Server CI collation, PostgreSQL case-sensitive, SQLite ASCII-only NOCASE).
