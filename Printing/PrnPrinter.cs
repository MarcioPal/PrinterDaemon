using Microsoft.Extensions.Options;
using PrinterDaemon.Printing.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;

        int itemsPerPage = Math.Max(1, _config.ItemsPerPage);

        int totalPaginas =
            (int)Math.Ceiling(
                (double)presupuesto.Items.Count /
                itemsPerPage);

        decimal grandTotal = presupuesto.Items.Sum(i => i.Importe);

        try
        {
            Document.Create(document =>
            {
            int indice = 0;
            int pagina = 1; // physical page counter
            int paginaLogica = 1; // logical page (1..totalPaginas) shown in headers

            decimal acumulado = 0;

            while (indice < presupuesto.Items.Count)
            {
                // calcular los items que se imprimirán en esta página (lista ya creada)
                var pageItems = presupuesto.Items.Skip(indice).Take(itemsPerPage).ToList();
                var subtotalPagina = pageItems.Sum(i => i.Importe);

                // estimaciones de espacio
                var margin = 15f;
                var sepFontSize = Math.Max(6f, _config.NormalFontSize - 6);
                var sepHeight = Math.Max(8f, sepFontSize * 1.2f + 2f);
                var halfHeight = (float)((PageSizes.A4.Height - 2 * margin - sepHeight) / 2.0);
                var lineHeight = _config.NormalFontSize * 1.2f;

                // estimar líneas del encabezado y pie (conservador)
                var headerLines = 9;
                var footerBaseLines = 2; // linea de separador + total

                int obsLines = 0;
                if (!string.IsNullOrWhiteSpace(presupuesto.Observaciones))
                {
                    // use a conservative chars-per-line estimate so observations don't get split
                    obsLines = (int)Math.Ceiling((double)presupuesto.Observaciones.Length / 50.0);
                }

                var neededHeight = (headerLines + pageItems.Count + footerBaseLines + obsLines) * lineHeight;
                bool needsFullPage = neededHeight > halfHeight;

                var transporteAcumulado = acumulado + subtotalPagina;

                if (needsFullPage)
                {
                    // Render original on full page
                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin((int)margin);
                        page.DefaultTextStyle(x => x.FontFamily(_config.FontName).FontSize(_config.NormalFontSize));
                        page.Content().Column(col =>
                        {
                            RenderMediaHojaItems(
                                col,
                                presupuesto,
                                pageItems,
                                ref acumulado,
                                "ORIGINAL",
                                paginaLogica,
                                totalPaginas,
                                transporteAcumulado,
                                grandTotal,
                                incrementarAcumulado: true,
                                esUltimaPagina: (indice + pageItems.Count) >= presupuesto.Items.Count);
                        });
                    });

                    pagina++;

                    // Render duplicate on next page
                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin((int)margin);
                        page.DefaultTextStyle(x => x.FontFamily(_config.FontName).FontSize(_config.NormalFontSize));
                        page.Content().Column(col =>
                        {
                            RenderMediaHojaItems(
                                col,
                                presupuesto,
                                pageItems,
                                ref acumulado,
                                "DUPLICADO",
                                paginaLogica,
                                totalPaginas,
                                transporteAcumulado,
                                grandTotal,
                                incrementarAcumulado: false,
                                esUltimaPagina: (indice + pageItems.Count) >= presupuesto.Items.Count);
                        });
                    });

                    // advance index and page
                    indice += pageItems.Count;
                    // logical page consumed
                    paginaLogica++;
                    pagina++;
                }
                else
                {
                    // fits in half page: render both halves on single physical page
                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin((int)margin);
                        page.DefaultTextStyle(x => x.FontFamily(_config.FontName).FontSize(_config.NormalFontSize));
                        page.Content().Column(col =>
                        {
                            // ORIGINAL top half
                            col.Item().Height(halfHeight).Column(topCol =>
                            {
                                RenderMediaHojaItems(
                                    topCol,
                                    presupuesto,
                                    pageItems,
                                    ref acumulado,
                                    "ORIGINAL",
                                    paginaLogica,
                                    totalPaginas,
                                    transporteAcumulado,
                                    grandTotal,
                                    incrementarAcumulado: true,
                                    esUltimaPagina: (indice + pageItems.Count) >= presupuesto.Items.Count);
                            });

                            // advance index now that original drawn
                            indice += pageItems.Count;

                            // separator
                            col.Item().Height(sepHeight).AlignCenter().Text(new string('-', 80)).FontFamily(_config.FontName).FontSize(sepFontSize);

                            // DUPLICATE bottom half
                            col.Item().Height(halfHeight).Column(botCol =>
                            {
                                RenderMediaHojaItems(
                                    botCol,
                                    presupuesto,
                                    pageItems,
                                    ref acumulado,
                                    "DUPLICADO",
                                    paginaLogica,
                                    totalPaginas,
                                    transporteAcumulado,
                                    grandTotal,
                                    incrementarAcumulado: false,
                                    esUltimaPagina: (indice >= presupuesto.Items.Count));
                            });
                        });
                    });
                    // logical page consumed
                    paginaLogica++;
                    pagina++;
                }
            }

            }).GeneratePdf(archivoPdf);
        }
        catch (QuestPDF.Drawing.Exceptions.DocumentLayoutException dex)
        {
            try
            {
                var carpetaLogs = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(carpetaLogs);
                var debugFile = Path.Combine(carpetaLogs, $"PrnPrinter-LayoutDebug-{DateTime.Now:yyyyMMddHHmmss}.txt");
                File.WriteAllText(debugFile, dex.ToString());
                Serilog.Log.Error(dex, "Document layout error generating PDF. Debug info written to {file}", debugFile);
            }
            catch (Exception) { }

            throw;
        }

        return archivoPdf;
    }

    private int RenderMediaHoja(
        ColumnDescriptor col,
        Presupuesto presupuesto,
        int indice,
        ref decimal acumulado,
        string copia,
        int pagina,
        int totalPaginas,
        bool incrementarAcumulado = true,
        bool renderFooter = true,
        decimal? transporteOverride = null,
        decimal? grandTotal = null,
        bool forceUltima = false)
    {
        //---------------------------------------
        // Encabezado
        //---------------------------------------

        AgregarEncabezado(
                        col,
                        presupuesto,
                        copia,
                        pagina,
                        totalPaginas);



        //---------------------------------------
        // Detalle
        //---------------------------------------

        // Decide cuántos items vamos a imprimir en esta media hoja
        int itemsDisponibles = Math.Max(0, presupuesto.Items.Count - indice);
        int itemsAImprimir = Math.Min(_config.ItemsPerPage, itemsDisponibles);

        // Renderizar exactamente itemsAImprimir líneas
        decimal subtotalLocal = 0m;
        for (int i = 0; i < itemsAImprimir; i++)
        {
            var item = presupuesto.Items[indice + i];

            col.Item()
                .Text(FormatearLinea(item))
                .FontFamily(_config.FontName)
                .FontSize(_config.NormalFontSize);

            subtotalLocal += item.Importe;

            if (incrementarAcumulado)
            {
                acumulado += item.Importe;
            }
        }

        indice += itemsAImprimir;



        //---------------------------------------
        // Pie (mostramos observaciones y total/transporte en la misma línea)
        //---------------------------------------

        bool ultimaPagina =
            indice >= presupuesto.Items.Count || forceUltima;

        var observaciones = string.IsNullOrWhiteSpace(presupuesto.Observaciones)
            ? null
            : presupuesto.Observaciones.Trim();

        if (renderFooter)
        {
            // Use override if provided, otherwise show subtotal of the items rendered in this media hoja
            var transporteAMostrar = transporteOverride ?? subtotalLocal;
            var totalAMostrar = grandTotal ?? presupuesto.Total;

            // Log antes de agregar pie
            try
            {
                Serilog.Log.Information("PRN: RenderMediaHoja copia={copia} pagina={pagina} transporteAMostrar={t} subtotalLocal={sl} acumulado={a} totalAMostrar={tot} ultima={u} renderFooter={rf} transporteOverride={ov}",
                    copia, pagina, transporteAMostrar, subtotalLocal, acumulado, totalAMostrar, ultimaPagina, renderFooter, transporteOverride);
            }
            catch { }

            AgregarPie(
                col,
                transporteAMostrar,
                totalAMostrar,
                ultimaPagina,
                observaciones);
        }

        return indice;
    }

    private void RenderMediaHojaItems(
        ColumnDescriptor col,
        Presupuesto presupuesto,
        List<ItemData> items,
        ref decimal acumulado,
        string copia,
        int pagina,
        int totalPaginas,
        decimal transporteOverride,
        decimal grandTotal,
        bool incrementarAcumulado = true,
        bool esUltimaPagina = false)
    {
        // Encabezado
        AgregarEncabezado(col, presupuesto, copia, pagina, totalPaginas);

        // Detalle
        decimal subtotalLocal = 0m;

        foreach (var item in items)
        {
            col.Item()
                .Text(FormatearLinea(item))
                .FontFamily(_config.FontName)
                .FontSize(_config.NormalFontSize);

            subtotalLocal += item.Importe;

            if (incrementarAcumulado)
                acumulado += item.Importe;
        }

        // Pie
        var observaciones = string.IsNullOrWhiteSpace(presupuesto.Observaciones)
            ? null
            : presupuesto.Observaciones.Trim();

        AgregarPie(col, transporteOverride, grandTotal, esUltimaPagina, observaciones);
    }

    private void AgregarPie(
    ColumnDescriptor col,
    decimal transporte,
    decimal total,
    bool ultimaPagina,
    string? observaciones)
    {
        col.Item().PaddingTop(3);

        col.Item()
            .Text(new string('-', 72))
            .FontFamily(_config.FontName);

        col.Item().PaddingTop(3);

        // Mostrar observaciones (si las hay) en la parte izquierda y total/transporte en la misma línea derecha
        col.Item().Row(r =>
        {
            if (!string.IsNullOrWhiteSpace(observaciones))
            {
                r.RelativeItem().Text("Observaciones: " + observaciones).FontFamily(_config.FontName);
            }
            else
            {
                r.RelativeItem().Text(string.Empty);
            }

            var rightContainer = r.ConstantItem(260).AlignRight();
            rightContainer.Text(t =>
            {
                if (ultimaPagina)
                {
                    t.Span("Total........$").Bold().FontFamily(_config.FontName).FontSize(_config.TotalFontSize);
                    t.Span(" " + total.ToString("N2")).Bold().FontFamily(_config.FontName).FontSize(_config.TotalFontSize);
                }
                else
                {
                    t.Span("Transporte $").Bold().FontFamily(_config.FontName).FontSize(_config.TotalFontSize);
                    t.Span(" " + transporte.ToString("N2")).Bold().FontFamily(_config.FontName).FontSize(_config.TotalFontSize);
                }
            });
        });

        // Deja unas líneas en blanco como hacía Clipper
        col.Item().Height(12);
    }

    private void AgregarEncabezado(
    ColumnDescriptor col,
    Presupuesto presupuesto,
    string copia,
    int pagina,
    int totalPaginas)
    {
        //-------------------------------------------------
        // Hoja
        //-------------------------------------------------

        col.Item()
            .AlignLeft()
            .Text($"Hoja {pagina} de {totalPaginas}")
            .FontFamily(_config.FontName)
            .FontSize(_config.NormalFontSize - 2);

        //-------------------------------------------------
        // Título
        //-------------------------------------------------

        col.Item()
            .AlignCenter()
            .Text(presupuesto.Tipo)
            .Bold()
            .FontSize(_config.TitleFontSize);

        //-------------------------------------------------
        // ORIGINAL / DUPLICADO
        //-------------------------------------------------

        col.Item()
            .AlignCenter()
            .Text(copia)
            .FontFamily(_config.FontName)
            .FontSize(_config.HeaderFontSize);

        col.Item().Height(6);

        //-------------------------------------------------
        // Fecha
        //-------------------------------------------------

        col.Item()
            .AlignRight()
            .Text($"FECHA: {presupuesto.Fecha:dd/MM/yyyy}")
            .FontFamily(_config.FontName);

        col.Item().Height(4);

        //-------------------------------------------------
        // Cliente / Código
        //-------------------------------------------------

        col.Item().Row(r =>
        {
            r.RelativeItem()
                .Text("Cliente: " + presupuesto.Cliente.Nombre)
                .FontFamily(_config.FontName);

            r.ConstantItem(180)
                .AlignRight()
                .Text("Codigo: " + presupuesto.Cliente.Codigo)
                .FontFamily(_config.FontName);
        });

        //-------------------------------------------------
        // Domicilio
        //-------------------------------------------------

        col.Item()
            .Text("Domicilio: " + presupuesto.Cliente.Domicilio)
            .FontFamily(_config.FontName);

        //-------------------------------------------------
        // Localidad
        //-------------------------------------------------

        col.Item()
            .Text("Localidad: " + presupuesto.Cliente.Localidad)
            .FontFamily(_config.FontName);

        col.Item().Height(6);

        //-------------------------------------------------
        // Cabecera
        //-------------------------------------------------

        col.Item()
            .Text(FormatearCabecera())
            .Bold()
            .FontFamily(_config.FontName);

        //-------------------------------------------------
        // Línea separadora
        //-------------------------------------------------

        col.Item()
            .Text(new string('-', 72))
            .FontFamily(_config.FontName);

        col.Item().Height(2);
    }

    private string FormatearCabecera()
    {
        return
            "Cant.".PadLeft(6) + " " +
            "Descripcion".PadRight(30) + " " +
            "Art.".PadLeft(5) + " " +
            "P.Unit.".PadLeft(10) + " " +
            "Dto.".PadLeft(4) + " " +
            "Importe".PadLeft(12);
    }

    private string FormatearLinea(ItemData item)
    {
        string descripcion = item.Descripcion ?? "";

        if (descripcion.Length > 30)
            descripcion = descripcion.Substring(0, 30);
        // Helper to center text inside a fixed width
        static string Center(string s, int width)
        {
            if (s.Length >= width)
                return s.Length == width ? s : s.Substring(0, width);

            int totalPad = width - s.Length;
            int padLeft = totalPad / 2;
            int padRight = totalPad - padLeft;
            return new string(' ', padLeft) + s + new string(' ', padRight);
        }

        var cantidad = item.Cantidad.ToString("0.##");
        var codigo = item.Codigo ?? "";
        var descuento = item.Descuento.ToString("0") + "%";

        return
            Center(cantidad, 6) + " " +
            descripcion.PadRight(30) + " " +
            Center(codigo, 5) + " " +
            item.Precio.ToString("0.00").PadLeft(10) + " " +
            Center(descuento, 4) + " " +
            item.Importe.ToString("0.00").PadLeft(12);
    }
}