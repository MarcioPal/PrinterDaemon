using Microsoft.Extensions.Options;
using PrinterDaemon.Printing.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrinterDaemon.Printing;

public sealed class LvPrinter
{
    private const float EspacioReservadoParaResumen = 170;
    private readonly PrinterSettings _config;

    public LvPrinter(IOptions<PrinterSettings> options)
    {
        _config = options.Value;
    }

    public string Print(string archivo)
    {
        var reporte = LvParser.Parse(archivo);
        var archivoPdf = Path.ChangeExtension(archivo, ".pdf");
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(style => style
                    .FontFamily(_config.FontName)
                    .FontSize(Math.Min(_config.NormalFontSize, 10)));

                page.Header().Column(header =>
                {
                    AgregarEncabezado(header, reporte);
                });

                page.Content().Column(content =>
                {
                    content.Item().PaddingTop(4)
                        .Text("Ventas al contado")
                        .Bold();

                    foreach (var movimiento in reporte.Movimientos)
                        content.Item().Text(FormatearMovimiento(movimiento));

                    // El resumen debe conservarse completo y con espacio libre
                    // al final de la hoja. Si no entra, QuestPDF lo desplaza
                    // íntegramente a la página siguiente.
                    content.Item()
                        .EnsureSpace(EspacioReservadoParaResumen)
                        .ShowEntire()
                        .Column(resumen => AgregarResumen(resumen, reporte));
                });
            });
        }).GeneratePdf(archivoPdf);

        return archivoPdf;
    }

    private void AgregarEncabezado(
        ColumnDescriptor content,
        VentasReporte reporte)
    {
        content.Item().Row(row =>
        {
            row.RelativeItem().Text(reporte.Empresa).Bold();
            row.ConstantItem(125).AlignRight().Text(text =>
            {
                text.Span("Hoja N° : ");
                text.CurrentPageNumber();
            });
        });

        content.Item().AlignRight().Text(FormatoFecha(reporte.FechaEmision));

        content.Item().PaddingTop(8).AlignCenter()
            .Text($"Resumen de ventas de: {reporte.Cliente}")
            .Bold();

        content.Item().AlignCenter()
            .Text($"( {reporte.CodigoCliente} )")
            .Bold();
    }

    private void AgregarResumen(ColumnDescriptor content, VentasReporte reporte)
    {
        content.Item().PaddingTop(8)
            .Text(Linea("Total de ventas al contado: ", FormatoImporte(reporte.TotalContado)))
            .Bold();

        content.Item().PaddingTop(12)
            .Text("Ventas en cuenta corriente")
            .Bold();

        content.Item().PaddingTop(8)
            .Text(Linea("Total de ventas en cta cte: ", FormatoImporte(reporte.TotalCuentaCorriente)))
            .Bold();

        content.Item()
            .Text(Linea("Saldo en cta cte a fecha : ", FormatoImporte(reporte.SaldoCuentaCorrienteFecha)))
            .Bold();

        content.Item().PaddingTop(12)
            .Text(Linea("Total de ventas general  : ", FormatoImporte(reporte.TotalGeneral)))
            .Bold();

        content.Item().PaddingTop(12)
            .Text(Linea("Saldo actual en cta. cte.: ", FormatoImporte(reporte.SaldoActualCuentaCorriente)))
            .Bold();
    }

    private static string FormatearMovimiento(VentaMovimiento movimiento)
    {
        // Todas las columnas usan anchos fijos para que el inicio de cada
        // campo sea idéntico aunque falte la factura o cambie su longitud.
        string fecha = FormatoFecha(movimiento.Fecha);
        string factura = FormatearFactura(movimiento.Factura);
        string importe = FormatoImporte(movimiento.Importe);

        return string.Format(
            "Fecha : {0,-10} N° F.: {1,-14} Importe: $ {2,12}  {3}",
            fecha,
            factura,
            importe,
            movimiento.Observacion);
    }

    private static string FormatearFactura(string factura)
    {
        string valor = new string(factura.Where(char.IsLetterOrDigit).ToArray());

        // El formato del archivo es un número de 13 dígitos: punto de venta
        // de 5 dígitos y número de comprobante de 8 dígitos.
        if (valor.Length == 13 && valor.All(char.IsDigit))
            return $"{valor[..5]}-{valor[5..]}";

        return valor;
    }

    private static string Linea(string etiqueta, string importe)
    {
        return etiqueta.PadRight(38) + importe.PadLeft(12);
    }

    private static string FormatoFecha(DateTime? fecha) =>
        fecha?.ToString("dd/MM/yyyy") ?? "";

    private static string FormatoImporte(decimal importe) =>
        importe.ToString("N2");
}
