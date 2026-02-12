using System.Text.Json;
using NBomberConsole.Models;

namespace NBomberConsole.Tests;

public class EndpointSettingsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var settings = new EndpointSettings();

        Assert.Equal("GET", settings.HttpMethod);
        Assert.Equal(string.Empty, settings.Url);
        Assert.Null(settings.Headers);
        Assert.Null(settings.JsonBody);
        Assert.Null(settings.StepName);
        Assert.Null(settings.ExpectedStatusCode);
        Assert.Equal(30000, settings.TimeoutMs);
        Assert.Null(settings.DataSource);
    }

    [Fact]
    public void Deserialize_FullConfig_PopulatesAllFields()
    {
        var json = """
        {
            "HttpMethod": "POST",
            "Url": "https://api.example.com/posts",
            "StepName": "create_post",
            "ExpectedStatusCode": 201,
            "TimeoutMs": 5000,
            "Headers": {
                "Authorization": "Bearer token123",
                "Accept": "application/json"
            },
            "JsonBody": { "title": "Test", "userId": 1 },
            "DataSource": {
                "Type": "CSV",
                "FilePath": "test.csv",
                "FeedStrategy": "Circular"
            }
        }
        """;

        var settings = JsonSerializer.Deserialize<EndpointSettings>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("POST", settings.HttpMethod);
        Assert.Equal("https://api.example.com/posts", settings.Url);
        Assert.Equal("create_post", settings.StepName);
        Assert.Equal(201, settings.ExpectedStatusCode);
        Assert.Equal(5000, settings.TimeoutMs);
        Assert.NotNull(settings.Headers);
        Assert.Equal("Bearer token123", settings.Headers!["Authorization"]);
        Assert.NotNull(settings.JsonBody);
        Assert.NotNull(settings.DataSource);
        Assert.Equal("CSV", settings.DataSource!.Type);
        Assert.Equal("test.csv", settings.DataSource.FilePath);
        Assert.Equal("Circular", settings.DataSource.FeedStrategy);
    }
}

public class DataSourceSettingsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var settings = new DataSourceSettings();

        Assert.Equal(string.Empty, settings.Type);
        Assert.Null(settings.FilePath);
        Assert.Null(settings.ConnectionString);
        Assert.Null(settings.Query);
        Assert.Equal("SqlServer", settings.ProviderName);
        Assert.Equal("Random", settings.FeedStrategy);
    }

    [Fact]
    public void Deserialize_DatabaseConfig_PopulatesAllFields()
    {
        var json = """
        {
            "Type": "Database",
            "ProviderName": "SqlServer",
            "ConnectionString": "Server=localhost;Database=TestDb;",
            "Query": "SELECT TOP 100 UserId FROM Users",
            "FeedStrategy": "Constant"
        }
        """;

        var settings = JsonSerializer.Deserialize<DataSourceSettings>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal("Database", settings.Type);
        Assert.Equal("SqlServer", settings.ProviderName);
        Assert.Equal("Server=localhost;Database=TestDb;", settings.ConnectionString);
        Assert.Equal("SELECT TOP 100 UserId FROM Users", settings.Query);
        Assert.Equal("Constant", settings.FeedStrategy);
    }
}
