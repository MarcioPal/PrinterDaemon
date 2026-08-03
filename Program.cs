using PrinterDaemon;
using PrinterDaemon.Printing;
using System.Threading;
using Serilog;

var mutex = new Mutex(
    initiallyOwned: true,
    name: @"Global\PrinterDaemon",
    createdNew: out bool createdNew);

if (!createdNew)
{
    return;
}

var carpetaLogs = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(carpetaLogs);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(carpetaLogs, "PrinterDaemon-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Use Serilog as the logging provider so all Microsoft ILogger logs are
// forwarded to the file sink configured above.
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.Configure<PrinterSettings>(
    builder.Configuration.GetSection("Printer"));

builder.Services.AddSingleton<PrinterService>();

builder.Services.AddSingleton<PdfPrinter>();

builder.Services.AddSingleton<BmpPrinter>();
builder.Services.AddSingleton<PrnPrinter>();

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PrinterDaemon";
});


var host = builder.Build();

host.Run();

mutex.ReleaseMutex();