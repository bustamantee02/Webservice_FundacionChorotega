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
                //Codigo para busqueda de CODIGO_BIC por la identidad del usuario
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
                        command.Parameters.AddWithValue("@Cliente", request.Cliente);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                codigoBic = reader["CODIGO_BIC"].ToString();
                            }
                        }
                        connection.Close(); 
                    }

                    if (codigoBic == null)
                    {
                        return NotFound($"No se encontró el cliente: {request.Cliente}");
                    }


                        /*
                        string codigoBic1 = "PV0011";
                    string numRecibos = null;
                    using (var command = new SqlCommand("select NUMERACION_DE_RECIBOS" +
                        "from PUNTO_DE_VISTA" +
                        "where CODIGO_BIC = @CodigoBic", connection))
                    {
                        connection.Open();
                        command.Parameters.AddWithValue("@CodigoBic", codigoBic1);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                numRecibos = reader["NUMERACION_DE_RECIBOS"].ToString();
                            }
                        }
                        connection.Close();
                    }

                    if (numRecibos == null)
                    {
                        return NotFound($"No se encontro el Proceso: {codigoBic1}");
                    }

                    using (var command = new SqlCommand(
                        "UPDATE TRN_CLIENTE_DIARIO_MOV" +
                        "SET PREFIJO_PARTIDA_CONTABLE = @numRecibos" +
                        "WHERE CODIGO_BIC = @CodigoBic", connection))
                    {
                        connection.Open();
                        command.Parameters.AddWithValue("@numRecibos", numRecibos);
                        command.Parameters.AddWithValue("@CodigoBic", codigoBic1);

                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        connection.Close();

                        if (rowsAffected == 0)
                        {
                            return NotFound($"No se encontro el Proceso :{codigoBic1}");
                        }
                    }

                        //  
                        */

                        //Codigo para tomar el ultimo NUMERO_DE_PARTIDA y le sume 1
                        int numPartida = 1;
                    using (var command = new SqlCommand(
                        "SELECT ISNULL(MAX(NUMERO_DE_PARTIDA), 0) + 1 FROM TRN_CLIENTE_DIARIO_MOV WHERE PREFIJO_PARTIDA_CONTABLE = @Proceso", connection))
                    {
                        connection.Open();
                        command.Parameters.AddWithValue("@Proceso", request.Proceso);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            numPartida = Convert.ToInt32(result);
                        }
                        connection.Close();
                    }
                      


                    //codigo para correr el procedimiento almacenado
                    using (var command = new SqlCommand("PROCESAR_PAGO", connection))
                    {
                        connection.Open();

                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.AddWithValue("@Proceso", request.Proceso);
                        command.Parameters.AddWithValue("@NumPartida", numPartida);
                        command.Parameters.AddWithValue("@CodigoBic", codigoBic);

                        await command.ExecuteNonQueryAsync();
                    }
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

