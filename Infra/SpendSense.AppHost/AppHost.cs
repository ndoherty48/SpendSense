using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var maui = builder.AddMauiProject("SpendSense", "../../App/SpendSense/SpendSense.csproj");

maui.AddiOSSimulator();
maui.AddAndroidDevice("SpendSense-Android-Device", deviceId: builder.Configuration.GetValue<string>("AndroidDeviceId"))
    .WithArgs(ctx => ctx.Args.Remove("run"));
    //.WithOtlpDevTunnel();
maui.AddAndroidEmulator()
    .WithArgs(ctx => ctx.Args.Remove("run"));
    //.WithOtlpDevTunnel();
maui.AddMacCatalystDevice();

builder.Build().Run();
