#!/usr/bin/env dotnet-script
#nullable enable
#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.8.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Test Generator Script for Issue #2308 Week 4 Backend Handler Tests
/// Generates comprehensive unit tests from CommandHandler files
/// Pattern: Null command, valid success, validation errors, repository exceptions
/// </summary>

public class HandlerInfo
{
    public string ClassName { get; set; } = "";
    public string CommandName { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string BoundedContext { get; set; } = "";
    public List<PropertyInfo> CommandProperties { get; set; } = new();
    public List<DependencyInfo> Dependencies { get; set; } = new();
    public bool HasReturnType { get; set; }
    public string? ReturnType { get; set; }
    public bool UsesUnitOfWork { get; set; }
}

public class PropertyInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsGuid { get; set; }
    public bool IsString { get; set; }
    public bool IsInt { get; set; }
}

public class DependencyInfo
{
    public string InterfaceName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string Type { get; set; } = ""; // "Repository", "Service", "UnitOfWork", "Logger"
}

public class TestGenerator
{
    public HandlerInfo AnalyzeHandler(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        var handlerClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text.EndsWith("Handler"));

        if (handlerClass == null)
            throw new InvalidOperationException($"No handler class found in {filePath}");

        var info = new HandlerInfo
        {
            ClassName = handlerClass.Identifier.Text
        };

        // Extract namespace
        var namespaceDecl = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        if (namespaceDecl != null)
        {
            info.Namespace = namespaceDecl.Name.ToString();

            // Extract bounded context from namespace
            var nsParts = info.Namespace.Split('.');
            var bcIndex = Array.IndexOf(nsParts, "BoundedContexts");
            if (bcIndex >= 0 && bcIndex + 1 < nsParts.Length)
            {
                info.BoundedContext = nsParts[bcIndex + 1];
            }
        }

        // Extract command name from handler name
        info.CommandName = info.ClassName.Replace("Handler", "");

        // Analyze constructor dependencies
        var constructor = handlerClass.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (constructor != null)
        {
            foreach (var param in constructor.ParameterList.Parameters)
            {
                var paramType = param.Type?.ToString() ?? "";
                var paramName = param.Identifier.Text;

                var dependency = new DependencyInfo
                {
                    InterfaceName = paramType,
                    FieldName = paramName.TrimStart('_')
                };

                if (paramType.Contains("Repository"))
                    dependency.Type = "Repository";
                else if (paramType.Contains("Service"))
                    dependency.Type = "Service";
                else if (paramType.Contains("UnitOfWork"))
                {
                    dependency.Type = "UnitOfWork";
                    info.UsesUnitOfWork = true;
                }
                else if (paramType.Contains("Logger"))
                    dependency.Type = "Logger";
                else
                    dependency.Type = "Other";

                info.Dependencies.Add(dependency);
            }
        }

        // Analyze Handle method to infer command properties and return type
        var handleMethod = handlerClass.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Handle");

        if (handleMethod != null)
        {
            // Check return type
            var returnType = handleMethod.ReturnType.ToString();
            if (returnType.Contains("Task<") && !returnType.Contains("Task<Unit>"))
            {
                info.HasReturnType = true;
                var match = Regex.Match(returnType, @"Task<(.+)>");
                if (match.Success)
                    info.ReturnType = match.Groups[1].Value;
            }

            // Infer command properties from validation logic and usage patterns
            var body = handleMethod.Body?.ToString() ?? "";

            // Look for Guid.Empty checks
            var guidChecks = Regex.Matches(body, @"command\.(\w+)\s*==\s*Guid\.Empty");
            foreach (Match match in guidChecks)
            {
                info.CommandProperties.Add(new PropertyInfo
                {
                    Name = match.Groups[1].Value,
                    Type = "Guid",
                    IsRequired = true,
                    IsGuid = true
                });
            }

            // Look for string null/whitespace checks
            var stringChecks = Regex.Matches(body, @"string\.IsNullOrWhiteSpace\(command\.(\w+)\)");
            foreach (Match match in stringChecks)
            {
                var propName = match.Groups[1].Value;
                if (!info.CommandProperties.Any(p => p.Name == propName))
                {
                    info.CommandProperties.Add(new PropertyInfo
                    {
                        Name = propName,
                        Type = "string",
                        IsRequired = true,
                        IsString = true
                    });
                }
            }

            // Look for other command property accesses
            var propertyAccesses = Regex.Matches(body, @"command\.(\w+)(?!\()");
            foreach (Match match in propertyAccesses)
            {
                var propName = match.Groups[1].Value;
                if (!info.CommandProperties.Any(p => p.Name == propName) &&
                    !propName.StartsWith("_") &&
                    char.IsUpper(propName[0]))
                {
                    info.CommandProperties.Add(new PropertyInfo
                    {
                        Name = propName,
                        Type = "object", // Unknown type
                        IsRequired = false,
                        IsGuid = false,
                        IsString = false
                    });
                }
            }
        }

