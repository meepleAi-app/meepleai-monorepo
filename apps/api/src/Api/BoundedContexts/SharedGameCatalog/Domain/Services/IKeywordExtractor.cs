namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

public interface IKeywordExtractor
{
    string[] Extract(string text);
}
