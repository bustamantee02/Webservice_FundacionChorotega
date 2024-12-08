using System;
using System.Reflection.Metadata.Ecma335;

namespace WB_CHORO.Models
{
    public class DatosConsulta
    {
        public string Cliente { get; set; }
        public string Documento { get; set; }
        public  decimal Cuota { get; set; }

        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public DateTime fechaInicio { get; set; }
        public int negocio { get; set; }
    }
}
