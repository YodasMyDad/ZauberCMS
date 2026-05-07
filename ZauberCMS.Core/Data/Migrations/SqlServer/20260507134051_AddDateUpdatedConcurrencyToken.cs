using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZauberCMS.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddDateUpdatedConcurrencyToken : Migration
    {
        // Adds the IsConcurrencyToken() annotation to ZauberContent.DateUpdated.
        // This is a model-only metadata change — no DDL is required because EF emits the concurrency
        // check via the WHERE clause on UPDATE/DELETE rather than a database constraint.
        // The body intentionally produces no SQL on apply or rollback.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
