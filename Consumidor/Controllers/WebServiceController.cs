using Consumidor.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
namespace Consumidor.Controllers
{
    public class PagosController : Controller
    {
        private readonly HttpClient _httpClient;

        public PagosController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://localhost:44373/api/"); // Cambia por la URL de tu API
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new DatosPagos());
        }

        [HttpPost]
        public async Task<IActionResult> ProcesarPago(DatosPagos datos)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", datos);
            }

            try
            {
                var jsonContent = new StringContent(JsonSerializer.Serialize(datos), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("pagos/procesar-pago", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    ViewBag.Mensaje = "El pago se realizó correctamente.";
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    ViewBag.Mensaje = $"Error: {errorMessage}";
                }
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = $"Ocurrió un error al procesar el pago: {ex.Message}";
            }

            return View("Index", datos);
        }
    }
}
