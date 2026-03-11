namespace Api.BoundedContexts.UserNotifications.Domain;

/// <summary>
/// Constants for well-known email template placeholder tokens.
/// Use these in template HTML bodies: e.g. "Hello {{userName}}, your file {{fileName}} is ready."
/// Issue #52: P4.1 Domain entity for admin email template management.
/// </summary>
internal static class EmailTemplatePlaceholders
{
    // Common
    public const string UserName = "{{userName}}";
    public const string AppUrl = "{{appUrl}}";
    public const string UnsubscribeUrl = "{{unsubscribeUrl}}";

    // PDF
    public const string FileName = "{{fileName}}";
    public const string DocumentUrl = "{{documentUrl}}";

    // Game Night
    public const string EventTitle = "{{eventTitle}}";
    public const string EventDate = "{{eventDate}}";
    public const string OrganizerName = "{{organizerName}}";
    public const string EventLocation = "{{eventLocation}}";
    public const string RsvpUrl = "{{rsvpUrl}}";

    // Admin
    public const string AlertType = "{{alertType}}";
    public const string AlertMessage = "{{alertMessage}}";

    // Share Request
    public const string GameTitle = "{{gameTitle}}";
    public const string RequestStatus = "{{requestStatus}}";
}
