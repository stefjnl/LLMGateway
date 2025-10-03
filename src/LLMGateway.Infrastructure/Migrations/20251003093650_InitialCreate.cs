using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LLMGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_cost_per_1m_tokens = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    output_cost_per_1m_tokens = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    max_context_tokens = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_pricing", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "request_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    model_used = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    response_time_ms = table.Column<long>(type: "bigint", nullable: false),
                    was_fallback = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_logs_provider_name",
                table: "request_logs",
                column: "provider_name");

            migrationBuilder.CreateIndex(
                name: "ix_request_logs_timestamp",
                table: "request_logs",
                column: "timestamp");

            // Seed data for model pricing
            migrationBuilder.InsertData(
                table: "model_pricing",
                columns: new[] { "id", "model_name", "model_provider", "provider_name", "input_cost_per_1m_tokens", "output_cost_per_1m_tokens", "max_context_tokens", "updated_at" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "z-ai/glm-4.6", "z-ai", "OpenRouter", 0.0001m, 0.0002m, 128000, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "deepseek-ai/DeepSeek-V3.1-Terminus", "deepseek-ai", "OpenRouter", 0.0003m, 0.0005m, 64000, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "moonshotai/Kimi-K2-Instruct-0905", "moonshotai", "OpenRouter", 0.0005m, 0.0010m, 200000, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seed data
            migrationBuilder.DeleteData(
                table: "model_pricing",
                keyColumn: "id",
                keyValues: new object[]
                {
                    new Guid("11111111-1111-1111-1111-111111111111"),
                    new Guid("22222222-2222-2222-2222-222222222222"),
                    new Guid("33333333-3333-3333-3333-333333333333")
                });

            migrationBuilder.DropTable(
                name: "model_pricing");

            migrationBuilder.DropTable(
                name: "request_logs");
        }
    }
}
