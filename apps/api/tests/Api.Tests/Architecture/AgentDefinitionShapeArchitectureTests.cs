using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #4138 — the shape of <see cref="AgentDefinition"/> after the single-system-agent
/// decision.
/// <para>
/// One agent, of the system, configured by the admin and used by everyone. It is NOT tied to a
/// game: the retrieval scope is a parameter of the question, not a property of the agent. The
/// fields that said otherwise are being retired, and the measures on the issue say why they never
/// mattered — <c>AskQuestionQuery</c> carries no agent id, and <c>AskQuestionQueryHandler</c> does
/// not mention <c>AgentDefinition</c>.
/// </para>
/// <para>
/// This exists because deleting a field is not the same as keeping it deleted. A unit test can
/// only assert what the type does; these assert what it must NOT have, which is the only form that
/// fails when someone adds it back. The issue asks for exactly that: "un test che fallirebbe se un
/// campo di scoping (GameId o KbCardIds) tornasse su AgentDefinition".
/// </para>
/// <para>
/// ⚠️ Private fields are checked too, not just public members. The retired type lived as
/// <c>_typeValue</c> / <c>_typeDescription</c> and was surfaced through a computed property, with
/// EF mapping the FIELDS (<c>builder.Property&lt;string&gt;("_typeValue")</c>). A public-only check
/// would have passed while the column came back.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4138")]
public sealed class AgentDefinitionShapeArchitectureTests
{
    private static readonly Type s_agentDefinition = typeof(AgentDefinition);

    /// <summary>
    /// Members retired with the single-agent decision. Add a row here when a further field goes;
    /// the remaining ones (<c>GameId</c>, <c>KbCardIds</c>, <c>Strategy</c>) are still on the
    /// aggregate and are NOT listed yet — listing them now would make this suite red for work
    /// that has not happened.
    /// </summary>
    public static TheoryData<string> RetiredMembers() => new()
    {
        "Type",
        "UpdateType",
        "RaiseUserCreatedEvent",
    };

    /// <summary>
    /// Private backing fields retired with those members. EF mapped these by name, so a column
    /// cannot come back without one of them coming back first.
    /// </summary>
    public static TheoryData<string> RetiredFields() => new()
    {
        "_typeValue",
        "_typeDescription",
    };

    [Theory]
    [MemberData(nameof(RetiredMembers))]
    public void RetiredMember_IsNotDeclared(string memberName)
    {
        var found = s_agentDefinition
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, memberName, StringComparison.Ordinal))
            .ToList();

        found.Should().BeEmpty(
            $"AgentDefinition.{memberName} was retired by #4138. If the decision changed, amend " +
            "the ADR first — a single system agent that is not tied to a game has no type, no " +
            "owning game and no user-creation event.");
    }

    [Theory]
    [MemberData(nameof(RetiredFields))]
    public void RetiredBackingField_IsNotDeclared(string fieldName)
    {
        var found = s_agentDefinition.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

        found.Should().BeNull(
            $"the private field {fieldName} was retired by #4138. EF mapped it by name " +
            "(builder.Property<string>(\"...\")), so re-adding the field is what brings the column " +
            "back — the public surface can look clean while the schema is not.");
    }

    /// <summary>
    /// The factories must not take a type. Asserted separately from the member check because a
    /// parameter is how the field would be re-populated even if the property stayed away.
    /// </summary>
    [Theory]
    [InlineData(nameof(AgentDefinition.Create))]
    [InlineData(nameof(AgentDefinition.CreateSystem))]
    public void Factory_TakesNoTypeParameter(string factoryName)
    {
        var parameters = s_agentDefinition
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, factoryName, StringComparison.Ordinal))
            .SelectMany(m => m.GetParameters())
            .Select(param => param.Name)
            .ToList();

        parameters.Should().NotBeEmpty($"{factoryName} must exist — otherwise this test passes for nothing");
        parameters.Should().NotContain(
            "type",
            $"AgentDefinition.{factoryName} must not take an agent type: #4138 retired it.");
    }
}
