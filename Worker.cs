using Microsoft.Extensions.Options;
using PdfiumViewer;
using System.Drawing;
using System.Drawing.Printing;
namespace PrinterDaemon
{
    public class Worker(
        ILogger<Worker> logger,
        IOptions<PrinterSettings> options)
        : BackgroundService
    {
        private readonly PrinterSettings _config = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Servicio de impresión iniciado.");

            if (!Directory.Exists(_config.WatchFolder))
                Directory.CreateDirectory(_config.WatchFolder);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    string[] archivos = Directory.GetFiles(
                                        _config.WatchFolder,
                                        _config.FileFilter);

                    foreach (string archivo in archivos)
                    {
                        try
                        {
                            string extension = Path.GetExtension(archivo).ToLowerInvariant();

                            switch (extension)
                            {
                                case ".pdf":
                                    ImprimirPdf(archivo);
                                    break;

                                case ".bmp":
                                    ImprimirBmp(archivo);
                                    break;

                                default:
                                    logger.LogWarning("Tipo de archivo no soportado: {archivo}", archivo);
                                    continue;
                            }

                            Thread.Sleep(2000);
                            if (_config.DeleteAfterPrint)
                                File.Delete(archivo);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Error imprimiendo {archivo}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error leyendo carpeta.");
                }

                await Task.Delay(_config.Interval, stoppingToken);
            }
        }


    private void ImprimirPdf(string archivo)
    {
        using var document = PdfDocument.Load(archivo);

        using var printDocument = document.CreatePrintDocument();

        printDocument.PrinterSettings.PrinterName = _config.PrinterName;

        if (!printDocument.PrinterSettings.IsValid)
            throw new Exception($"No se encontró la impresora '{_config.PrinterName}'.");

        printDocument.PrintController = new StandardPrintController();

        printDocument.Print();
    }

    private void ImprimirBmp(string archivo)
    {
        using Image imagen = Image.FromFile(archivo);

        using PrintDocument pd = new PrintDocument();

        pd.PrinterSettings.PrinterName = _config.PrinterName;

        if (!pd.PrinterSettings.IsValid)
            throw new Exception($"No se encontró la impresora '{_config.PrinterName}'.");

        pd.PrintController = new StandardPrintController();

        pd.PrintPage += (sender, e) =>
        {
            Rectangle area = e.MarginBounds;

            // Escala la imagen manteniendo la proporción
            double escala = Math.Min(
                (double)area.Width / imagen.Width,
                (double)area.Height / imagen.Height);

            int ancho = (int)(imagen.Width * escala);
            int alto = (int)(imagen.Height * escala);

            int x = area.X + (area.Width - ancho) / 2;
            int y = area.Y + (area.Height - alto) / 2;

            e.Graphics.DrawImage(imagen, x, y, ancho, alto);
        };

        pd.Print();
    }


    }
}
