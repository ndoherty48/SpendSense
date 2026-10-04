using System.Globalization;

using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Services;

/// <summary>Writes transactions as CSV for spreadsheets (RFC 4180 quoting; see ADR 0003 for the columns).</summary>
public static class TransactionCsv
{
    public const string Header = "Date,Description,Type,Category,Account,To account,Amount,Currency,Notes";

    public static void Write(IEnumerable<Transaction> transactions, TextWriter writer)
    {
        writer.Write(Header);
        writer.Write("\r\n");
        foreach (var t in transactions)
        {
            writer.Write(string.Join(',',
                t.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Text(t.Description),
                t.TransactionType.ToString(),
                Text(t.Category?.Name),
                Text(t.Account?.Name),
                Text(t.ToAccount?.Name),
                SignedAmount(t).ToString("0.00", CultureInfo.InvariantCulture),
                Text(t.Currency?.Code),
                Text(t.Notes)));
            writer.Write("\r\n");
        }
    }

    /// <summary>From the source account's point of view: income in, everything else out.</summary>
    public static double SignedAmount(Transaction t) =>
        t.TransactionType == TransactionTypeEnum.Income ? t.Amount : -t.Amount;

    /// <summary>A text cell: neutralised against formula injection, then quoted if needed.</summary>
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // A cell starting with these runs as a formula in Excel/Sheets; a leading ' shows it as text.
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 || value != value.Trim()
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
