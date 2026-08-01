using PrinterDaemon;
using PrinterDaemon.Printing;
using System.Threading;

var mutex = new Mutex(
    initiallyOwned: true,
    name: @"Global\PrinterDaemon",
    createdNew: out bool createdNew);

if (!createdNew)
{
    return;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.Configure<PrinterSettings>(
    builder.Configuration.GetSection("Printer"));

builder.Services.AddSingleton<PrinterService>();

builder.Services.AddSingleton<PdfPrinter>();

builder.Services.AddSingleton<BmpPrinter>();

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PrinterDaemon";
});


var host = builder.Build();

host.Run();

mutex.ReleaseMutex();