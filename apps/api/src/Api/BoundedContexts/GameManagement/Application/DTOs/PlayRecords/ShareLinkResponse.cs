namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>Share-link response for a play record (#2437-2).</summary>
public record ShareLinkResponse(string ShareToken, string ShareUrl);
