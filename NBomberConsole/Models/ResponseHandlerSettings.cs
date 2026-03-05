public class ResponseHandlerSettings
{
    /// <summary>
    /// The fully qualified type name to deserialize the response into.
    /// Example: "MyApp.Models.PaginatedResponse`1[[System.String]], MyApp"
    /// If null, response body is not deserialized.
    /// </summary>
    public string? ResponseTypeName { get; set; }

    /// <summary>
    /// JMES Path expression to extract data from deserialized response.
    /// Example: "Items[0].Id" or "HasNextPage"
    /// Used to conditionally chain requests or modify subsequent request parameters.
    /// </summary>
    public string? ExtractDataPath { get; set; }

    /// <summary>
    /// Name of the field in the data record (or context state) to store extracted value.
    /// This allows subsequent requests to reference the value via {FieldName} placeholder.
    /// </summary>
    public string? StoreAsField { get; set; }

    /// <summary>
    /// If true, continue to next request only if extracted value meets this condition.
    /// Example: "HasNextPage == true" or "Offset < TotalItems"
    /// </summary>
    public string? ContinueCondition { get; set; }
}