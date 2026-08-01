using Microsoft.Extensions.Options;
using System.Drawing;
using System.Drawing.Printing;

namespace PrinterDaemon.Printing;

public class BmpPrinter
{
    private readonly PrinterSettings _config;

    public BmpPrinter(IOptions<PrinterSettings> options)
    {
        _config = options.Value;
    }

    public void Print(string archivo)
    {
        using Image imagen = Image.FromFile(archivo);

        using PrintDocument pd = new();

        pd.PrinterSettings.PrinterName = _config.PrinterName;

        if (!pd.PrinterSettings.IsValid)
            throw new Exception(
                $"No existe la impresora {_config.PrinterName}");

        pd.PrintController = new StandardPrintController();

        pd.PrintPage += (_, e) =>
        {
            Rectangle area = e.MarginBounds;

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