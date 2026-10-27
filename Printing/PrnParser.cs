using System.Globalization;
using PrinterDaemon.Printing.Models;

namespace PrinterDaemon.Printing;

public static class PrnParser
{
    public static Presupuesto Parse(string archivo)
    {
        var data = new Presupuesto();

        foreach (var linea in File.ReadLines(archivo))
        {
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            var c = linea.Split('|');

            switch (c[0])
            {
                case "CAB":

                    data.Tipo = c[1];

                    if (c.Length > 2)
                    {
                        data.Fecha = DateTime.ParseExact(
                            c[2].Trim(),
                            ["dd/MM/yyyy", "yyyyMMdd", "yyyy-MM-dd"],
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None);
                    }

                    break;

                case "CLI":

                    data.Cliente.Codigo = c[1];
                    data.Cliente.Nombre = c[2];
                    data.Cliente.Domicilio = c[3];
                    data.Cliente.Localidad = c[4];

                    break;

                case "DET":

                    data.Items.Add(new ItemData
                    {
                        Cantidad = decimal.Parse(c[1], CultureInfo.InvariantCulture),
                        Codigo = c[2],
                        Descripcion = c[3],
                        Precio = decimal.Parse(c[4], CultureInfo.InvariantCulture),
                        Descuento = decimal.Parse(c[5], CultureInfo.InvariantCulture),
                        Importe = decimal.Parse(c[6], CultureInfo.InvariantCulture)
                    });

                    break;

                case "OBS":

                    data.Observaciones = c[1];

                    break;

                case "TOT":

                    data.Total = decimal.Parse(
                        c[1],
                        CultureInfo.InvariantCulture);

                    break;
            }
        }

        return data;
    }
}