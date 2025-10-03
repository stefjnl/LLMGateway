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
                    model_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    model_used = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_pricing");

            migrationBuilder.DropTable(
                name: "request_logs");
        }
    }
}
