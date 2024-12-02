using Microsoft.Xrm.Sdk;

namespace WB_CHORO.Models
{
    public class DatosPagos
    {
        public string Cliente { get; set; }
        public string  Documento { get; set; }

        public decimal ValorPago { get; set; }
    }
}
