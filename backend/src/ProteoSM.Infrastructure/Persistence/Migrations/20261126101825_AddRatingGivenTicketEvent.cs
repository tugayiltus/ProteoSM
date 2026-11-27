using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRatingGivenTicketEvent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events");

        migrationBuilder.AddCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events",
            sql: "kind IN ('created', 'assigned', 'transferred', 'status_changed', 'public_comment', 'internal_note', 'priority_changed', 'approval_requested', 'approval_decided', 'approval_voided', 'approval_reassigned', 'related_request_created', 'asset_linked', 'asset_unlinked', 'completed', 'cancelled', 'change_linked', 'change_unlinked', 'problem_linked', 'problem_unlinked', 'service_set', 'service_cleared', 'rating_given')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events");

        migrationBuilder.AddCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events",
            sql: "kind IN ('created', 'assigned', 'transferred', 'status_changed', 'public_comment', 'internal_note', 'priority_changed', 'approval_requested', 'approval_decided', 'approval_voided', 'approval_reassigned', 'related_request_created', 'asset_linked', 'asset_unlinked', 'completed', 'cancelled', 'change_linked', 'change_unlinked', 'problem_linked', 'problem_unlinked', 'service_set', 'service_cleared')");
    }
}
