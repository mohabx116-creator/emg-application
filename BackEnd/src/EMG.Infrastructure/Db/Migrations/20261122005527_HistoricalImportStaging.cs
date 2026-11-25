using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalImportStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historical_import_batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    MappingVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceApprovalReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_import_batches_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historical_import_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    DeterministicIdentity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SectionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SheetName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SourceCoordinate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceCellsJson = table.Column<string>(type: "text", nullable: false),
                    MappingFamily = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MappingVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BusinessEventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SemanticValidationState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsSummary = table.Column<bool>(type: "boolean", nullable: false),
                    IsDuplicateCandidate = table.Column<bool>(type: "boolean", nullable: false),
                    EligibleForPosting = table.Column<bool>(type: "boolean", nullable: false),
                    ValueFingerprint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_import_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_import_records_historical_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historical_import_audit_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    RecordId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_import_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_import_audit_events_historical_import_batches_Ba~",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_historical_import_audit_events_historical_import_records_Re~",
                        column: x => x.RecordId,
                        principalTable: "historical_import_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "historical_posted_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    HistoricalRecordId = table.Column<int>(type: "integer", nullable: false),
                    DeterministicIdentity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LedgerStream = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BusinessEventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "text", nullable: false),
                    PostedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_posted_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_posted_entries_historical_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historical_posted_entries_historical_import_records_Histori~",
                        column: x => x.HistoricalRecordId,
                        principalTable: "historical_import_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historical_posted_entries_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historical_import_audit_events_BatchId_CreatedAtUtc",
                table: "historical_import_audit_events",
                columns: new[] { "BatchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_historical_import_audit_events_RecordId",
                table: "historical_import_audit_events",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_historical_import_batches_ProjectId_SourceSha256",
                table: "historical_import_batches",
                columns: new[] { "ProjectId", "SourceSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_import_records_BatchId_LifecycleStatus",
                table: "historical_import_records",
                columns: new[] { "BatchId", "LifecycleStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_historical_import_records_DeterministicIdentity",
                table: "historical_import_records",
                column: "DeterministicIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_posted_entries_BatchId",
                table: "historical_posted_entries",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_historical_posted_entries_DeterministicIdentity",
                table: "historical_posted_entries",
                column: "DeterministicIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_posted_entries_HistoricalRecordId",
                table: "historical_posted_entries",
                column: "HistoricalRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_posted_entries_ProjectId_Currency_LedgerStream",
                table: "historical_posted_entries",
                columns: new[] { "ProjectId", "Currency", "LedgerStream" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historical_import_audit_events");

            migrationBuilder.DropTable(
                name: "historical_posted_entries");

            migrationBuilder.DropTable(
                name: "historical_import_records");

            migrationBuilder.DropTable(
                name: "historical_import_batches");
        }
    }
}
