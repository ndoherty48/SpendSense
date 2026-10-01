using MudBlazor;

using SpendSense.Components.Shared;

namespace SpendSense.Components.Layout;

/// <summary>A top-level destination shown in the bottom bar and the side rail.</summary>
public sealed record NavTab(string Key, string Label, string Href, string Icon, string[] Prefixes, bool ExactOnly = false)
{
    /// <summary>True when <paramref name="path"/> (leading slash, lowercase, no query) belongs to this tab.</summary>
    public bool Matches(string path) =>
        ExactOnly
            ? path == Href
            : Prefixes.Any(prefix => path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal));
}

/// <summary>A route-bound switch shown above a list page (e.g. All | Recurring).</summary>
public sealed record NavSwitch(string Label, IReadOnlyList<Segment<string>> Segments, string Value);

/// <summary>What the shell should show for the current route.</summary>
public sealed record NavState(NavTab? Tab, string? AddHref, string? AddLabel, NavSwitch? Switch)
{
    public static NavState None { get; } = new(null, null, null, null);
}

/// <summary>
/// The app's navigation model. Routing is the state: every existing page URL is kept, and this table
/// decides which tab is active, which list pages get the add button, and which get a section switch.
/// </summary>
public static class NavDestinations
{
    public static IReadOnlyList<NavTab> Tabs { get; } =
    [
        new("home", "Home", "/", Icons.Material.Filled.Home, [], ExactOnly: true),
        new("activity", "Activity", "/transactions", Icons.Material.Filled.ReceiptLong, ["/transactions", "/recurring-transactions"]),
        new("budgets", "Budgets", "/monthly-budgets", Icons.Material.Filled.AccountBalanceWallet, ["/monthly-budgets", "/goals"]),
        new("trends", "Trends", "/trends", Icons.Material.Filled.TrendingUp, ["/trends"]),
        new("more", "More", "/more", Icons.Material.Filled.MoreHoriz, ["/more", "/categories", "/currencies", "/settings"]),
    ];

    // List pages that get the add button, keyed by exact path.
    static readonly Dictionary<string, (string Href, string Label)> AddTargets = new()
    {
        ["/"] = ("/transactions/add", "Add transaction"),
        ["/transactions"] = ("/transactions/add", "Add transaction"),
        ["/recurring-transactions"] = ("/recurring-transactions/add", "Add recurring transaction"),
        ["/monthly-budgets"] = ("/monthly-budgets/add", "Add budget"),
        ["/goals"] = ("/goals/add", "Add goal"),
    };

    static readonly IReadOnlyList<Segment<string>> ActivitySegments =
    [
        new("/transactions", "All", "/transactions"),
        new("/recurring-transactions", "Recurring", "/recurring-transactions"),
    ];

    static readonly IReadOnlyList<Segment<string>> BudgetSegments =
    [
        new("/monthly-budgets", "Monthly", "/monthly-budgets"),
        new("/goals", "Goals", "/goals"),
    ];

    /// <param name="relativePath">The URL relative to the app base, e.g. <c>transactions/edit/3?x=1</c>.</param>
    public static NavState Resolve(string? relativePath)
    {
        var path = Normalize(relativePath);
        var tab = Tabs.FirstOrDefault(t => t.Matches(path));

        string? addHref = null, addLabel = null;
        if (AddTargets.TryGetValue(path, out var add))
            (addHref, addLabel) = add;

        NavSwitch? @switch = path switch
        {
            "/transactions" or "/recurring-transactions" => new NavSwitch("Transaction view", ActivitySegments, path),
            "/monthly-budgets" or "/goals" => new NavSwitch("Budget view", BudgetSegments, path),
            _ => null,
        };

        return new NavState(tab, addHref, addLabel, @switch);
    }

    static string Normalize(string? relativePath)
    {
        var path = (relativePath ?? "").Split('?', '#')[0].Trim('/').ToLowerInvariant();
        return "/" + path;
    }
}
