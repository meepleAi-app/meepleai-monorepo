using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class UpdateHandSlotCommandValidator : AbstractValidator<UpdateHandSlotCommand>
{
    private static readonly HashSet<string> ValidSlotTypes = new(StringComparer.OrdinalIgnoreCase)
        { "toolkit", "game", "session", "ai" };

    private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        { "toolkit", "game", "session", "agent" };

    private static readonly Dictionary<string, HashSet<string>> SlotEntityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["toolkit"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "toolkit" },
        ["game"]    = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "game" },
        ["session"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "session" },
        ["ai"]      = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "agent" }
    };

    public UpdateHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", ValidSlotTypes)}");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("EntityId is required");

        RuleFor(x => x.EntityType)
            .NotEmpty()
            .Must(t => ValidEntityTypes.Contains(t))
            .WithMessage($"EntityType must be one of: {string.Join(", ", ValidEntityTypes)}");

        RuleFor(x => x)
            .Must(x => SlotEntityMap.TryGetValue(x.SlotType, out var allowed) && allowed.Contains(x.EntityType))
            .WithMessage(x => $"EntityType '{x.EntityType}' is not valid for SlotType '{x.SlotType}'")
            .When(x => ValidSlotTypes.Contains(x.SlotType) && ValidEntityTypes.Contains(x.EntityType));
    }
}
