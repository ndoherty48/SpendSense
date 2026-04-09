var builder = DistributedApplication.CreateBuilder(args);

var maui = builder.AddMauiProject("SpendSense", "../../App/SpendSense/SpendSense.csproj");

maui.AddiOSSimulator();
maui.AddAndroidDevice().WithArgs(ctx => ctx.Args.Remove("run"));
maui.AddAndroidEmulator().WithArgs(ctx => ctx.Args.Remove("run"));
maui.AddMacCatalystDevice();

builder.Build().Run();
