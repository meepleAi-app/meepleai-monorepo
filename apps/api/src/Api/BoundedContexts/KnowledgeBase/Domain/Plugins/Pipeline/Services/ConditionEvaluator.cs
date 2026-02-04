// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Services;

/// <summary>
/// Evaluates condition expressions for pipeline edge routing.
/// Supports simple DSL: "always", "never", "confidence >= 0.7", "output.type == 'rules'"
/// </summary>
public sealed class ConditionEvaluator
{
    /// <summary>
    /// Evaluates a condition expression against a plugin output.
    /// </summary>
    /// <param name="condition">The condition expression to evaluate.</param>
    /// <param name="output">The plugin output to evaluate against.</param>
    /// <returns>True if the condition is met, false otherwise.</returns>
    public bool Evaluate(string? condition, PluginOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        // Null or empty condition means always true
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var trimmed = condition.Trim().ToLowerInvariant();

        // Simple keywords
        if (trimmed is "always" or "true")
        {
            return true;
        }

        if (trimmed is "never" or "false")
        {
            return false;
        }

        // Parse expression
        try
        {
            return EvaluateExpression(condition, output);
        }
        catch
        {
            // If parsing fails, default to true (fail-open for routing)
            return true;
        }
    }

    private static bool EvaluateExpression(string expression, PluginOutput output)
    {
        // Split by operators
        var operators = new[] { ">=", "<=", "!=", "==", ">", "<" };

        foreach (var op in operators)
        {
            var index = expression.IndexOf(op, StringComparison.Ordinal);
            if (index > 0)
            {
                var left = expression[..index].Trim();
                var right = expression[(index + op.Length)..].Trim();
                return EvaluateComparison(left, op, right, output);
            }
        }

        // No operator found, try evaluating as a boolean field
        var value = GetFieldValue(expression, output);
        if (value is bool b)
        {
            return b;
        }

        // If we couldn't parse a valid expression, fail-open for routing
        return true;
    }

    private static bool EvaluateComparison(string left, string op, string right, PluginOutput output)
    {
        var leftValue = GetFieldValue(left, output);
        var rightValue = ParseLiteral(right);

        // Handle null comparisons
        if (leftValue == null || rightValue == null)
        {
            return op switch
            {
                "==" => leftValue == null && rightValue == null,
                "!=" => leftValue != null || rightValue != null,
                _ => false
            };
        }

        // String comparison
        if (leftValue is string leftStr && rightValue is string rightStr)
        {
            return op switch
            {
                "==" => string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        // Numeric comparison
        if (TryConvertToDouble(leftValue, out var leftNum) && TryConvertToDouble(rightValue, out var rightNum))
        {
            return op switch
            {
                "==" => Math.Abs(leftNum - rightNum) < 0.0001,
                "!=" => Math.Abs(leftNum - rightNum) >= 0.0001,
                ">" => leftNum > rightNum,
                ">=" => leftNum >= rightNum,
                "<" => leftNum < rightNum,
                "<=" => leftNum <= rightNum,
                _ => false
            };
        }

        // Boolean comparison
        if (leftValue is bool leftBool && rightValue is bool rightBool)
        {
            return op switch
            {
                "==" => leftBool == rightBool,
                "!=" => leftBool != rightBool,
                _ => false
            };
        }

        return false;
    }

    private static object? GetFieldValue(string fieldPath, PluginOutput output)
    {
        var path = fieldPath.Trim().ToLowerInvariant();

        // Direct properties
        return path switch
        {
            "success" => output.Success,
            "confidence" => output.Confidence,
            "errormessage" => output.ErrorMessage,
            "errorcode" => output.ErrorCode,
            _ => GetJsonFieldValue(fieldPath, output.Result)
        };
    }

    private static object? GetJsonFieldValue(string fieldPath, JsonDocument? result)
    {
        if (result == null)
        {
            return null;
        }

        // Handle paths like "output.type" or "result.score"
        var parts = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var startIndex = parts[0].Equals("output", StringComparison.OrdinalIgnoreCase) ||
                         parts[0].Equals("result", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        var element = result.RootElement;

        for (var i = startIndex; i < parts.Length; i++)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!element.TryGetProperty(parts[i], out var next))
            {
                // Try case-insensitive match
                var found = false;
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, parts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        element = prop.Value;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return null;
                }
            }
            else
            {
                element = next;
            }
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static object? ParseLiteral(string literal)
    {
        var trimmed = literal.Trim();

        // String literal (single or double quotes)
        if ((trimmed.StartsWith('\'') && trimmed.EndsWith('\'')) ||
            (trimmed.StartsWith('"') && trimmed.EndsWith('"')))
        {
            return trimmed[1..^1];
        }

        // Boolean
        if (bool.TryParse(trimmed, out var boolVal))
        {
            return boolVal;
        }

        // Number
        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal))
        {
            return numVal;
        }

        // Null
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Return as string
        return trimmed;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;

        if (value == null)
        {
            return false;
        }

        if (value is double d)
        {
            result = d;
            return true;
        }

        if (value is int i)
        {
            result = i;
            return true;
        }

        if (value is long l)
        {
            result = l;
            return true;
        }

        if (value is float f)
        {
            result = f;
            return true;
        }

        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return false;
    }
}
