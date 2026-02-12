using System.Data.Common;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;

namespace NBomberConsole;

/// <summary>
/// Loads test data from CSV files or databases into a list of dictionaries.
/// Each dictionary represents one data record where keys are column names
/// and values are string representations used for placeholder substitution.
/// </summary>
public static class DataLoader
{
    /// <summary>
    /// Reads all records from a CSV file. The first row must be a header row
    /// defining column names that become placeholder tokens (e.g., {PostId}).
    /// </summary>
    public static List<Dictionary<string, string>> LoadFromCsv(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"CSV data file not found: '{filePath}'. " +
                "Ensure the file path in DataSource.FilePath is correct and the file exists.",
                filePath);
        }

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

        var records = new List<Dictionary<string, string>>();
        csv.Read();
        csv.ReadHeader();

        var headers = csv.HeaderRecord
            ?? throw new InvalidOperationException(
                $"CSV file '{filePath}' has no header row. " +
                "The first row must contain column names.");

        while (csv.Read())
        {
            var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                record[header] = csv.GetField(header) ?? string.Empty;
            }
            records.Add(record);
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException(
                $"CSV file '{filePath}' contains headers but no data rows.");
        }

        return records;
    }

    /// <summary>
    /// Executes a SQL query and returns all rows as dictionaries.
    /// Column names from the query result become placeholder tokens.
    /// </summary>
    public static List<Dictionary<string, string>> LoadFromDatabase(
        string providerName, string connectionString, string query)
    {
        using var connection = CreateConnection(providerName, connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        using var reader = command.ExecuteReader();
        var records = new List<Dictionary<string, string>>();

        while (reader.Read())
        {
            var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                record[reader.GetName(i)] = reader.IsDBNull(i)
                    ? string.Empty
                    : reader[i].ToString()!;
            }
            records.Add(record);
        }

        if (records.Count == 0)
        {
            throw new InvalidOperationException(
                $"Database query returned no rows. Query: {query}");
        }

        return records;
    }

    private static DbConnection CreateConnection(string providerName, string connectionString)
    {
        return providerName.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlConnection(connectionString),
            _ => throw new NotSupportedException(
                $"Database provider '{providerName}' is not supported. " +
                "Supported providers: SqlServer. " +
                "To add support for other databases, install the appropriate NuGet package " +
                "and add a case to DataLoader.CreateConnection().")
        };
    }
}
