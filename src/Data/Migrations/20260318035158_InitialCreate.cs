using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CopilotPluginApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    userId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    sessionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    requestId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    prompt_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    completion_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    model_used = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    latency_ms = table.Column<double>(type: "REAL", nullable: false),
                    cache_hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    degraded = table.Column<bool>(type: "INTEGER", nullable: false),
                    cost_estimate_usd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_userId_created_at",
                table: "audit_logs",
                columns: new[] { "userId", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}
