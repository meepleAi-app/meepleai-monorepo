using Api.BoundedContexts.GameManagement.Application.Commands.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.ToolState;

internal sealed class InitializeToolStatesCommandValidator : AbstractValidator<InitializeToolStatesCommand>
{
    public InitializeToolStatesCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ToolkitId).NotEmpty();
    }
}

internal sealed class RollDiceCommandValidator : AbstractValidator<RollDiceCommand>
{
    public RollDiceCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ToolName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateCounterCommandValidator : AbstractValidator<UpdateCounterCommand>
{
    public UpdateCounterCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ToolName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PlayerId).NotEmpty().MaximumLength(100);
    }
}

internal sealed class GetToolStatesQueryValidator : AbstractValidator<GetToolStatesQuery>
{
    public GetToolStatesQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

internal sealed class GetToolStateQueryValidator : AbstractValidator<GetToolStateQuery>
{
    public GetToolStateQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ToolName).NotEmpty().MaximumLength(200);
    }
}
