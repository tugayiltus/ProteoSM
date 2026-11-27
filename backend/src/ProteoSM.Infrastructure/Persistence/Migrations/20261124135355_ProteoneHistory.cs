using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <summary>
/// The former product name in sent emails, the pilot category and the audit trail (owner decision, ADR-0037).
/// The audit table is append-only: its trigger is off only inside this transaction, and the replacement leaves its own
/// audit row. A name after "=" is part of a directory path and stays, because it names an object outside this product.
/// </summary>
public partial class ProteoneHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE email_deliveries SET subject = replace(subject, 'TGYSM', 'Proteone'), body = replace(body, 'TGYSM', 'Proteone')
            WHERE subject LIKE '%TGYSM%' OR body LIKE '%TGYSM%';

            UPDATE categories c SET name = 'Proteone Pilot', normalized_name = 'proteone pilot'
            WHERE c.name = 'TGYSM Pilot'
              AND NOT EXISTS (SELECT 1 FROM categories o WHERE o.normalized_name = 'proteone pilot');

            ALTER TABLE audit_events DISABLE TRIGGER audit_events_append_only;

            WITH replaced AS (
                UPDATE audit_events SET
                    reason = regexp_replace(reason, '(?<!=)TGYSM', 'Proteone', 'g'),
                    old_values = regexp_replace(old_values::text, '(?<!=)TGYSM', 'Proteone', 'g')::jsonb,
                    new_values = regexp_replace(new_values::text, '(?<!=)TGYSM', 'Proteone', 'g')::jsonb
                WHERE reason ~ '(?<!=)TGYSM' OR old_values::text ~ '(?<!=)TGYSM' OR new_values::text ~ '(?<!=)TGYSM'
                RETURNING id)
            INSERT INTO audit_events (id, occurred_at, action, entity_type, reason, new_values, correlation_id)
            SELECT gen_random_uuid(), now(), 'audit.product_name_replaced', 'audit',
                   'Product name replaced in earlier audit rows (ADR-0037).', jsonb_build_object('rows', count(*)), 'migration'
            FROM replaced HAVING count(*) > 0;

            ALTER TABLE audit_events ENABLE TRIGGER audit_events_append_only;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Text only: which texts held the former name is not known afterwards.
    }
}
