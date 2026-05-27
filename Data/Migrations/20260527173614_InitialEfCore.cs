using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CareerVault.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialEfCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "career_vault");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "professional_entries",
                schema: "career_vault",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    source_external_id = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    company = table.Column<string>(type: "text", nullable: true),
                    project = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    technologies = table.Column<string[]>(type: "text[]", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    raw_payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    embedding_status = table.Column<string>(type: "text", nullable: false),
                    embedding_model = table.Column<string>(type: "text", nullable: true),
                    embedding_dimensions = table.Column<int>(type: "integer", nullable: true),
                    embedding_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    embedding_error = table.Column<string>(type: "text", nullable: true),
                    notion_sync_status = table.Column<string>(type: "text", nullable: false),
                    notion_page_id = table.Column<string>(type: "text", nullable: true),
                    notion_last_error = table.Column<string>(type: "text", nullable: true),
                    notion_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "professional_entry_embeddings",
                schema: "career_vault",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    dimensions = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_entry_embeddings", x => x.id);
                    table.ForeignKey(
                        name: "FK_professional_entry_embeddings_professional_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "career_vault",
                        principalTable: "professional_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_professional_entries_content_hash",
                schema: "career_vault",
                table: "professional_entries",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "ix_professional_entries_embedding_status_created_at",
                schema: "career_vault",
                table: "professional_entries",
                columns: new[] { "embedding_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_professional_entry_embeddings_embedding_hnsw",
                schema: "career_vault",
                table: "professional_entry_embeddings",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "uq_professional_entry_embedding_entry_model",
                schema: "career_vault",
                table: "professional_entry_embeddings",
                columns: new[] { "entry_id", "model" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "professional_entry_embeddings",
                schema: "career_vault");

            migrationBuilder.DropTable(
                name: "professional_entries",
                schema: "career_vault");
        }
    }
}
