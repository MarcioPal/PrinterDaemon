using System;
using System.Collections.Generic;
using System.Text;

namespace PrinterDaemon.Printing.Models
{
    public class Presupuesto
    {
        public string Tipo { get; set; } = "";
        public DateTime Fecha { get; set; }

        public ClienteData Cliente { get; set; } = new();

        public List<ItemData> Items { get; set; } = [];

        public string Observaciones { get; set; } = "";

        public decimal Total { get; set; }
    }

    public class ClienteData
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Domicilio { get; set; } = "";
        public string Localidad { get; set; } = "";
    }

    public class ItemData
    {
        public decimal Cantidad { get; set; }

        public string Codigo { get; set; } = "";

        public string Descripcion { get; set; } = "";

        public decimal Precio { get; set; }

        public decimal Descuento { get; set; }

        public decimal Importe { get; set; }
    }
}
