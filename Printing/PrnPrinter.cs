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

        int totalPaginas =
            (int)Math.Ceiling(
                (double)presupuesto.Items.Count /
                _config.ItemsPerPage);

        decimal grandTotal = presupuesto.Items.Sum(i => i.Importe);

        Document.Create(document =>
        {
            int indice = 0;
            int pagina = 1;

            decimal acumulado = 0;

            while (indice < presupuesto.Items.Count)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);

                    page.Margin(15);

                    page.DefaultTextStyle(x => x
                        .FontFamily(_config.FontName)
                        .FontSize(_config.NormalFontSize));

                    page.Content().Column(col =>
                    {
                        //---------------------------------------------------
                        // ORIGINAL
                        //---------------------------------------------------

                        int inicio = indice;

                        // calcular los items que se imprimirán en esta página
                        var pageItems = presupuesto.Items.Skip(indice).Take(_config.ItemsPerPage).ToList();
                        var subtotalPagina = pageItems.Sum(i => i.Importe);

                        // transporte acumulado hasta esta página (incluye esta página)
                        var transporteAcumulado = acumulado + subtotalPagina;

                        // Renderizar ORIGINAL usando la lista de items calculada
                        bool esUltimaPagina = (indice + pageItems.Count) >= presupuesto.Items.Count;

                        RenderMediaHojaItems(
                            col,
                            presupuesto,
                            pageItems,
                            ref acumulado,
                            "ORIGINAL",
                            pagina,
                            totalPaginas,
                            transporteAcumulado,
                            grandTotal,
                            incrementarAcumulado: true,
                            esUltimaPagina: esUltimaPagina);

                        // avanzamos el indice por la cantidad de items procesados
                        indice += pageItems.Count;

                        // Log para depuración de subtotales
                        try
                        {
                            Serilog.Log.Information("PRN: Pagina {pagina} subtotalPagina={subtotal} acumulado={acumulado}", pagina, subtotalPagina, acumulado);
                        }
                        catch { }

                        col.Item().Height(10);

                        col.Item()
                            .Text(new string('-', 72))  
                            .FontFamily(_config.FontName);

                        col.Item().Height(10);

                        //---------------------------------------------------
                        // DUPLICADO
                        //---------------------------------------------------

                        // Renderizamos el duplicado sin volver a incrementar el acumulado
                        // y usando el mismo transporte/subtotal de la original
                        // Renderizamos el duplicado sin volver a incrementar el acumulado
                        // pero mostrando el mismo transporte/subtotal de la original
                        // Duplicate should display same transporte and same ultimaPagina flag
                        RenderMediaHojaItems(
                            col,
                            presupuesto,
                            pageItems,
                            ref acumulado,
                            "DUPLICADO",
                            pagina,
                            totalPaginas,
                            transporteAcumulado,
                            grandTotal,
                            incrementarAcumulado: false,
                            esUltimaPagina: esUltimaPagina);
                    });
                });

                pagina++;
            }

        }).GeneratePdf(archivoPdf);

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

        return
            item.Cantidad.ToString("0.##").PadLeft(6) + " " +
            descripcion.PadRight(30) + " " +
            item.Codigo.PadLeft(5) + " " +
            item.Precio.ToString("0.00").PadLeft(10) + " " +
            (item.Descuento.ToString("0") + "%").PadLeft(4) + " " +
            item.Importe.ToString("0.00").PadLeft(12);
    }
}