using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed record WeightDataImportResult(
    bool Success,
    int InsertedCount,
    int UpdatedCount,
    IReadOnlyList<string> Errors)
{
    public static WeightDataImportResult Failed(IReadOnlyList<string> errors) => new(false, 0, 0, errors);

    public static WeightDataImportResult Imported(int insertedCount, int updatedCount) =>
        new(true, insertedCount, updatedCount, []);
}

public sealed record WeightDataDeleteResult(
    bool Success,
    int DeletedCount,
    IReadOnlyList<string> Errors)
{
    public static WeightDataDeleteResult Failed(IReadOnlyList<string> errors) => new(false, 0, errors);

    public static WeightDataDeleteResult Deleted(int deletedCount) => new(true, deletedCount, []);
}

public sealed class WeightDataService(
    WeightTrackerDbContext db,
    ILocalDateProvider localDateProvider,
    IClock clock)
{
    private const decimal MinimumWeightKg = 0.1m;
    private const decimal MaximumWeightKg = 1000m;
    private const int MaximumNoteLength = 500;

    public async Task<string> ExportCsvAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.WeightEntries
            .AsNoTracking()
            .OrderBy(entry => entry.EntryDate)
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.Append("entry_date,weight_kg,note\n");

        foreach (var entry in entries)
        {
            builder.Append(entry.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(entry.WeightKg.ToString("0.000", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(EscapeCsv(entry.Note ?? string.Empty));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public async Task<WeightDataImportResult> ImportCsvAsync(
        string csv,
        CancellationToken cancellationToken = default)
    {
        var parseResult = ParseCsv(csv);
        if (parseResult.Errors.Count > 0)
        {
            return WeightDataImportResult.Failed(parseResult.Errors);
        }

        var records = parseResult.Records;
        if (records.Count == 0)
        {
            return WeightDataImportResult.Failed(["CSV file must include a header row."]);
        }

        if (!HasExpectedHeader(records[0].Fields))
        {
            return WeightDataImportResult.Failed(["CSV header must be exactly: entry_date,weight_kg,note."]);
        }

        var today = await localDateProvider.GetTodayAsync(cancellationToken);
        var errors = new List<string>();
        var importedRows = new List<ImportedWeightRow>();
        var seenDates = new HashSet<DateOnly>();

        foreach (var record in records.Skip(1))
        {
            ValidateImportRecord(record, today, seenDates, importedRows, errors);
        }

        if (errors.Count > 0)
        {
            return WeightDataImportResult.Failed(errors);
        }

        var dates = importedRows.Select(row => row.EntryDate).ToList();
        var existingEntries = await db.WeightEntries
            .Where(entry => dates.Contains(entry.EntryDate))
            .ToDictionaryAsync(entry => entry.EntryDate, cancellationToken);
        var now = clock.UtcNow;
        var inserted = 0;
        var updated = 0;

        foreach (var row in importedRows)
        {
            if (existingEntries.TryGetValue(row.EntryDate, out var existing))
            {
                existing.WeightKg = row.WeightKg;
                existing.Note = row.Note;
                existing.UpdatedAtUtc = now;
                updated++;
                continue;
            }

            db.WeightEntries.Add(new WeightEntry
            {
                EntryDate = row.EntryDate,
                WeightKg = row.WeightKg,
                Note = row.Note,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            inserted++;
        }

        await db.SaveChangesAsync(cancellationToken);

        return WeightDataImportResult.Imported(inserted, updated);
    }

    public async Task<WeightDataDeleteResult> DeleteAllWeightsAsync(
        string? confirmation,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirmation, "DELETE", StringComparison.Ordinal))
        {
            return WeightDataDeleteResult.Failed(["Type DELETE to confirm deleting all weight entries."]);
        }

        var entries = await db.WeightEntries.ToListAsync(cancellationToken);
        db.WeightEntries.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken);

        return WeightDataDeleteResult.Deleted(entries.Count);
    }

    private static void ValidateImportRecord(
        CsvRecord record,
        DateOnly today,
        ISet<DateOnly> seenDates,
        ICollection<ImportedWeightRow> importedRows,
        ICollection<string> errors)
    {
        if (record.Fields.Count != 3)
        {
            errors.Add($"Row {record.RowNumber}: expected 3 columns, found {record.Fields.Count}.");
            return;
        }

        if (!DateOnly.TryParseExact(
            record.Fields[0],
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var entryDate))
        {
            errors.Add($"Row {record.RowNumber}: entry_date must use yyyy-MM-dd.");
            return;
        }

        if (entryDate > today)
        {
            errors.Add($"Row {record.RowNumber}: entry_date cannot be in the future.");
        }

        if (!seenDates.Add(entryDate))
        {
            errors.Add($"Row {record.RowNumber}: duplicate entry_date {entryDate:yyyy-MM-dd}.");
        }

        var weightText = record.Fields[1];
        if (!decimal.TryParse(
            weightText,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var weightKg) ||
            weightKg < MinimumWeightKg ||
            weightKg > MaximumWeightKg ||
            GetDecimalScale(weightKg) > 3)
        {
            errors.Add(
                $"Row {record.RowNumber}: weight_kg must be from 0.1 through 1000 with no more than 3 decimal places.");
        }

        var note = string.IsNullOrEmpty(record.Fields[2]) ? null : record.Fields[2];
        if (note is { Length: > MaximumNoteLength })
        {
            errors.Add($"Row {record.RowNumber}: note must be 500 characters or fewer.");
        }

        if (errors.Count == 0 ||
            !errors.Any(error => error.StartsWith($"Row {record.RowNumber}:", StringComparison.Ordinal)))
        {
            importedRows.Add(new ImportedWeightRow(entryDate, weightKg, note));
        }
    }

    private static CsvParseResult ParseCsv(string csv)
    {
        var records = new List<CsvRecord>();
        var errors = new List<string>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var rowNumber = 1;

        for (var index = 0; index < csv.Length; index++)
        {
            var current = csv[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (current == '"' && field.Length == 0)
            {
                inQuotes = true;
                continue;
            }

            if (current == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (current == '\r' || current == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                AddRecord(records, fields, rowNumber);
                fields.Clear();

                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                rowNumber++;
                continue;
            }

            field.Append(current);
        }

        if (inQuotes)
        {
            errors.Add($"Row {rowNumber}: quoted field is not closed.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            AddRecord(records, fields, rowNumber);
        }

        return new CsvParseResult(records, errors);
    }

    private static void AddRecord(ICollection<CsvRecord> records, IReadOnlyList<string> fields, int rowNumber)
    {
        if (fields.Count == 1 && fields[0].Length == 0)
        {
            return;
        }

        records.Add(new CsvRecord(rowNumber, fields.ToList()));
    }

    private static bool HasExpectedHeader(IReadOnlyList<string> fields) =>
        fields.Count == 3 &&
        fields[0] == "entry_date" &&
        fields[1] == "weight_kg" &&
        fields[2] == "note";

    private static int GetDecimalScale(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed record CsvParseResult(IReadOnlyList<CsvRecord> Records, IReadOnlyList<string> Errors);

    private sealed record CsvRecord(int RowNumber, IReadOnlyList<string> Fields);

    private sealed record ImportedWeightRow(DateOnly EntryDate, decimal WeightKg, string? Note);
}