using Microsoft.Extensions.Options;
using PrinterDaemon.Printing.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;


namespace PrinterDaemon.Printing;

public class PrnPrinter
{
    private readonly PrinterSettings _config;
    public PrnPrinter(IOptions<PrinterSettings> options)
    {
        _config = options.Value;
    }

    public string Print(string archivo)
    {

        return GenerarPdf(archivo);
    }


    private string GenerarPdf(string archivoPrn)
    {
        var presupuesto = PrnParser.Parse(archivoPrn);

        string archivoPdf = Path.ChangeExtension(archivoPrn, ".pdf");

        QuestPDF.Settings.License =
            QuestPDF.Infrastructure.LicenseType.Community;

        Document.Create(document =>
        {
        document.Page(page =>
        {
        page.Size(PageSizes.A4);

        page.Margin(20);

        page.DefaultTextStyle(x => x
            .FontFamily(_config.FontName)
            .FontSize(_config.NormalFontSize));

        page.Content().Column(col =>
        {
            AgregarEncabezado(col, presupuesto);

            //-------------------------------------------------------
            // DETALLE
            //-------------------------------------------------------

            int linea = 0;

            foreach (var item in presupuesto.Items)
            {
                col.Item()
                    .Text(FormatearLinea(item))
                    .FontFamily(_config.FontName)
                    .FontSize(_config.NormalFontSize);

                linea++;

                if (linea >= _config.ItemsPerPage &&
                    item != presupuesto.Items.Last())
                {
                    linea = 0;

                    col.Item().PageBreak();

                    AgregarEncabezado(col, presupuesto);
                }
            }

            //-------------------------------------------------------
            // FIN DEL DETALLE
            //-------------------------------------------------------

            col.Item().PaddingTop(2);

            col.Item()
                .Text(new string('-', 70))
                .FontFamily(_config.FontName);

            //-------------------------------------------------------
            // OBSERVACIONES
            //-------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(presupuesto.Observaciones))
            {
                col.Item().PaddingTop(8);

                col.Item().Text(t =>
                {
                    t.Span("Observaciones: ")
                        .Bold();

                    t.Span(presupuesto.Observaciones);
                });
            }

            //-------------------------------------------------------
            // TOTAL
            //-------------------------------------------------------

            col.Item().PaddingTop(10);

            col.Item().AlignRight().Row(r =>
            {
                r.ConstantItem(220);

                r.ConstantItem(160)
                    .AlignRight()
                    .Text("Total........$")
                    .Bold();

                r.ConstantItem(120)
                    .AlignRight()
                    .Text(presupuesto.Total.ToString("N2"))
                    .Bold()
                    .FontSize(_config.TotalFontSize);
            });

        });
        });

        }).GeneratePdf(archivoPdf);

        return archivoPdf;
    }

    private string FormatearLinea(ItemData item)
    {
        string descripcion = item.Descripcion ?? "";

        if (descripcion.Length > 28)
            descripcion = descripcion[..28];

        return
            item.Cantidad.ToString("0.##").PadLeft(6) + " " +
            descripcion.PadRight(28) + " " +
            item.Codigo.PadLeft(5) + " " +
            item.Precio.ToString("0.00").PadLeft(11) + " " +
            (item.Descuento.ToString("0") + "%").PadLeft(4) + " " +
            item.Importe.ToString("0.00").PadLeft(12);
    }

    private void AgregarEncabezado(
    ColumnDescriptor col,
    Presupuesto presupuesto)
    {
        // Título
        col.Item()
            .AlignCenter()
            .Text(presupuesto.Tipo)
            .Bold()
            .FontSize(_config.TitleFontSize);

        // Línea de "=" igual al Clipper
        /*
        col.Item()
            .Text(new string('=', 79))
            .Bold()
            .FontFamily(_config.FontName);
        */

        // Fecha
        col.Item()
            .AlignRight()
            .Text($"FECHA: {presupuesto.Fecha:dd/MM/yyyy}");

        col.Item().Height(5);

        // Cliente y código
        col.Item()
            .Text(
                "Cliente: " +
                (presupuesto.Cliente.Nombre ?? "").PadRight(35) +
                " Codigo: " +
                (presupuesto.Cliente.Codigo ?? ""))
            .FontFamily(_config.FontName);

        // Domicilio
        col.Item()
            .Text(
                "Domicilio: " +
                (presupuesto.Cliente.Domicilio ?? ""))
            .FontFamily(_config.FontName);

        // Localidad
        col.Item()
            .Text(
                "Localidad: " +
                (presupuesto.Cliente.Localidad ?? ""))
            .FontFamily(_config.FontName);

        col.Item().Height(8);

        // Cabecera exactamente igual al Clipper
        col.Item()
            .Text(FormatearCabecera())
            .Bold()
            .FontFamily(_config.FontName)
            .FontSize(_config.NormalFontSize);

        // Línea de "-"
        col.Item()
            .Text(new string('-', 70))
            .FontFamily(_config.FontName);
    }

    private string FormatearCabecera()
    {
        return
            "Cant.".PadLeft(6) + " " +
            "Descripcion".PadRight(28) + " " +
            "Art.".PadLeft(5) + " " +
            "P.Unit.".PadLeft(11) + " " +
            "Dto.".PadLeft(4) + " " +
            "Importe".PadLeft(12);
    }
}