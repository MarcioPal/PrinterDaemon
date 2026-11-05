namespace PrinterDaemon.Printing;

public class PrinterService
{
    private readonly PdfPrinter _pdf;
    private readonly BmpPrinter _bmp;
    private readonly PrnPrinter _prn;
    private readonly LvPrinter _lv;

    public PrinterService(
        PdfPrinter pdf,
        BmpPrinter bmp,
        PrnPrinter prn,
        LvPrinter lv)
    {
        _pdf = pdf;
        _bmp = bmp;
        _prn = prn;
        _lv = lv;
    }

    public string Print(string archivo)
    {
        string ext = Path.GetExtension(archivo).ToLowerInvariant();

        switch (ext)
        {
            case ".pdf":
                _pdf.Print(archivo);
                return archivo;

            case ".bmp":
                _bmp.Print(archivo);
                return archivo;

            case ".prn":
                {
                    string pdf = _prn.Print(archivo);

                    _pdf.Print(pdf);

                    return pdf;
                }

            case ".txt":
                if (!string.Equals(Path.GetFileName(archivo), "LV.TXT", StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Archivo TXT no soportado: {Path.GetFileName(archivo)}");

                {
                    string pdf = _lv.Print(archivo);
                    _pdf.Print(pdf);
                    return pdf;
                }

            default:
                throw new Exception($"Formato no soportado: {ext}");
        }
    }
}