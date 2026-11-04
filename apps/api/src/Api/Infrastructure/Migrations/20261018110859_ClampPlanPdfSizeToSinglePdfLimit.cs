using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4449: a plan's PDF size is a commercial cap under the single PDF limit (104 857 600 bytes,
    /// <c>PdfProcessing:MaxFileSizeBytes</c>), and the plan editor now refuses values above it.
    /// </summary>
    /// <remarks>
    /// The premium plan was seeded at 200 MB. No upload ever accepted that much — every path stopped
    /// at 100 MB — so lowering the stored value changes no user's effective limit. Without it, any
    /// edit of the premium plan would be refused until the admin lowered the PDF size by hand, and the
    /// editor would keep showing a size no upload accepts. Data only: no schema change.
    /// </remarks>
    public partial class ClampPlanPdfSizeToSinglePdfLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE tier_definitions SET max_pdf_size_bytes = 104857600 WHERE max_pdf_size_bytes > 104857600;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the values above the limit were never applied by any upload path.
        }
    }
}
