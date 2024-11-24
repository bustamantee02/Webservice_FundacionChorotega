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

        [HttpGet("consulta-pago/{numPartida}")]
        public async Task<IActionResult> ConsultarSaldo(int numPartida)
        {
            var resultado = new List<DatosConsulta>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand("CONSULTAR_PAGO", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@NumPartida", numPartida);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                resultado.Add(new DatosConsulta
                                {
                                    Proceso = reader.GetString(reader.GetOrdinal("PREFIJO_PARTIDA_CONTABLE")),
                                    NumPartida = reader.GetInt32(reader.GetOrdinal("NUMERO_DE_PARTIDA")),
                                    Linea = reader.GetInt32(reader.GetOrdinal("LINEA")),
                                    Documento = reader.GetString(reader.GetOrdinal("DOCUMENTO_TRANSACCION_ICP"))
                                });
                            }
                        }
                    }
                }
                if (resultado.Count == 0)
                {
                    return NotFound($"No se encontro ningun registro: {numPartida}");
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
