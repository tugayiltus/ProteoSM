using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProblems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "problems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    search_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    impact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    owner_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    root_cause = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    workaround = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    article_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    known_error_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problems", x => x.id);
                    table.CheckConstraint("ck_problems_impact", "impact IN ('high', 'medium', 'low')");
                    table.CheckConstraint("ck_problems_status", "status IN ('open', 'investigating', 'known_error', 'resolved', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_problems_knowledge_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "knowledge_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problems_teams_owner_team_id",
                        column: x => x.owner_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problems_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problems_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "problem_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problem_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_problem_changes_change_requests_change_id",
                        column: x => x.change_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problem_changes_problems_problem_id",
                        column: x => x.problem_id,
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problem_changes_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "problem_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problem_events", x => x.id);
                    table.CheckConstraint("ck_problem_events_kind", "kind IN ('created', 'updated', 'started', 'known_error', 'resolved', 'cancelled', 'commented', 'transferred', 'owner_changed', 'ticket_linked', 'ticket_unlinked', 'change_linked', 'change_unlinked', 'article_created')");
                    table.ForeignKey(
                        name: "fk_problem_events_problems_problem_id",
                        column: x => x.problem_id,
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problem_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "problem_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problem_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_problem_tickets_problems_problem_id",
                        column: x => x.problem_id,
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problem_tickets_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problem_tickets_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_problem_changes_change_id",
                table: "problem_changes",
                column: "change_id");

            migrationBuilder.CreateIndex(
                name: "ix_problem_changes_created_by_id",
                table: "problem_changes",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_problem_changes_problem_change",
                table: "problem_changes",
                columns: new[] { "problem_id", "change_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_problem_events_actor_user_id",
                table: "problem_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_problem_events_problem_id_occurred_at",
                table: "problem_events",
                columns: new[] { "problem_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_problem_tickets_created_by_id",
                table: "problem_tickets",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_problem_tickets_problem_id",
                table: "problem_tickets",
                column: "problem_id");

            migrationBuilder.CreateIndex(
                name: "ux_problem_tickets_ticket_id",
                table: "problem_tickets",
                column: "ticket_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_problems_created_by_id",
                table: "problems",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_problems_number",
                table: "problems",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_problems_owner_id_status",
                table: "problems",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_problems_owner_team_id_status",
                table: "problems",
                columns: new[] { "owner_team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_problems_search_text",
                table: "problems",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_problems_updated_at_id",
                table: "problems",
                columns: new[] { "updated_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_problems_article_id",
                table: "problems",
                column: "article_id",
                unique: true,
                filter: "article_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "problem_changes");

            migrationBuilder.DropTable(
                name: "problem_events");

            migrationBuilder.DropTable(
                name: "problem_tickets");

            migrationBuilder.DropTable(
                name: "problems");
        }
    }
}
