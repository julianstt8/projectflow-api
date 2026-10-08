using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectFlow.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Label names are unique per project ignoring case. EF Core cannot model an index on an expression,
    /// so it is created with SQL (the model does not change).
    /// </summary>
    public partial class AddLabelNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_labels_project_id_lower_name ON labels (project_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_labels_project_id_lower_name;");
        }
    }
}
