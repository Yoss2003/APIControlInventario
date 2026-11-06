using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InventoryAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentsStockOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecipientUserId = table.Column<int>(type: "integer", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvidencePath = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleAssignments_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticleAssignments_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticleAssignments_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovementCancellations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalMovementId = table.Column<int>(type: "integer", nullable: false),
                    ReversalMovementId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementCancellations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleCancellations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleCancellations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleAssignmentEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MovementId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleAssignmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleAssignmentEvents_ArticleAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ArticleAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignmentEvents_AssignmentId",
                table: "ArticleAssignmentEvents",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignmentEvents_OperationKey",
                table: "ArticleAssignmentEvents",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignments_ArticleId",
                table: "ArticleAssignments",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignments_CompanyId_InventoryId_ArticleId",
                table: "ArticleAssignments",
                columns: new[] { "CompanyId", "InventoryId", "ArticleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignments_CompanyId_RecipientUserId",
                table: "ArticleAssignments",
                columns: new[] { "CompanyId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignments_InventoryId",
                table: "ArticleAssignments",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignments_OperationKey",
                table: "ArticleAssignments",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAssignments_RecipientUserId",
                table: "ArticleAssignments",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementCancellations_OriginalMovementId",
                table: "MovementCancellations",
                column: "OriginalMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementCancellations_ReversalMovementId",
                table: "MovementCancellations",
                column: "ReversalMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleCancellations_SaleId",
                table: "SaleCancellations",
                column: "SaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleOperations_OperationKey",
                table: "SaleOperations",
                column: "OperationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleAssignmentEvents");

            migrationBuilder.DropTable(
                name: "MovementCancellations");

            migrationBuilder.DropTable(
                name: "SaleCancellations");

            migrationBuilder.DropTable(
                name: "SaleOperations");

            migrationBuilder.DropTable(
                name: "ArticleAssignments");
        }
    }
}
