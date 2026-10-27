using System.Globalization;
using System.Text;
using PrinterDaemon.Printing.Models;

namespace PrinterDaemon.Printing;

public static class LvParser
{
    public static VentasReporte Parse(string archivo)
    {
        var reporte = new VentasReporte();
        var lineas = File.ReadLines(archivo, Encoding.UTF8);
        bool encabezado = true;

        foreach (var linea in lineas)
        {
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            if (encabezado)
            {
                encabezado = false;
                continue;
            }

            var campos = SepararCampos(linea);
            if (campos.Count < 12)
                throw new FormatException($"La línea de LV.TXT tiene {campos.Count} campos y se esperaban 12: {linea}");

            var registro = campos[0].Trim();
            CargarCabecera(reporte, campos);

            switch (registro)
            {
                case "VENTA_CONTADO":
                    reporte.Movimientos.Add(new VentaMovimiento
                    {
                        Fecha = ParseDate(campos[7]),
                        Factura = campos[9].Trim(),
                        Importe = ParseDecimal(campos[10]),
                        Observacion = campos[11].Trim()
                    });
                    break;
                case "TOTAL_CONTADO":
                    reporte.TotalContado = ParseDecimal(campos[10]);
                    break;
                case "TOTAL_CUENTA_CORRIENTE":
                    reporte.TotalCuentaCorriente = ParseDecimal(campos[10]);
                    break;
                case "SALDO_CUENTA_CORRIENTE_FECHA":
                    reporte.SaldoCuentaCorrienteFecha = ParseDecimal(campos[10]);
                    break;
                case "TOTAL_GENERAL":
                    reporte.TotalGeneral = ParseDecimal(campos[10]);
                    break;
                case "SALDO_ACTUAL_CUENTA_CORRIENTE":
                    reporte.SaldoActualCuentaCorriente = ParseDecimal(campos[10]);
                    break;
            }
        }

        return reporte;
    }

    private static void CargarCabecera(VentasReporte reporte, IReadOnlyList<string> campos)
    {
        if (string.IsNullOrWhiteSpace(reporte.Empresa)) reporte.Empresa = campos[1].Trim();
        if (reporte.FechaEmision == default) reporte.FechaEmision = ParseDate(campos[2]) ?? default;
        if (string.IsNullOrWhiteSpace(reporte.CodigoCliente)) reporte.CodigoCliente = campos[3].Trim();
        if (string.IsNullOrWhiteSpace(reporte.Cliente)) reporte.Cliente = campos[4].Trim();
        reporte.FechaDesde ??= ParseDate(campos[5]);
        reporte.FechaHasta ??= ParseDate(campos[6]);
    }

    private static List<string> SepararCampos(string linea)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        bool entreComillas = false;

        for (int i = 0; i < linea.Length; i++)
        {
            char caracter = linea[i];
            if (caracter == '"')
            {
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    entreComillas = !entreComillas;
                }
            }
            else if (caracter == ';' && !entreComillas)
            {
                campos.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(caracter);
            }
        }

        campos.Add(actual.ToString());
        return campos;
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParseExact(
            value.Trim(),
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result) ? result : null;
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(
            value.Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result) ? result : 0m;
    }
}
