namespace NBomberConsole.Tests;

public class ApplySubstitutionsTests
{
    [Fact]
    public void ApplySubstitutions_SinglePlaceholder_ReplacesValue()
    {
        var data = new Dictionary<string, string> { ["PostId"] = "42" };

        var result = Program.ApplySubstitutions("https://api.example.com/posts/{PostId}", data);

        Assert.Equal("https://api.example.com/posts/42", result);
    }

    [Fact]
    public void ApplySubstitutions_MultiplePlaceholders_ReplacesAll()
    {
        var data = new Dictionary<string, string>
        {
            ["UserId"] = "5",
            ["TenantId"] = "acme"
        };

        var result = Program.ApplySubstitutions(
            "https://api.example.com/{TenantId}/users/{UserId}", data);

        Assert.Equal("https://api.example.com/acme/users/5", result);
    }

    [Fact]
    public void ApplySubstitutions_CaseInsensitive_ReplacesRegardlessOfCase()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["postid"] = "99"
        };

        var result = Program.ApplySubstitutions("https://api.example.com/posts/{PostId}", data);

        Assert.Equal("https://api.example.com/posts/99", result);
    }

    [Fact]
    public void ApplySubstitutions_NoMatchingPlaceholder_LeavesTemplateUnchanged()
    {
        var data = new Dictionary<string, string> { ["Title"] = "Hello" };

        var result = Program.ApplySubstitutions("https://api.example.com/posts/{PostId}", data);

        Assert.Equal("https://api.example.com/posts/{PostId}", result);
    }

    [Fact]
    public void ApplySubstitutions_NoPlaceholders_ReturnsOriginal()
    {
        var data = new Dictionary<string, string> { ["PostId"] = "42" };

        var result = Program.ApplySubstitutions("https://api.example.com/posts/1", data);

        Assert.Equal("https://api.example.com/posts/1", result);
    }

    [Fact]
    public void ApplySubstitutions_EmptyData_ReturnsOriginal()
    {
        var data = new Dictionary<string, string>();

        var result = Program.ApplySubstitutions("https://api.example.com/posts/{PostId}", data);

        Assert.Equal("https://api.example.com/posts/{PostId}", result);
    }

    [Fact]
    public void ApplySubstitutions_JsonBody_ReplacesPlaceholders()
    {
        var data = new Dictionary<string, string>
        {
            ["UserId"] = "7",
            ["Title"] = "Test Post"
        };

        var result = Program.ApplySubstitutions(
            """{"userId": "{UserId}", "title": "{Title}"}""", data);

        Assert.Equal("""{"userId": "7", "title": "Test Post"}""", result);
    }
}
