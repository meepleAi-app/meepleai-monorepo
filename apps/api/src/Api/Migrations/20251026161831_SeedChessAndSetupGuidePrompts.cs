using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedChessAndSetupGuidePrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADMIN-01 Phase 3: Seed chess-system-prompt template
            var chessTemplateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
            var chessVersionId = Guid.Parse("10000000-0000-0000-0000-000000000002");
            var setupTemplateId = Guid.Parse("20000000-0000-0000-0000-000000000001");
            var setupVersionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
            var seedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // Admin user from demo seed

            // Chess System Prompt Template
            migrationBuilder.Sql($@"
                INSERT INTO prompt_templates (id, name, description, category, is_active, created_at, updated_at, created_by_id)
                VALUES ('{chessTemplateId}', 'chess-system-prompt', 'System prompt for Chess AI agent - handles rules, tactics, and position analysis', 'chess', true, NOW(), NOW(), '{seedUserId}')
                ON CONFLICT (id) DO NOTHING;
            ");

            migrationBuilder.Sql($@"
                INSERT INTO prompt_versions (id, template_id, version_number, content, metadata, is_active, created_at, created_by_id)
                VALUES (
                    '{chessVersionId}',
                    '{chessTemplateId}',
                    1,
                    'You are a specialized chess AI assistant with deep knowledge of chess rules, openings, tactics, and strategies.

YOUR ROLE:
- Answer chess questions based ONLY on the provided context from the chess knowledge base
- Explain chess concepts clearly and concisely
- Suggest tactical and strategic moves when relevant
- Reference specific sources when providing information

CRITICAL INSTRUCTIONS:
- If the answer is clearly found in the provided context, answer it accurately
- If the answer is NOT in the context or you''re uncertain, respond with: ""Not specified in chess knowledge base""
- Do NOT hallucinate or invent information
- Keep answers concise (3-5 sentences for explanations, 2-3 sentences for direct questions)
- When suggesting moves, always explain WHY the move is good

RESPONSE FORMAT:
- Start with a direct answer to the question
- If analyzing a position, provide a brief evaluation
- List suggested moves in format: ""1. Move notation: Explanation""
- End with ""Sources: [1] [2]..."" to cite your sources',
                    '{{""purpose"": ""chess_agent"", ""dynamic_sections"": [""fen_position_analysis"", ""fen_validation_warning""]}}',
                    true,
                    NOW(),
                    '{seedUserId}'
                )
                ON CONFLICT (id) DO NOTHING;
            ");

            migrationBuilder.Sql($@"
                INSERT INTO prompt_audit_logs (template_id, version_id, action, performed_by_id, performed_at, changes)
                VALUES (
                    '{chessTemplateId}',
                    '{chessVersionId}',
                    'activated',
                    '{seedUserId}',
                    NOW(),
                    '{{""reason"": ""Initial seed migration (ADMIN-01 Phase 3)"", ""version"": 1}}'
                );
            ");

            // Setup Guide System Prompt Template
            migrationBuilder.Sql($@"
                INSERT INTO prompt_templates (id, name, description, category, is_active, created_at, updated_at, created_by_id)
                VALUES ('{setupTemplateId}', 'setup-guide-system-prompt', 'System prompt for board game setup guide generation - creates step-by-step setup instructions', 'setup', true, NOW(), NOW(), '{seedUserId}')
                ON CONFLICT (id) DO NOTHING;
            ");

            migrationBuilder.Sql($@"
                INSERT INTO prompt_versions (id, template_id, version_number, content, metadata, is_active, created_at, created_by_id)
                VALUES (
                    '{setupVersionId}',
                    '{setupTemplateId}',
                    1,
                    'You are a board game setup assistant. Your job is to create clear, actionable setup instructions based ONLY on the provided rulebook context.

CRITICAL INSTRUCTIONS:
- Generate 3-7 numbered setup steps in a logical order
- Each step should be concrete and actionable (e.g., ''Place the board in the center'')
- Keep each step instruction concise (1-2 sentences maximum)
- Mark optional steps with ''[OPTIONAL]'' prefix in the title
- Use information ONLY from the provided context
- If the context is insufficient, generate generic but helpful setup steps
- Return ONLY the steps in this exact format:

STEP 1: <title>
<instruction>

STEP 2: <title>
<instruction>

etc.',
                    '{{""purpose"": ""setup_guide_generation""}}',
                    true,
                    NOW(),
                    '{seedUserId}'
                )
                ON CONFLICT (id) DO NOTHING;
            ");

            migrationBuilder.Sql($@"
                INSERT INTO prompt_audit_logs (template_id, version_id, action, performed_by_id, performed_at, changes)
                VALUES (
                    '{setupTemplateId}',
                    '{setupVersionId}',
                    'activated',
                    '{seedUserId}',
                    NOW(),
                    '{{""reason"": ""Initial seed migration (ADMIN-01 Phase 3)"", ""version"": 1}}'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ADMIN-01 Phase 3: Rollback seed data (delete in reverse order due to FK constraints)
            var chessTemplateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
            var setupTemplateId = Guid.Parse("20000000-0000-0000-0000-000000000001");

            // Delete audit logs first
            migrationBuilder.Sql($"DELETE FROM prompt_audit_logs WHERE template_id IN ('{chessTemplateId}', '{setupTemplateId}');");

            // Delete versions
            migrationBuilder.Sql($"DELETE FROM prompt_versions WHERE template_id IN ('{chessTemplateId}', '{setupTemplateId}');");

            // Delete templates
            migrationBuilder.Sql($"DELETE FROM prompt_templates WHERE id IN ('{chessTemplateId}', '{setupTemplateId}');");
        }
    }
}
