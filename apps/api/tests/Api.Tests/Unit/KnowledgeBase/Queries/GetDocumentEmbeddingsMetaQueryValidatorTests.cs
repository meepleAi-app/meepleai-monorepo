using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetDocumentEmbeddingsMetaQueryValidatorTests
{
    [Fact]
    public void Validator_Rejects_Empty_DocId()
    {
        var validator = new GetDocumentEmbeddingsMetaQueryValidator();
        var result = validator.TestValidate(new GetDocumentEmbeddingsMetaQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(q => q.DocId);
    }

    [Fact]
    public void Validator_Accepts_Valid_DocId()
    {
        var validator = new GetDocumentEmbeddingsMetaQueryValidator();
        var result = validator.TestValidate(new GetDocumentEmbeddingsMetaQuery(Guid.NewGuid()));
        result.ShouldNotHaveValidationErrorFor(q => q.DocId);
    }
}
