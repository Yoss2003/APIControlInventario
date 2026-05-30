using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Data;
using ControlInventario.Shared.Models;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient; // 👇 AJUSTE 1: Añadimos el motor de peticiones HTTP para internet

        // Ajustamos el constructor para que reciba ambos servicios por Inyección de Dependencias
        public SuppliersController(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // ==========================================
        // 🌟 NUEVO ENDPOINT: CONSULTA INTELIGENTE RUC + PERSISTENCIA
        // ==========================================
        [HttpGet("ruc/{ruc}")]
        public async Task<IActionResult> ConsultarRuc(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 11)
            {
                return BadRequest(new { error = "El RUC debe tener exactamente 11 dígitos numéricos." });
            }

            try
            {
                // 1. CACHÉ LOCAL: Buscamos primero en tu tabla 'suppliers'
                var proveedorExistente = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Ruc == ruc);

                if (proveedorExistente != null)
                {
                    return Ok(proveedorExistente);
                }

                string apiToken = "sk_13723.G2u2hnJk9acgY3uFHZMYsJliHho0GXu4";

                // 2. INTENTO 1: Intentamos con el Endpoint Avanzado (/full)
                Debug.WriteLine($"[INTENTO 1] Consultando RUC Avanzado para {ruc}...");
                string urlAvanzada = $"https://api.decolecta.com/v1/sunat/ruc/full?numero={ruc}";

                var request = new HttpRequestMessage(HttpMethod.Get, urlAvanzada);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    var nuevoProveedor = new Supplier
                    {
                        InventoryId = 0,
                        Ruc = ruc,
                        BusinessName = root.TryGetProperty("razon_social", out var rz) ? rz.GetString() ?? "SIN NOMBRE LEGAL" : "SIN NOMBRE LEGAL",
                        Address = root.TryGetProperty("direccion", out var dir) ? dir.GetString() ?? "Dirección no registrada" : "Dirección no registrada",
                        StatusId = 1,
                        Estado = root.TryGetProperty("estado", out var est) ? est.GetString() ?? "ACTIVO" : "ACTIVO",
                        Condicion = root.TryGetProperty("condicion", out var cond) ? cond.GetString() ?? "HABIDO" : "HABIDO",
                        Distrito = root.TryGetProperty("distrito", out var dist) ? dist.GetString() ?? "" : "",
                        Provincia = root.TryGetProperty("provincia", out var prov) ? prov.GetString() ?? "" : "",
                        Departamento = root.TryGetProperty("departamento", out var dep) ? dep.GetString() ?? "" : ""
                    };

                    _context.Suppliers.Add(nuevoProveedor);
                    await _context.SaveChangesAsync();
                    return Ok(nuevoProveedor);
                }

                // 🌟 3. INTENTO 2 (FALLBACK): Si el avanzado falló, disparamos el Endpoint Básico
                Debug.WriteLine($"[FALLBACK] El RUC avanzado no retornó datos corporativos. Intentando endpoint básico para {ruc}...");
                string urlBasica = $"https://api.decolecta.com/v1/sunat/ruc?numero={ruc}";

                var requestBasico = new HttpRequestMessage(HttpMethod.Get, urlBasica);
                requestBasico.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var responseBasica = await _httpClient.SendAsync(requestBasico);

                if (responseBasica.IsSuccessStatusCode)
                {
                    string jsonString = await responseBasica.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    // Mapeo seguro tolerante a camelCase (básico) o snake_case
                    string razonSocial = (root.TryGetProperty("razonSocial", out var rz1) ? rz1.GetString() : null)
                     ?? (root.TryGetProperty("razon_social", out var rz2) ? rz2.GetString() : null)
                     ?? "SIN NOMBRE LEGAL";

                    string direccion = root.TryGetProperty("direccion", out var dir1) ? dir1.GetString() ?? "Dirección no registrada" : "Dirección no registrada";
                    string estado = root.TryGetProperty("estado", out var est1) ? est1.GetString() ?? "ACTIVO" : "ACTIVO";
                    string condicion = root.TryGetProperty("condicion", out var cond1) ? cond1.GetString() ?? "HABIDO" : "HABIDO";
                    string distrito = root.TryGetProperty("distrito", out var dist1) ? dist1.GetString() ?? "" : "";
                    string provincia = root.TryGetProperty("provincia", out var prov1) ? prov1.GetString() ?? "" : "";
                    string departamento = root.TryGetProperty("departamento", out var dep1) ? dep1.GetString() ?? "" : "";

                    var nuevoProveedorBasico = new Supplier
                    {
                        InventoryId = 0,
                        Ruc = ruc,
                        BusinessName = razonSocial ?? "SIN NOMBRE LEGAL",
                        Address = direccion,
                        StatusId = 1,
                        Estado = estado,
                        Condicion = condicion,
                        Distrito = distrito,
                        Provincia = provincia,
                        Departamento = departamento
                    };

                    _context.Suppliers.Add(nuevoProveedorBasico);
                    await _context.SaveChangesAsync();
                    return Ok(nuevoProveedorBasico);
                }

                return NotFound(new { error = $"El RUC {ruc} no fue localizado en SUNAT (Fallo en consulta avanzada y básica)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Falla crítica en el ecosistema en cascada de RUC.", detalle = ex.Message });
            }
        }

        // ==========================================
        // MÉTODOS CRUD ESTÁNDAR (Mantenidos intactos)
        // ==========================================

        // GET: api/Suppliers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers()
        {
            return await _context.Suppliers.ToListAsync();
        }

        // GET: api/Suppliers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Supplier>> GetSupplier(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);

            if (supplier == null)
            {
                return NotFound();
            }

            return supplier;
        }

        // PUT: api/Suppliers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSupplier(int id, Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return BadRequest();
            }

            _context.Entry(supplier).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupplierExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Suppliers
        [HttpPost]
        public async Task<ActionResult<Supplier>> PostSupplier(Supplier supplier)
        {
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSupplier", new { id = supplier.Id }, supplier);
        }

        // DELETE: api/Suppliers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.Id == id);
        }
    }
}