using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WB_CHORO.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace WB_CHORO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsultaController:ControllerBase
    {
        private readonly string _connectionString;

        public ConsultaController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("consultar-pago")]
        public async Task<IActionResult> ProcesarConsulta(string Cliente, string Documento)
        {
            var resultado = new List<DatosConsulta>();

            try
            {
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
               
                    //Codigo para busqueda de CODIGO_BIC por la identidad del usuario
                    string codigoBic = null;
                    string nombreBic = null;
                    string apellidoBic = null;
                    using (var command = new SqlCommand(
                        "SELECT RTRIM(CLIENTE_TABLA.CODIGO_BIC) AS CODIGO_BIC, " +
                        "RTRIM(BASE_INFO_CENTRAL.NOMBRE_BIC) AS NOMBRE_BIC, " +
                        "RTRIM(BASE_INFO_CENTRAL.APELLIDO_BIC) AS APELLIDO_BIC " +
                        "FROM BASE_INFO_CENTRAL " +
                        "INNER JOIN CLIENTE_TABLA ON BASE_INFO_CENTRAL.CODIGO_BIC = CLIENTE_TABLA.CODIGO_BIC " +
                        "WHERE REPLACE(BASE_INFO_CENTRAL.DOCUMENTO_DE_IDENTIFICACI, '-', '') = REPLACE(@Cliente, '-', '')", connection))
                    {
                        command.Parameters.AddWithValue("@Cliente",Cliente);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                codigoBic = reader["CODIGO_BIC"].ToString();
                                nombreBic = reader["NOMBRE_BIC"].ToString();
                                apellidoBic = reader["APELLIDO_BIC"].ToString();
                            }
                        }
                    }
                    
                    //Correr el procedimiento almacenado
                    using (var command = new SqlCommand("CONSULTAR_PAGO", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Cliente", codigoBic);
                        command.Parameters.AddWithValue("@Documento", Documento);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                resultado.Add(new DatosConsulta
                                {
                                    Cliente = reader.GetString(reader.GetOrdinal("CLIENTE_CODIGO_BIC")),
                                    Documento = reader.GetString(reader.GetOrdinal("Documento")),
                                    Cuota = reader.GetDecimal(reader.GetOrdinal("CUOTA_DEL_CONTRATO")),
                                    Nombre = nombreBic,
                                    Apellido = apellidoBic,
                                    fechaInicio = reader.GetDateTime(reader.GetOrdinal("FECHA_DE_INICIO_DEL_CONTR")),
                                    negocio = reader.GetInt32(reader.GetOrdinal("NEGOCIO"))
                                });
                            }
                        }
                    }
                }
                if (resultado.Count == 0)
                {
                    return NotFound($"No se encontro ningun registro: {Cliente}");
                }
                return Ok(resultado);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, $"Error al consultar: {ex.Message}");
            }
        }

    }
}
