using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Data;
using System.Diagnostics;
using WB_CHORO.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace WB_CHORO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PagosController : ControllerBase
    {
        private readonly string _connectionString;

        public PagosController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpPost("procesar-pago")]
        public async Task<IActionResult> ProcesarPago([FromBody] Datos request)
        {
            if (request == null)
            {
                return BadRequest("No pueden haber datos nulos.");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    //Codigo para sacar funcion de proceso en el procedimiento almacenado
                    string proceso;

                    using (var getProcesoCommand = new SqlCommand(
                        "SELECT NUMERACION_DE_RECIBOS FROM PUNTO_DE_VENTA WHERE CODIGO_BIC = 'PV0011';", connection))
                    {
                        var result = await getProcesoCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            proceso = result.ToString();
                        }
                        else
                        {
                            return NotFound("No se encontró un valor para NUMERACION_DE_RECIBO en PUNTO_DE_VENTA.");
                        }
                    }

                    //Codigo que toma el ultimo numero de partida segun el proceso y le suma 1
                    int numPartida = 1;
                       using (var command = new SqlCommand(
                       "DECLARE @Proceso NVARCHAR(6); " +
                       "SELECT @Proceso = NUMERACION_DE_RECIBOS FROM PUNTO_DE_VENTA WHERE CODIGO_BIC = 'PV0011'; " +
                       "SELECT ISNULL(MAX(NUMERO_DE_PARTIDA), 0) + 1 " +
                       "FROM TRN_CLIENTE_DIARIO_MOV WHERE PREFIJO_PARTIDA_CONTABLE = @Proceso;", connection))
                        {
                               var result = await command.ExecuteScalarAsync();
                               if (result != null)
                               {
                                      numPartida = Convert.ToInt32(result);
                               }
                        }

                    //Codigo para busqueda de CODIGO_BIC por la identidad del usuario
                    string codigoBic = null;
                    using (var command = new SqlCommand(
                        "SELECT CLIENTE_TABLA.CODIGO_BIC " +
                        "FROM BASE_INFO_CENTRAL " +
                        "INNER JOIN CLIENTE_TABLA ON BASE_INFO_CENTRAL.CODIGO_BIC = CLIENTE_TABLA.CODIGO_BIC " +
                        "WHERE BASE_INFO_CENTRAL.DOCUMENTO_DE_IDENTIFICACI = @Cliente", connection))
                    {
                        command.Parameters.AddWithValue("@Cliente", request.Cliente);

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
                        return NotFound($"No se encontró el cliente: {request.Cliente}");
                    }


                    //codigo para correr el procedimiento almacenado
                    using (var command = new SqlCommand("PROCESAR_PAGO", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.AddWithValue("@Proceso", proceso);
                        command.Parameters.AddWithValue("@NumPartida", numPartida);
                        command.Parameters.AddWithValue("@Cliente_codigoBic", codigoBic);

                        await command.ExecuteNonQueryAsync();
                    }
                    connection.Close();
                }

                return Ok("El pago se realizó correctamente!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Pago fallido.: {ex.Message}");
                
            }
        }
    }
}

