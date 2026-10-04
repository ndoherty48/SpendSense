using SpendSense.Components.Layout;

namespace SpendSense.Tests.Unit;

/// <summary>Routing is the state: this table decides the active tab, the add button and the section switch.</summary>
public class NavDestinationsTests
{
    [Theory]
    [InlineData("", "home")]
    [InlineData("accounts", "home")]
    [InlineData("accounts/5", "home")]
    [InlineData("accounts/edit/5", "home")]
    [InlineData("transactions", "activity")]
    [InlineData("transactions/edit/3", "activity")]
    [InlineData("recurring-transactions/add", "activity")]
    [InlineData("monthly-budgets", "budgets")]
    [InlineData("goals/add", "budgets")]
    [InlineData("trends", "trends")]
    [InlineData("categories/edit/3", "more")]
    [InlineData("currencies", "more")]
    [InlineData("settings", "more")]
    public void Active_tab(string path, string tab)
    {
        Assert.Equal(tab, NavDestinations.Resolve(path).Tab?.Key);
    }

    [Theory]
    [InlineData("dev/gallery")]
    [InlineData("accountsx")]
    [InlineData("nothing-here")]
    public void Unknown_routes_have_no_tab(string path)
    {
        Assert.Null(NavDestinations.Resolve(path).Tab);
    }

    [Theory]
    [InlineData("", "/transactions/add")]
    [InlineData("transactions", "/transactions/add")]
    [InlineData("recurring-transactions", "/recurring-transactions/add")]
    [InlineData("monthly-budgets", "/monthly-budgets/add")]
    [InlineData("goals", "/goals/add")]
    [InlineData("accounts", "/accounts/add")]
    [InlineData("accounts/5", "/transactions/add?account=5")]
    public void Add_button_target(string path, string href)
    {
        Assert.Equal(href, NavDestinations.Resolve(path).AddHref);
    }

    [Theory]
    [InlineData("transactions/add")]
    [InlineData("accounts/edit/5")]
    [InlineData("trends")]
    [InlineData("more")]
    public void Forms_and_hubs_have_no_add_button(string path)
    {
        Assert.Null(NavDestinations.Resolve(path).AddHref);
    }

    [Theory]
    [InlineData("transactions", "Transaction view")]
    [InlineData("recurring-transactions", "Transaction view")]
    [InlineData("monthly-budgets", "Budget view")]
    [InlineData("goals", "Budget view")]
    public void Section_switch(string path, string label)
    {
        var state = NavDestinations.Resolve(path);

        Assert.Equal(label, state.Switch?.Label);
        Assert.Equal("/" + path, state.Switch?.Value);
    }

    [Theory]
    [InlineData("/Transactions?filter=x")]
    [InlineData("transactions/#top")]
    [InlineData("TRANSACTIONS")]
    public void Paths_are_normalised(string path)
    {
        Assert.Equal("activity", NavDestinations.Resolve(path).Tab?.Key);
    }
}
