using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Xrm.Sdk;
using System.Data;
using System;
using WB_CHORO.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace WB_CHORO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReversarController : ControllerBase
    {
        private readonly string _connectionString;
        public ReversarController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpPost("reversar-pago")]
        public async Task<IActionResult> ReversarPago([FromBody] DatosReversar request)
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
                    //Datos Dinamicos 
                    string PuntoVenta = "PV0011";

                    //Para mandar a llamar el proceso
                    string proceso;
                    using (var getProcesoCommand = new SqlCommand(
                        "SELECT NUMERACION_DE_REVERSION_DE_RECIBOS FROM PUNTO_DE_VENTA_DA WHERE CODIGO_BIC = @PuntoVenta;", connection))
                    {
                        getProcesoCommand.Parameters.AddWithValue("@PuntoVenta", PuntoVenta);
                        var result = await getProcesoCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            proceso = result.ToString();
                        }
                        else
                        {
                            return NotFound("No se encontró el proceso.");
                        }
                    }

                    int numPartida = 1;
                    using (var command = new SqlCommand(
                    "DECLARE @Proceso NVARCHAR(6); " +
                    "SELECT @Proceso = NUMERACION_DE_REVERSION_DE_RECIBOS FROM PUNTO_DE_VENTA_DA WHERE CODIGO_BIC = @PuntoVenta; " +
                    "SELECT ISNULL(MAX(NUMERO_ULTIMA_PARTIDA_CON), 0) + 1 " +
                    "FROM NUMERACION_PARTIDAS WHERE PREFIJO_PARTIDA_CONTABLE = @Proceso;", connection))
                    {

                        command.Parameters.AddWithValue("@PuntoVenta", PuntoVenta);
                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            numPartida = Convert.ToInt32(result);
                        }
                    }

                    //Actualizacion de la tabla de NUMERACION_PARTIDAS
                    using (var updateCommand = new SqlCommand(
                   "UPDATE NUMERACION_PARTIDAS " +
                   "SET NUMERO_ULTIMA_PARTIDA_CON = @NumPartida " +
                   "WHERE PREFIJO_PARTIDA_CONTABLE = (SELECT NUMERACION_DE_REVERSION_DE_RECIBOS FROM PUNTO_DE_VENTA_DA WHERE CODIGO_BIC = @PuntoVenta);", connection))
                    {
                        updateCommand.Parameters.AddWithValue("@NumPartida", numPartida);
                        updateCommand.Parameters.AddWithValue("@PuntoVenta", PuntoVenta);
                        await updateCommand.ExecuteNonQueryAsync();
                    }


                    //Validacion de la Existencia del cliente
                    string exisCliente;
                    using (var command = new SqlCommand(
                        "SELECT CLIENTE_TABLA.CODIGO_BIC " +
                        "FROM BASE_INFO_CENTRAL INNER JOIN CLIENTE_TABLA ON " +
                        "BASE_INFO_CENTRAL.CODIGO_BIC = CLIENTE_TABLA.CODIGO_BIC" +
                        "WHERE (BASE_INFO_CENTRAL.DOCUMENTO_DE_IDENTIFICACI = @Cliente);", connection))
                    {
                        exisCliente = request.Cliente;

                        if (exisCliente == null)
                        {
                            return NotFound("No existe el cliente en las dos tablas");
                        }
                    }
                    //Insert del cliente ya con la validacion 
                    string codigoBic = null;
                    using (var command = new SqlCommand(
                        "SELECT CLIENTE_TABLA.CODIGO_BIC " +
                        "FROM BASE_INFO_CENTRAL " +
                        "INNER JOIN CLIENTE_TABLA ON BASE_INFO_CENTRAL.CODIGO_BIC = CLIENTE_TABLA.CODIGO_BIC " +
                        "WHERE REPLACE(BASE_INFO_CENTRAL.DOCUMENTO_DE_IDENTIFICACI, '-', '') = REPLACE(@Cliente, '-', '')", connection))
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

                    //Codigo para sacar la forma de pago 
                    string formaPago;

                    using (var insertCommand = new SqlCommand(
                        "SELECT CODIGO_FORMAS_PAGO FROM PUNTO_DE_VENTA WHERE CODIGO_BIC = @PuntoVenta;", connection))
                    {
                        insertCommand.Parameters.AddWithValue("@PuntoVenta", PuntoVenta);
                        var result = await insertCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            formaPago = result.ToString();
                        }
                        else
                        {
                            return NotFound("No se encontró la forma de pago.");
                        }
                    }

                    // Dividir el valor de DocumentoICP en dos partes
                    string prefDocumento = null;
                    int numDocumento = 0;

                    if (!string.IsNullOrEmpty(request.Documento))
                    {
                        var partes = request.Documento.Split('-');
                        if (partes.Length == 2)
                        {
                            prefDocumento = partes[0];
                            if (!int.TryParse(partes[1], out numDocumento))
                            {
                                return BadRequest("El formato de Documento no es valido.");
                            }
                        }
                        else
                        {
                            return BadRequest("El Documento no existe.");
                        }
                    }
                    else
                    {
                        return BadRequest("El Documento no puede estar vacío.");
                    }


                    //Codigo para sacar la forma de pago 
                    string moneda;

                    using (var command = new SqlCommand(
                        "SELECT MONEDA_ORIGEN_CODIGO_DE_M FROM FACTURAS_SERVICIOS_CONTROL WHERE PREFIJO_PARTIDA_CONTABLE = @prefDocumento AND NUMERO_DE_FACTURA_DE_SERV = @numDocumento;", connection))
                    {
                        command.Parameters.AddWithValue("@prefDocumento", prefDocumento);
                        command.Parameters.AddWithValue("@numDocumento", numDocumento);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            moneda = result.ToString();
                        }
                        else
                        {
                            return NotFound("No se encontró el codigo de moneda");
                        }
                    }

                    //Tomar el valor de la cuota
                    int cuota;
                    string procesoPago = "RECCON";
                    using (var command = new SqlCommand(
                        "SELECT TOP 1 VALOR_TRANSACCION_ORIG2JT FROM TRN_CLIENTE_DIARIO_MOV WHERE PREFIJO_PARTIDA_CONTABLE = @Proceso2 " +
                        "AND CLIENTE_CODIGO_BIC = @Cliente AND DOCUMENTO_TRANSACCION_ICP = @DocumentoICP " +
                        "ORDER BY FECHA_DE_TRANSACCION DESC;", connection))
                    {
                        command.Parameters.AddWithValue("@Proceso2", procesoPago);
                        command.Parameters.AddWithValue("@DocumentoICP", request.Documento);
                        command.Parameters.AddWithValue("@Cliente", codigoBic);
                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            cuota = Convert.ToInt32(result);
                        }
                        else
                        {
                            return NotFound("No se encontró una cuota.");
                        }
                    }

                    //Valor de cambio 
                    decimal valCambio;

                    using (var getValorCommand = new SqlCommand(
                        "SELECT TOP 1 VALOR_TASA_DE_CAMBIO FROM TASAS_DE_CAMBIO WHERE FECHA_DE_VIGENCIA_TASA_CA<=@FechaActual ORDER BY FECHA_DE_VIGENCIA_TASA_CA DESC;", connection))
                    {
                        getValorCommand.Parameters.AddWithValue("@FechaActual", DateTime.Now);
                        var result = await getValorCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            valCambio = Convert.ToDecimal(result);
                        }
                        else
                        {
                            return NotFound("No se encontró el valor de cambio.");
                        }
                    }

                    //Fecha de factura
                    DateTime dateTime;

                    using (var command = new SqlCommand(
                        "SELECT FECHA_FACTURA_SERVICIO FROM FACTURAS_SERVICIOS_CONTROL WHERE PREFIJO_PARTIDA_CONTABLE = @prefDocumento AND NUMERO_DE_FACTURA_DE_SERV = @numDocumento;", connection))
                    {
                        command.Parameters.AddWithValue("@prefDocumento", prefDocumento);
                        command.Parameters.AddWithValue("@numDocumento", numDocumento);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            dateTime = Convert.ToDateTime(result.ToString());
                        }
                        else
                        {
                            return NotFound("No se encontró la fecha");
                        }
                    }

                    //Definicion del proceso 
                    string defProceso;

                    using (var getProcesoCommand = new SqlCommand(
                        "SELECT NUMERACION_DE_REVERSION_DE_RECIBOS FROM PUNTO_DE_VENTA_DA WHERE CODIGO_BIC = @PuntoVenta;", connection))
                    {
                        getProcesoCommand.Parameters.AddWithValue("@PuntoVenta", PuntoVenta);
                        var result = await getProcesoCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            defProceso = result.ToString();
                        }
                        else
                        {
                            return NotFound("No se encontró uan definicion del proceso.");
                        }
                    }

                    //YEAR del periodo contable 
                    int year;

                    using (var getYearCommand = new SqlCommand(
                        "SELECT ANO_DEL_PERIODO FROM PERIODO_CONTABLE WHERE @FechaActual >= FECHA_DE_INICIO_DEL_PERIO "
                        + " AND @FechaActual < DATEADD(DAY, 1, FECHA_FIN_DEL_PERIODO); ", connection))
                    {
                        getYearCommand.Parameters.AddWithValue("@FechaActual", DateTime.Now);
                        var result = await getYearCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            year = Convert.ToInt32(result);
                        }
                        else
                        {
                            return NotFound("No se encontró el year.");
                        }
                    }

                    //Mes del periodo contable 
                    string mes;

                    using (var getMesCommand = new SqlCommand(
                        "SELECT NUMERO_DEL_PERIODO FROM PERIODO_CONTABLE WHERE @FechaActual >= FECHA_DE_INICIO_DEL_PERIO " +
                        "AND @FechaActual < DATEADD(DAY, 1, FECHA_FIN_DEL_PERIODO);", connection))
                    {
                        getMesCommand.Parameters.AddWithValue("@FechaActual", DateTime.Now);
                        var result = await getMesCommand.ExecuteScalarAsync();
                        if (result != null)
                        {
                            mes = result.ToString();
                        }
                        else
                        {
                            return NotFound("No se encontró el mes.");
                        }
                    }

                    //Usuario 
                    string usuario;
                    using (var insertCommand = new SqlCommand(
                        "INSERT INTO TRN_CLIENTE_DIARIO_MOV (USUARIO_ADICION) VALUES (@Usuario);", connection))
                    {
                        usuario = request.Usuario;
                    }

                    //codigo para correr el procedimiento almacenado
                    using (var command = new SqlCommand("REVERSAR_PAGO", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.AddWithValue("@Proceso", proceso);
                        command.Parameters.AddWithValue("@NumPartida", numPartida);
                        command.Parameters.AddWithValue("@Cliente", codigoBic);
                        command.Parameters.AddWithValue("@FormaPago", formaPago);
                        command.Parameters.AddWithValue("@DocumentoICP", request.Documento);
                        command.Parameters.AddWithValue("@Moneda", moneda);
                        command.Parameters.AddWithValue("@Cuota", cuota);
                        command.Parameters.AddWithValue("@valCambio", valCambio);
                        command.Parameters.AddWithValue("@Fecha", dateTime);
                        command.Parameters.AddWithValue("@PuntoVenta", PuntoVenta);
                        command.Parameters.AddWithValue("@defProceso", defProceso);
                        command.Parameters.AddWithValue("@Year", year);
                        command.Parameters.AddWithValue("@numPeriodo", mes);
                        command.Parameters.AddWithValue("@Usuario", usuario);

                        await command.ExecuteNonQueryAsync();
                    }
                    connection.Close();
                }
                return Ok("La reversion se realizó correctamente!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Reversion fallida.:{ex.Message}");
            }
        }
    }
}
