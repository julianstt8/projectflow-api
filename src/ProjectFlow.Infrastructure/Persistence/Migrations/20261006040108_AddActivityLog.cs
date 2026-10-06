using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    old_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    new_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_activity_logs_projects_project_id_organization_id",
                        columns: x => new { x.project_id, x.organization_id },
                        principalTable: "projects",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activity_logs_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_actor_id",
                table: "activity_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_organization_id_entity_id",
                table: "activity_logs",
                columns: new[] { "organization_id", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_organization_id_project_id_created_at",
                table: "activity_logs",
                columns: new[] { "organization_id", "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_project_id_organization_id",
                table: "activity_logs",
                columns: new[] { "project_id", "organization_id" });

            // RF-10: the activity log is insert-only, enforced by the database itself.
            migrationBuilder.Sql("""
                CREATE FUNCTION activity_logs_insert_only() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'activity_logs is insert-only: % is not allowed', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER activity_logs_insert_only
                BEFORE UPDATE OR DELETE ON activity_logs
                FOR EACH ROW EXECUTE FUNCTION activity_logs_insert_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS activity_logs_insert_only ON activity_logs;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS activity_logs_insert_only();");

            migrationBuilder.DropTable(
                name: "activity_logs");
        }
    }
}
