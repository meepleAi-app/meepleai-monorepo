using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// #4470 (ADR-095 fetta 7b): le domande di riferimento. Nessuna chiave esterna verso <c>shared_games</c>,
/// come <c>kb_user_feedback</c>: il catalogo cancella in modo logico, e una domanda il cui gioco sparisce
/// deve restare leggibile, non bloccare né portarsi via la riga del gioco.
/// </summary>
internal sealed class ReferenceQuestionConfiguration : IEntityTypeConfiguration<ReferenceQuestion>
{
    public void Configure(EntityTypeBuilder<ReferenceQuestion> builder)
    {
        builder.ToTable("reference_questions", "knowledge_base");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(ReferenceQuestion.CodeMaxLength).IsRequired();
        builder.Property(e => e.SharedGameId).HasColumnName("shared_game_id").IsRequired();
        builder.Property(e => e.Question).HasColumnName("question")
            .HasMaxLength(ReferenceQuestion.QuestionMaxLength).IsRequired();
        builder.Property(e => e.ExpectedAnswer).HasColumnName("expected_answer")
            .HasMaxLength(ReferenceQuestion.ExpectedAnswerMaxLength).IsRequired();
        builder.Property(e => e.Category).HasColumnName("category")
            .HasMaxLength(ReferenceQuestion.CategoryMaxLength).IsRequired();
        builder.Property(e => e.Difficulty).HasColumnName("difficulty")
            .HasMaxLength(ReferenceQuestion.DifficultyMaxLength).IsRequired();
        builder.Property(e => e.QuestionType).HasColumnName("question_type")
            .HasMaxLength(ReferenceQuestion.QuestionTypeMaxLength).IsRequired();
        builder.Property(e => e.SourceReference).HasColumnName("source_reference")
            .HasMaxLength(ReferenceQuestion.SourceReferenceMaxLength).IsRequired();
        builder.Property(e => e.OutOfCorpus).HasColumnName("out_of_corpus").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.InGate).HasColumnName("in_gate").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_reference_questions_code");
        builder.HasIndex(e => e.SharedGameId).HasDatabaseName("IX_reference_questions_shared_game_id");
        builder.Ignore(e => e.DomainEvents);
    }
}
