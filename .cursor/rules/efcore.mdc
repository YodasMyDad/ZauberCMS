---
description: For any changes or updates to the database structure
globs:
alwaysApply: true
---

Based on my analysis of the ZauberCMS data layer, here's a comprehensive developer guide for EF Core changes:

# EF Core Developer Guide - ZauberCMS

This guide outlines the patterns and practices for making database changes in ZauberCMS, ensuring consistency across all supported database providers (SQL Server, SQLite, PostgreSQL).

DO NOT add new tables or properties unless it is absolutely necessary. 

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Adding New Entities](#adding-new-entities)
3. [Updating Existing Entities](#updating-existing-entities)
4. [Creating Migrations](#creating-migrations)
5. [Migration Best Practices](#migration-best-practices)
6. [Testing Database Changes](#testing-database-changes)

## Architecture Overview

ZauberCMS uses a multi-provider EF Core architecture supporting SQL Server, SQLite, and PostgreSQL. The key components are:

### Core Components

- **`IZauberDbContext`**: Interface defining the database contract
- **`ZauberDbContextBase`**: Base class containing common configuration
- **Provider-specific contexts**: `ZauberDbContext`, `SqliteZauberDbContext`, `PostgreSqlZauberDbContext`
- **Design-time factories**: For generating migrations for each provider
- **Mapping classes**: Using `IEntityTypeConfiguration` pattern

### Database Providers

| Provider   | Context Class               | Migration Path                |
| ---------- | --------------------------- | ----------------------------- |
| SQL Server | `ZauberDbContext`           | `Data/Migrations/SqlServer/`  |
| SQLite     | `SqliteZauberDbContext`     | `Data/Migrations/SqLite/`     |
| PostgreSQL | `PostgreSqlZauberDbContext` | `Data/Migrations/PostgreSql/` |

## Adding New Entities

### Step 1: Create the Entity Model

Create your entity class in the appropriate folder under `ZauberCMS.Core/`. For example:

```csharp
// ZauberCMS.Core/Data/Models/ExampleEntity.cs
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Data.Models;

public class ExampleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
```

**Note**: Always use `Guid.NewGuid().NewSequentialGuid()` for ID generation to ensure optimal performance.

### Step 2: Create Database Mapping

Create a mapping class in `ZauberCMS.Core/Data/Mapping/`:

```csharp
// ZauberCMS.Core/Data/Mapping/ExampleEntityDbMapping.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Mapping;

public class ExampleEntityDbMapping : IEntityTypeConfiguration<ExampleEntity>
{
    public void Configure(EntityTypeBuilder<ExampleEntity> builder)
    {
        builder.ToTable("ZauberExampleEntities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Description).HasMaxLength(4000);
        
        // Add indexes for commonly queried fields
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_ExampleEntity_Name");
    }
}
```

### Step 3: Update IZauberDbContext Interface

Add the new DbSet to the interface:

```csharp
// ZauberCMS.Core/Data/IZauberDbContext.cs
// ... existing code ...

// --- Your CMS-specific DbSets ---
DbSet<ExampleEntity> ExampleEntities { get; }

// ... rest of interface ...
```

### Step 4: Update ZauberDbContextBase

Add the DbSet property to the base context:

```csharp
// ZauberCMS.Core/Data/ZauberDbContextBase.cs
// ... existing code ...

// All DbSets
public DbSet<ExampleEntity> ExampleEntities => Set<ExampleEntity>();

// ... rest of class ...
```

## Updating Existing Entities

### Adding Properties

1. **Update the entity model**:
```csharp
public class GlobalData
{
    // ... existing properties ...
    public bool IsActive { get; set; } = true;
}
```

2. **Update the mapping**:
```csharp
public class GlobalDataDbMapping : IEntityTypeConfiguration<GlobalData>
{
    public void Configure(EntityTypeBuilder<GlobalData> builder)
    {
        // ... existing configuration ...
        builder.Property(x => x.IsActive).HasDefaultValue(true);
    }
}
```

### Modifying Properties

Always use migrations to modify existing properties rather than changing them directly in mappings, as this ensures proper database schema updates.

## Creating Migrations

### Prerequisites

Ensure you have the EF Core tools installed:

```bash
dotnet tool update --global dotnet-ef
```

### Step 1: Create Migration for Each Provider

Create migrations for all supported database providers. Use descriptive names that reflect the change:

```bash
# SQL Server
dotnet ef migrations add AddExampleEntity --context ZauberDbContext -o "Data/Migrations/SqlServer"

# SQLite
dotnet ef migrations add AddExampleEntity --context SqliteZauberDbContext -o "Data/Migrations/SqLite"

# PostgreSQL
dotnet ef migrations add AddExampleEntity --context PostgreSqlZauberDbContext -o "Data/Migrations/PostgreSql"
```

### Step 2: Review Generated Migrations

Examine the generated migration files to ensure they contain the correct changes:

```csharp
// Example migration file
public partial class AddExampleEntity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ZauberExampleEntities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZauberExampleEntities", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExampleEntity_Name",
            table: "ZauberExampleEntities",
            column: "Name");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ZauberExampleEntities");
    }
}
```

### Step 3: Update Model Snapshots

The `.Designer.cs` files and model snapshots are automatically updated when you create migrations. These should not be manually edited.

## Migration Best Practices

### Naming Conventions

- **Migration names**: Use PascalCase with descriptive names (e.g., `AddExampleEntity`, `UpdateUserRoles`)
- **Table names**: Always prefix with `Zauber` (e.g., `ZauberUsers`, `ZauberContentTypes`)
- **Index names**: Prefix with `IX_` followed by table name and column (e.g., `IX_GlobalData_Alias`)
- **Foreign key names**: Let EF generate them automatically for consistency

### Migration Guidelines

1. **Always create migrations for all providers** - ZauberCMS supports multiple database providers
2. **Test migrations on all providers** before committing
3. **Use descriptive migration names** that clearly indicate the change
4. **Review generated SQL** to ensure it matches expectations
5. **Never modify existing migrations** - create new ones for additional changes
6. **Include both Up and Down methods** for proper rollback capability

**Remember, migrations are auto applied when the website starts**

### Common Migration Patterns

#### Adding a Column
```csharp
migrationBuilder.AddColumn<string>(
    name: "NewProperty",
    table: "ZauberExampleTable",
    type: "nvarchar(500)",
    maxLength: 500,
    nullable: true,
    defaultValue: "default_value");
```

#### Creating an Index
```csharp
migrationBuilder.CreateIndex(
    name: "IX_ExampleTable_NewProperty",
    table: "ZauberExampleTable",
    column: "NewProperty");
```

#### Adding a Foreign Key
```csharp
migrationBuilder.AddForeignKey(
    name: "FK_ExampleTable_RelatedTable_RelatedId",
    table: "ZauberExampleTable",
    column: "RelatedId",
    principalTable: "ZauberRelatedTable",
    principalColumn: "Id",
    onDelete: ReferentialAction.Cascade);
```

## Troubleshooting

### Common Issues

1. **Migration conflicts**: Ensure all providers have consistent migrations
2. **Model snapshot issues**: Regenerate snapshots if they become out of sync
3. **Provider-specific errors**: Test each provider individually

### Commands

```bash
# Remove last migration (if not applied to production)
dotnet ef migrations remove --context ZauberDbContext

# List all migrations
dotnet ef migrations list --context ZauberDbContext

# Generate SQL script for migration
dotnet ef migrations script --context ZauberDbContext

# Update to specific migration
dotnet ef database update TargetMigration --context ZauberDbContext
```

## Example: Complete Entity Addition

Following the complete pattern for adding a new `BlogPost` entity:

1. **Create entity** (`Data/Models/BlogPost.cs`)
2. **Create mapping** (`Data/Mapping/BlogPostDbMapping.cs`) 
3. **Update interface** (`IZauberDbContext.cs`)
4. **Update base context** (`ZauberDbContextBase.cs`)
5. **Create migrations** for all three providers
6. **Test the changes**

This ensures all database providers are consistently updated and existing installations can be upgraded without data loss.
[7 tools called]

Based on my analysis of the ZauberCMS data layer, here's a comprehensive developer guide for EF Core changes:

# EF Core Developer Guide - ZauberCMS

This guide outlines the patterns and practices for making database changes in ZauberCMS, ensuring consistency across all supported database providers (SQL Server, SQLite, PostgreSQL).

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Adding New Entities](#adding-new-entities)
3. [Updating Existing Entities](#updating-existing-entities)
4. [Creating Migrations](#creating-migrations)
5. [Migration Best Practices](#migration-best-practices)
6. [Testing Database Changes](#testing-database-changes)

## Architecture Overview

ZauberCMS uses a multi-provider EF Core architecture supporting SQL Server, SQLite, and PostgreSQL. The key components are:

### Core Components

- **`IZauberDbContext`**: Interface defining the database contract
- **`ZauberDbContextBase`**: Base class containing common configuration
- **Provider-specific contexts**: `ZauberDbContext`, `SqliteZauberDbContext`, `PostgreSqlZauberDbContext`
- **Design-time factories**: For generating migrations for each provider
- **Mapping classes**: Using `IEntityTypeConfiguration` pattern

### Database Providers

| Provider   | Context Class               | Migration Path                |
| ---------- | --------------------------- | ----------------------------- |
| SQL Server | `ZauberDbContext`           | `Data/Migrations/SqlServer/`  |
| SQLite     | `SqliteZauberDbContext`     | `Data/Migrations/SqLite/`     |
| PostgreSQL | `PostgreSqlZauberDbContext` | `Data/Migrations/PostgreSql/` |

## Adding New Entities

### Step 1: Create the Entity Model

Create your entity class in the appropriate folder under `ZauberCMS.Core/`. For example:

```csharp
// ZauberCMS.Core/Data/Models/ExampleEntity.cs
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Data.Models;

public class ExampleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
```

**Note**: Always use `Guid.NewGuid().NewSequentialGuid()` for ID generation to ensure optimal performance.

### Step 2: Create Database Mapping

Create a mapping class in `ZauberCMS.Core/Data/Mapping/`:

```csharp
// ZauberCMS.Core/Data/Mapping/ExampleEntityDbMapping.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Mapping;

public class ExampleEntityDbMapping : IEntityTypeConfiguration<ExampleEntity>
{
    public void Configure(EntityTypeBuilder<ExampleEntity> builder)
    {
        builder.ToTable("ZauberExampleEntities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Description).HasMaxLength(4000);
        
        // Add indexes for commonly queried fields
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_ExampleEntity_Name");
    }
}
```

### Step 3: Update IZauberDbContext Interface

Add the new DbSet to the interface:

```csharp
// ZauberCMS.Core/Data/IZauberDbContext.cs
// ... existing code ...

// --- Your CMS-specific DbSets ---
DbSet<ExampleEntity> ExampleEntities { get; }

// ... rest of interface ...
```

### Step 4: Update ZauberDbContextBase

Add the DbSet property to the base context:

```csharp
// ZauberCMS.Core/Data/ZauberDbContextBase.cs
// ... existing code ...

// All DbSets
public DbSet<ExampleEntity> ExampleEntities => Set<ExampleEntity>();

// ... rest of class ...
```

## Updating Existing Entities

### Adding Properties

1. **Update the entity model**:
```csharp
public class GlobalData
{
    // ... existing properties ...
    public bool IsActive { get; set; } = true;
}
```

2. **Update the mapping**:
```csharp
public class GlobalDataDbMapping : IEntityTypeConfiguration<GlobalData>
{
    public void Configure(EntityTypeBuilder<GlobalData> builder)
    {
        // ... existing configuration ...
        builder.Property(x => x.IsActive).HasDefaultValue(true);
    }
}
```

### Modifying Properties

Always use migrations to modify existing properties rather than changing them directly in mappings, as this ensures proper database schema updates.

## Creating Migrations

### Prerequisites

Ensure you have the EF Core tools installed:

```bash
dotnet tool update --global dotnet-ef
```

### Step 1: Create Migration for Each Provider

Create migrations for all supported database providers. Use descriptive names that reflect the change:

```bash
# SQL Server
dotnet ef migrations add AddExampleEntity --context ZauberDbContext -o "Data/Migrations/SqlServer"

# SQLite
dotnet ef migrations add AddExampleEntity --context SqliteZauberDbContext -o "Data/Migrations/SqLite"

# PostgreSQL
dotnet ef migrations add AddExampleEntity --context PostgreSqlZauberDbContext -o "Data/Migrations/PostgreSql"
```

### Step 2: Review Generated Migrations

Examine the generated migration files to ensure they contain the correct changes:

```csharp
// Example migration file
public partial class AddExampleEntity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ZauberExampleEntities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ZauberExampleEntities", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExampleEntity_Name",
            table: "ZauberExampleEntities",
            column: "Name");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ZauberExampleEntities");
    }
}
```

### Step 3: Update Model Snapshots

The `.Designer.cs` files and model snapshots are automatically updated when you create migrations. These should not be manually edited.

## Migration Best Practices

### Naming Conventions

- **Migration names**: Use PascalCase with descriptive names (e.g., `AddExampleEntity`, `UpdateUserRoles`)
- **Table names**: Always prefix with `Zauber` (e.g., `ZauberUsers`, `ZauberContentTypes`)
- **Index names**: Prefix with `IX_` followed by table name and column (e.g., `IX_GlobalData_Alias`)
- **Foreign key names**: Let EF generate them automatically for consistency

### Migration Guidelines

1. **Always create migrations for all providers** - ZauberCMS supports multiple database providers
2. **Test migrations on all providers** before committing
3. **Use descriptive migration names** that clearly indicate the change
4. **Review generated SQL** to ensure it matches expectations
5. **Never modify existing migrations** - create new ones for additional changes
6. **Include both Up and Down methods** for proper rollback capability

**Remember, migrations are auto applied when the website starts**

### Common Migration Patterns

#### Adding a Column
```csharp
migrationBuilder.AddColumn<string>(
    name: "NewProperty",
    table: "ZauberExampleTable",
    type: "nvarchar(500)",
    maxLength: 500,
    nullable: true,
    defaultValue: "default_value");
```

#### Creating an Index
```csharp
migrationBuilder.CreateIndex(
    name: "IX_ExampleTable_NewProperty",
    table: "ZauberExampleTable",
    column: "NewProperty");
```

#### Adding a Foreign Key
```csharp
migrationBuilder.AddForeignKey(
    name: "FK_ExampleTable_RelatedTable_RelatedId",
    table: "ZauberExampleTable",
    column: "RelatedId",
    principalTable: "ZauberRelatedTable",
    principalColumn: "Id",
    onDelete: ReferentialAction.Cascade);
```

## Troubleshooting

### Common Issues

1. **Migration conflicts**: Ensure all providers have consistent migrations
2. **Model snapshot issues**: Regenerate snapshots if they become out of sync
3. **Provider-specific errors**: Test each provider individually

### Commands

```bash
# Remove last migration (if not applied to production)
dotnet ef migrations remove --context ZauberDbContext

# List all migrations
dotnet ef migrations list --context ZauberDbContext

# Generate SQL script for migration
dotnet ef migrations script --context ZauberDbContext

# Update to specific migration
dotnet ef database update TargetMigration --context ZauberDbContext
```

## Example: Complete Entity Addition

Following the complete pattern for adding a new `BlogPost` entity:

1. **Create entity** (`Data/Models/BlogPost.cs`)
2. **Create mapping** (`Data/Mapping/BlogPostDbMapping.cs`) 
3. **Update interface** (`IZauberDbContext.cs`)
4. **Update base context** (`ZauberDbContextBase.cs`)
5. **Create migrations** for all three providers
6. **Test the changes**

This ensures all database providers are consistently updated and existing installations can be upgraded without data loss.
