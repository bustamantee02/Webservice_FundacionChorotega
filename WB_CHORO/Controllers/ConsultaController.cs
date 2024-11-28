using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WB_CHORO.Models;

namespace WB_CHORO.Controllers
{
    public class ConsultaController:ControllerBase
    {
        private readonly string _connectionString;

        public ConsultaController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("consulta-pago")]
        public async Task<IActionResult> ConsultarSaldo(string Cliente, string Documento)
        {
            var resultado = new List<DatosConsulta>();

            try
            {
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    string codigoBic = null;
                    using (var command = new SqlCommand(
                        "SELECT CLIENTE_TABLA.CODIGO_BIC " +
                        "FROM BASE_INFO_CENTRAL " +
                        "INNER JOIN CLIENTE_TABLA ON BASE_INFO_CENTRAL.CODIGO_BIC = CLIENTE_TABLA.CODIGO_BIC " +
                        "WHERE BASE_INFO_CENTRAL.DOCUMENTO_DE_IDENTIFICACI = @Cliente", connection))
                    {
                        command.Parameters.AddWithValue("@Cliente", Cliente.Replace("-", ""));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                codigoBic = reader["CODIGO_BIC"].ToString();
                            }
                        }
                    }

                    if (codigoBic == null)
                    {
                        return NotFound($"No se encontró el cliente: {Cliente}");
                    }

                    //Tomar el year

                    
                    //Correr el procedimiento almacenado
                    using (var command = new SqlCommand("CONSULTAR_PAGO", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Cliente", Cliente.Replace("-", ""));
                        command.Parameters.AddWithValue("@Documento", Documento);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                resultado.Add(new DatosConsulta
                                {
                                    Cliente = reader.GetString(reader.GetOrdinal("CLIENTE_CODIGO_BIC")),
                                    Documento = reader.GetString(reader.GetOrdinal("Documento")),
                                    Cuota = reader.GetDecimal(reader.GetOrdinal("CUOTA_DEL_CONTRATO"))
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