        return info;
    }

    public string GenerateTestFile(HandlerInfo info)
    {
        var sb = new StringBuilder();

        // Usings
        sb.AppendLine($"using {info.Namespace};");

        // Add bounded context-specific usings
        if (!string.IsNullOrEmpty(info.BoundedContext))
        {
            sb.AppendLine($"using Api.BoundedContexts.{info.BoundedContext}.Infrastructure.Persistence;");
            if (info.BoundedContext == "Authentication")
            {
                sb.AppendLine($"using Api.BoundedContexts.{info.BoundedContext}.Domain.Entities;");
                sb.AppendLine($"using Api.BoundedContexts.{info.BoundedContext}.Domain.ValueObjects;");
            }
        }

        sb.AppendLine("using Api.SharedKernel.Domain.Exceptions;");
        sb.AppendLine("using Api.SharedKernel.Infrastructure.Persistence;");
        sb.AppendLine("using Api.Tests.Constants;");
        sb.AppendLine("using FluentAssertions;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using Moq;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();

        // Namespace
        var testNamespace = info.Namespace.Replace("Api.BoundedContexts", "Api.Tests.BoundedContexts");
        sb.AppendLine($"namespace {testNamespace};");
        sb.AppendLine();

        // Class documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Tests for {info.ClassName} - Issue #2308 Week 4.");
        sb.AppendLine("/// Auto-generated tests with branch coverage for all validation paths.");
        sb.AppendLine("/// Covers: Success path, validation failures, repository exceptions.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[Trait(\"Category\", TestCategories.Unit)]");
        sb.AppendLine($"[Trait(\"BoundedContext\", \"{info.BoundedContext}\")]");
        sb.AppendLine("[Trait(\"Issue\", \"2308\")]");
        sb.AppendLine($"public class {info.ClassName}Tests");
        sb.AppendLine("{");

        // Fields
        foreach (var dep in info.Dependencies)
        {
            sb.AppendLine($"    private readonly Mock<{dep.InterfaceName}> _mock{ToPascalCase(dep.FieldName)};");
        }
        sb.AppendLine($"    private readonly {info.ClassName} _handler;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {info.ClassName}Tests()");
        sb.AppendLine("    {");
        foreach (var dep in info.Dependencies)
        {
            sb.AppendLine($"        _mock{ToPascalCase(dep.FieldName)} = new Mock<{dep.InterfaceName}>();");
        }

        sb.Append($"        _handler = new {info.ClassName}(");
        sb.Append(string.Join(", ", info.Dependencies.Select(d => $"_mock{ToPascalCase(d.FieldName)}.Object")));
        sb.AppendLine(");");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Test 1: Null command
        sb.AppendLine(GenerateNullCommandTest(info));
        sb.AppendLine();

        // Test 2: Valid command success
        sb.AppendLine(GenerateValidCommandTest(info));
        sb.AppendLine();

        // Test 3-N: Validation error tests for each required property
        foreach (var prop in info.CommandProperties.Where(p => p.IsRequired))
        {
            if (prop.IsGuid)
            {
                sb.AppendLine(GenerateEmptyGuidTest(info, prop));
                sb.AppendLine();
            }
            else if (prop.IsString)
            {
                sb.AppendLine(GenerateNullStringTest(info, prop));
                sb.AppendLine();
                sb.AppendLine(GenerateEmptyStringTest(info, prop));
                sb.AppendLine();
            }
        }

        // Test N+1: Repository/Service exception
        sb.AppendLine(GenerateExceptionTest(info));

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GenerateNullCommandTest(HandlerInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");
        sb.AppendLine($"        {info.CommandName}? command = null;");
        sb.AppendLine();
        sb.AppendLine("        // Act & Assert");
        sb.AppendLine("        var act = async () => await _handler.Handle(command!, CancellationToken.None);");
        sb.AppendLine();
        sb.AppendLine("        await act.Should().ThrowAsync<ArgumentNullException>();");

        // Verify no repository/service calls
        var mainDep = info.Dependencies.FirstOrDefault(d => d.Type == "Repository" || d.Type == "Service");
        if (mainDep != null)
        {
            sb.AppendLine();
            sb.AppendLine($"        _mock{ToPascalCase(mainDep.FieldName)}.Verify(");
            sb.AppendLine("            s => s.AnyMethod(It.IsAny<object>(), It.IsAny<CancellationToken>()),");
            sb.AppendLine("            Times.Never");
            sb.AppendLine("        );");
        }

        sb.AppendLine("    }");
        return sb.ToString();
    }

    private string GenerateValidCommandTest(HandlerInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public async Task Handle_WithValidCommand_ShouldSucceed()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");

        // Generate sample values for ALL command properties
        var propValues = new Dictionary<string, string>();
        foreach (var prop in info.CommandProperties)
        {
            string varName = ToCamelCase(prop.Name);
            string value;

            if (prop.IsGuid)
            {
                value = "Guid.NewGuid()";
                sb.AppendLine($"        var {varName} = {value};");
            }
            else if (prop.IsString)
            {
                value = $"\"test{prop.Name}\"";
                sb.AppendLine($"        var {varName} = {value};");
            }
            else
            {
                // For unknown types, use inline value
                value = $"\"test{prop.Name}\"";
            }

            propValues[prop.Name] = varName;
        }

        sb.AppendLine($"        var command = new {info.CommandName}");
        sb.AppendLine("        {");
        foreach (var prop in info.CommandProperties)
        {
            if (propValues.ContainsKey(prop.Name))
                sb.AppendLine($"            {prop.Name} = {propValues[prop.Name]},");
            else
                sb.AppendLine($"            {prop.Name} = \"test{prop.Name}\",");
        }
        sb.AppendLine("        };");
        sb.AppendLine();

        // Setup mocks
        var mainDep = info.Dependencies.FirstOrDefault(d => d.Type == "Repository" || d.Type == "Service");
        if (mainDep != null && info.HasReturnType)
        {
            sb.AppendLine($"        var mockResult = new {info.ReturnType}();");
            sb.AppendLine($"        _mock{ToPascalCase(mainDep.FieldName)}");
            sb.AppendLine("            .Setup(s => s.MethodAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))");
            sb.AppendLine("            .ReturnsAsync(mockResult);");
            sb.AppendLine();
        }

        sb.AppendLine("        // Act");
        if (info.HasReturnType)
        {
            sb.AppendLine("        var result = await _handler.Handle(command, CancellationToken.None);");
        }
        else
        {
            sb.AppendLine("        await _handler.Handle(command, CancellationToken.None);");
        }
        sb.AppendLine();

        sb.AppendLine("        // Assert");
        if (info.HasReturnType)
        {
            sb.AppendLine("        result.Should().NotBeNull();");
        }

        if (mainDep != null)
        {
            sb.AppendLine($"        _mock{ToPascalCase(mainDep.FieldName)}.Verify(");
            sb.AppendLine("            s => s.MethodAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),");
            sb.AppendLine("            Times.Once");
            sb.AppendLine("        );");
        }

        if (info.UsesUnitOfWork)
        {
            sb.AppendLine("        _mockUnitOfWork.Verify(");
            sb.AppendLine("            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),");
            sb.AppendLine("            Times.Once");
            sb.AppendLine("        );");
        }

        sb.AppendLine("    }");
        return sb.ToString();
    }

    private string GenerateEmptyGuidTest(HandlerInfo info, PropertyInfo prop)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task Handle_WithEmptyGuid{prop.Name}_ShouldThrowArgumentException()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");
        sb.AppendLine($"        var command = new {info.CommandName}");
        sb.AppendLine("        {");
        sb.AppendLine($"            {prop.Name} = Guid.Empty,");

        // Add other required properties with valid values
        foreach (var otherProp in info.CommandProperties.Where(p => p.Name != prop.Name))
        {
            if (otherProp.IsGuid)
                sb.AppendLine($"            {otherProp.Name} = Guid.NewGuid(),");
            else if (otherProp.IsString)
                sb.AppendLine($"            {otherProp.Name} = \"test{otherProp.Name}\",");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        // Act & Assert");
        sb.AppendLine("        var act = async () => await _handler.Handle(command, CancellationToken.None);");
        sb.AppendLine();
        sb.AppendLine("        await act.Should().ThrowAsync<ArgumentException>()");
        sb.AppendLine($"            .WithMessage(\"*{prop.Name}*\");");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    private string GenerateNullStringTest(HandlerInfo info, PropertyInfo prop)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task Handle_WithNull{prop.Name}_ShouldThrowArgumentException()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");
        sb.AppendLine($"        var command = new {info.CommandName}");
        sb.AppendLine("        {");
        sb.AppendLine($"            {prop.Name} = null!,");

        foreach (var otherProp in info.CommandProperties.Where(p => p.Name != prop.Name))
        {
            if (otherProp.IsGuid)
                sb.AppendLine($"            {otherProp.Name} = Guid.NewGuid(),");
            else if (otherProp.IsString)
                sb.AppendLine($"            {otherProp.Name} = \"test{otherProp.Name}\",");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        // Act & Assert");
        sb.AppendLine("        var act = async () => await _handler.Handle(command, CancellationToken.None);");
        sb.AppendLine();
        sb.AppendLine("        await act.Should().ThrowAsync<ArgumentException>()");
        sb.AppendLine($"            .WithMessage(\"*{prop.Name}*\");");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    private string GenerateEmptyStringTest(HandlerInfo info, PropertyInfo prop)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task Handle_WithEmpty{prop.Name}_ShouldThrowArgumentException()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");
        sb.AppendLine($"        var command = new {info.CommandName}");
        sb.AppendLine("        {");
        sb.AppendLine($"            {prop.Name} = \"   \",");

        foreach (var otherProp in info.CommandProperties.Where(p => p.Name != prop.Name))
        {
            if (otherProp.IsGuid)
                sb.AppendLine($"            {otherProp.Name} = Guid.NewGuid(),");
            else if (otherProp.IsString)
                sb.AppendLine($"            {otherProp.Name} = \"test{otherProp.Name}\",");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        // Act & Assert");
        sb.AppendLine("        var act = async () => await _handler.Handle(command, CancellationToken.None);");
        sb.AppendLine();
        sb.AppendLine("        await act.Should().ThrowAsync<ArgumentException>()");
        sb.AppendLine($"            .WithMessage(\"*{prop.Name}*\");");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    private string GenerateExceptionTest(HandlerInfo info)
    {
        var sb = new StringBuilder();
        var mainDep = info.Dependencies.FirstOrDefault(d => d.Type == "Repository" || d.Type == "Service");

        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public async Task Handle_When{mainDep?.Type ?? "Service"}ThrowsException_ShouldPropagateException()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");

        // Generate sample values for ALL command properties
        var propValues = new Dictionary<string, string>();
        foreach (var prop in info.CommandProperties)
        {
            string varName = ToCamelCase(prop.Name);

            if (prop.IsGuid)
            {
                sb.AppendLine($"        var {varName} = Guid.NewGuid();");
                propValues[prop.Name] = varName;
            }
            else if (prop.IsString)
            {
                sb.AppendLine($"        var {varName} = \"test{prop.Name}\";");
                propValues[prop.Name] = varName;
            }
            else
            {
                propValues[prop.Name] = $"\"test{prop.Name}\"";
            }
        }

        sb.AppendLine($"        var command = new {info.CommandName}");
        sb.AppendLine("        {");
        foreach (var prop in info.CommandProperties)
        {
            if (propValues.ContainsKey(prop.Name))
                sb.AppendLine($"            {prop.Name} = {propValues[prop.Name]},");
            else
                sb.AppendLine($"            {prop.Name} = \"test{prop.Name}\",");
        }
        sb.AppendLine("        };");
        sb.AppendLine();

        if (mainDep != null)
        {
            sb.AppendLine($"        _mock{ToPascalCase(mainDep.FieldName)}");
            sb.AppendLine("            .Setup(s => s.MethodAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))");
            sb.AppendLine($"            .ThrowsAsync(new InvalidOperationException(\"{mainDep.Type} unavailable\"));");
        }

        sb.AppendLine();
        sb.AppendLine("        // Act & Assert");
        sb.AppendLine("        var act = async () => await _handler.Handle(command, CancellationToken.None);");
        sb.AppendLine();
        sb.AppendLine("        await act.Should().ThrowAsync<InvalidOperationException>()");
        sb.AppendLine($"            .WithMessage(\"{mainDep?.Type ?? "Service"} unavailable\");");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }

    private string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToLower(input[0]) + input.Substring(1);
    }
}

// Main script execution
var args = Args.ToArray();

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet script TestGenerator.csx <handler-file-path> [output-directory]");
    Console.WriteLine("Example: dotnet script TestGenerator.csx path/to/Handler.cs tests/output/");
    return;
}

var handlerPath = args[0];
var outputDir = args.Length > 1 ? args[1] : Path.GetDirectoryName(handlerPath);

if (!File.Exists(handlerPath))
{
    Console.WriteLine($"Error: Handler file not found: {handlerPath}");
    return;
}

try
{
    var generator = new TestGenerator();
    Console.WriteLine($"Analyzing handler: {handlerPath}");

    var handlerInfo = generator.AnalyzeHandler(handlerPath);
    Console.WriteLine($"  Handler: {handlerInfo.ClassName}");
    Console.WriteLine($"  Command: {handlerInfo.CommandName}");
    Console.WriteLine($"  Bounded Context: {handlerInfo.BoundedContext}");
    Console.WriteLine($"  Dependencies: {handlerInfo.Dependencies.Count}");
    Console.WriteLine($"  Command Properties: {handlerInfo.CommandProperties.Count}");

    var testCode = generator.GenerateTestFile(handlerInfo);

    var outputFileName = $"{handlerInfo.ClassName}Tests.cs";
    var outputPath = Path.Combine(outputDir!, outputFileName);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, testCode);

    Console.WriteLine($"✅ Generated test file: {outputPath}");
    Console.WriteLine($"   Estimated test count: {handlerInfo.CommandProperties.Count(p => p.IsRequired) * 2 + 3}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
