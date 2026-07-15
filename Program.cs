using PrinterDaemon;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<PrinterSettings>(
    builder.Configuration.GetSection("Printer"));

// Ejecutar como Servicio de Windows
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PrinterDaemon";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();