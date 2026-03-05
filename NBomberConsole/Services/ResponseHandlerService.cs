using System.Reflection;
using System.Text.Json;

namespace NBomberConsole.Services;

public class ResponseHandlerService
{
    /// <summary>
    /// Deserializes the response body to the configured type and extracts
    /// fields based on the handler settings.
    /// </summary>
    public static async Task<Dictionary<string, string>> ExtractResponseData(
        string responseBody,
        ResponseHandlerSettings handler)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(handler.ResponseTypeName))
            return result;

        try
        {
            // Deserialize response to the configured type
            var responseType = Type.GetType(handler.ResponseTypeName);
            if (responseType == null)
            {
                throw new InvalidOperationException(
                    $"Response type '{handler.ResponseTypeName}' could not be resolved. " +
                    "Ensure the assembly is loaded and the fully qualified name is correct.");
            }

            var deserializedResponse = JsonSerializer.Deserialize(
                responseBody, 
                responseType,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (deserializedResponse == null)
                return result;

            // Extract data using the path expression (simple property access)
            if (!string.IsNullOrWhiteSpace(handler.ExtractDataPath))
            {
                var extractedValue = ExtractValueByPath(deserializedResponse, handler.ExtractDataPath);
                
                if (extractedValue != null && !string.IsNullOrWhiteSpace(handler.StoreAsField))
                {
                    result[handler.StoreAsField] = extractedValue.ToString()!;
                }
            }

            // For pagination scenarios, expose common pagination properties automatically
            ExposePaginationProperties(deserializedResponse, result);
        }
        catch (Exception ex)
        {
            // Log deserialization errors but don't fail the request
            // The extracted data will simply be empty
            System.Diagnostics.Debug.WriteLine(
                $"Failed to extract response data: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Evaluates a simple condition against extracted response data.
    /// Supports basic comparisons like "HasNextPage == true" or "Offset < TotalItems".
    /// </summary>
    public static bool EvaluateCondition(
        string? condition, 
        Dictionary<string, string> responseData)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        try
        {
            // Simple condition evaluation: "FieldName == Value" or "FieldName > Value"
            // For production, consider using NLua or DynamicExpresso for safer evaluation
            var parts = condition.Split(new[] { "==", "!=", "<", ">", "<=", ">=" }, 
                StringSplitOptions.None);

            if (parts.Length < 2)
                return true;

            var fieldName = parts[0].Trim();
            var expectedValue = parts[1].Trim();

            if (!responseData.TryGetValue(fieldName, out var actualValue))
                return true; // Field not found, assume true to continue

            // Simple boolean check: "HasNextPage == true"
            if (expectedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                return actualValue.Equals("True", StringComparison.OrdinalIgnoreCase);
            if (expectedValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                return actualValue.Equals("False", StringComparison.OrdinalIgnoreCase);

            // Simple numeric comparison
            if (int.TryParse(actualValue, out var actualNum) && 
                int.TryParse(expectedValue, out var expectedNum))
            {
                if (condition.Contains(">="))
                    return actualNum >= expectedNum;
                if (condition.Contains("<="))
                    return actualNum <= expectedNum;
                if (condition.Contains(">"))
                    return actualNum > expectedNum;
                if (condition.Contains("<"))
                    return actualNum < expectedNum;
                if (condition.Contains("=="))
                    return actualNum == expectedNum;
                if (condition.Contains("!="))
                    return actualNum != expectedNum;
            }

            return true;
        }
        catch
        {
            return true; // On error, assume condition is met
        }
    }

    private static object? ExtractValueByPath(object obj, string path)
    {
        var parts = path.Split('.');
        var current = obj;

        foreach (var part in parts)
        {
            if (current == null)
                return null;

            var property = current.GetType().GetProperty(
                part, 
                BindingFlags.IgnoreCase | BindingFlags.Public);

            if (property == null)
                return null;

            current = property.GetValue(current);
        }

        return current;
    }

    private static void ExposePaginationProperties(
        object responseObj, 
        Dictionary<string, string> result)
    {
        // Automatically extract common pagination fields
        var paginationFields = new[] 
        { 
            "HasNextPage", "HasPreviousPage", "TotalPages", "CurrentPage", 
            "Offset", "Limit", "TotalItems", "ItemsSubSetCount"
        };

        var objType = responseObj.GetType();

        foreach (var fieldName in paginationFields)
        {
            var property = objType.GetProperty(
                fieldName, 
                BindingFlags.IgnoreCase | BindingFlags.Public);

            if (property != null)
            {
                var value = property.GetValue(responseObj);
                if (value != null)
                {
                    result[fieldName] = value.ToString()!;
                }
            }
        }
    }
}