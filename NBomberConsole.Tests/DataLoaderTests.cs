namespace NBomberConsole.Tests;

public class DataLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public DataLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nbomber_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateTempCsv(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void LoadFromCsv_ValidFile_ReturnsRecords()
    {
        var path = CreateTempCsv("valid.csv", "PostId,Title\n1,First\n2,Second\n3,Third\n");

        var records = DataLoader.LoadFromCsv(path);

        Assert.Equal(3, records.Count);
        Assert.Equal("1", records[0]["PostId"]);
        Assert.Equal("First", records[0]["Title"]);
        Assert.Equal("3", records[2]["PostId"]);
        Assert.Equal("Third", records[2]["Title"]);
    }

    [Fact]
    public void LoadFromCsv_SingleColumn_ReturnsRecords()
    {
        var path = CreateTempCsv("single.csv", "PostId\n1\n2\n3\n");

        var records = DataLoader.LoadFromCsv(path);

        Assert.Equal(3, records.Count);
        Assert.Equal("2", records[1]["PostId"]);
    }

    [Fact]
    public void LoadFromCsv_CaseInsensitiveKeys()
    {
        var path = CreateTempCsv("case.csv", "PostId,Title\n1,Test\n");

        var records = DataLoader.LoadFromCsv(path);

        Assert.Equal("1", records[0]["postid"]);
        Assert.Equal("1", records[0]["POSTID"]);
        Assert.Equal("Test", records[0]["title"]);
    }

    [Fact]
    public void LoadFromCsv_FileNotFound_ThrowsFileNotFoundException()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => DataLoader.LoadFromCsv("nonexistent.csv"));

        Assert.Contains("nonexistent.csv", ex.Message);
    }

    [Fact]
    public void LoadFromCsv_HeadersOnly_ThrowsInvalidOperationException()
    {
        var path = CreateTempCsv("empty.csv", "PostId,Title\n");

        var ex = Assert.Throws<InvalidOperationException>(
            () => DataLoader.LoadFromCsv(path));

        Assert.Contains("no data rows", ex.Message);
    }

    [Fact]
    public void LoadFromDatabase_UnsupportedProvider_ThrowsNotSupportedException()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => DataLoader.LoadFromDatabase("postgres", "Host=localhost", "SELECT 1"));

        Assert.Contains("postgres", ex.Message);
        Assert.Contains("not supported", ex.Message);
    }
}
