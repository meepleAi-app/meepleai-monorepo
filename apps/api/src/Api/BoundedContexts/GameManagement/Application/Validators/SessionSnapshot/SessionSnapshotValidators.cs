using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.SessionSnapshot;

internal sealed class RestoreSessionSnapshotCommandValidator : AbstractValidator<RestoreSessionSnapshotCommand>
{
    public RestoreSessionSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.SnapshotIndex).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateSnapshotCommandValidator : AbstractValidator<CreateSnapshotCommand>
{
    public CreateSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TriggerDescription).MaximumLength(500);
    }
}

internal sealed class GetSnapshotsQueryValidator : AbstractValidator<GetSnapshotsQuery>
{
    public GetSnapshotsQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

internal sealed class GetSnapshotStateQueryValidator : AbstractValidator<GetSnapshotStateQuery>
{
    public GetSnapshotStateQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.SnapshotIndex).GreaterThanOrEqualTo(0);
    }
}
