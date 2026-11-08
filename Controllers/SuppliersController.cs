using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using InventoryAPI.Models;
using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace InventoryAPI.Controllers
{
    public class SuppliersController(ISupplierService supplierService, AppDbContext db) : BaseApiController
    {
        private readonly ISupplierService _supplierService = supplierService;
        private static readonly HashSet<string> AllowedFiscalKeys = new(StringComparer.Ordinal)
        {
            "tipo", "actividad_economica", "ubigeo", "numero_trabajadores",
            "tipo_facturacion", "tipo_contabilidad", "comercio_exterior",
            "via_tipo", "via_nombre", "zona_codigo", "zona_tipo",
            "numero", "interior", "lote", "dpto", "manzana", "kilometro",
            "es_agente_retencion", "es_buen_contribuyente"
        };

        // La instantánea llega del navegador: validar forma y campos permitidos,
        // evitando guardar JSON arbitrario o textos excesivos.
        private static bool TryNormalizeFiscalData(string? source, out string? normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(source)) return true;
            if (source.Length > 10000) return false;
            try
            {
                using var document = JsonDocument.Parse(source);
                if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
                var data = new Dictionary<string, JsonElement>();
                foreach (var field in document.RootElement.EnumerateObject())
                {
                    if (!AllowedFiscalKeys.Contains(field.Name)) continue;
                    if (field.Value.ValueKind is JsonValueKind.String)
                    {
                        if ((field.Value.GetString()?.Length ?? 0) > 500) return false;
                    }
                    else if (field.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
                        return false;
                    data[field.Name] = field.Value.Clone();
                }
                normalized = JsonSerializer.Serialize(data);
                return true;
            }
            catch (JsonException) { return false; }
        }


        private bool CanManageMaster => User.FindFirst("RoleId")?.Value is "1" or "2";
        private Task<Inventory?> Master(int company) => db.Inventories
            .FirstOrDefaultAsync(i => i.CompanyId == company && i.IsMaster && i.IsActive);
        private Task<bool> MayCreate(int company, int inventory)
        {
            var userId = ObtenerUsuarioSeguro();
            var developer = User.FindFirst("RoleId")?.Value == "1";
            return db.Inventories.AnyAsync(i =>
                i.CompanyId == company && i.IsActive && !i.IsMaster &&
                (i.Id == inventory || inventory == 0) &&
                (developer || i.UserId == userId ||
                 db.SharedInventories.Any(sh => sh.CompanyId == company &&
                    sh.InventoryId == i.Id && sh.UserId == userId &&
                    sh.AccessLevel == SharedInventory.AccessMode.Editor)));
        }

        [HttpGet("ruc/{ruc}")]
        public async Task<IActionResult> ConsultarRuc(string ruc)
        {
            int company = ObtenerEmpresaSegura();
            if (!Regex.IsMatch(ruc, @"^\d{11}$"))
                return BadRequest(new { error = "El RUC debe tener 11 dígitos." });
            // El usuario solicitó expresamente consultar SUNAT: actualizar siempre
            // los datos fiscales, aunque el proveedor ya exista en este catálogo.
            var result = await _supplierService.ConsultarRucAsync(ruc);
            if (!result.Success)
            {
                // Contingencia: solo datos previamente registrados por ESTA empresa.
                var cached = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s =>
                    s.CompanyId == company && s.Ruc == ruc && s.IsActive);
                if (cached is not null)
                {
                    cached.LookupFromCache = true;
                    return Ok(cached);
                }
                if (result.Message.Contains("crítica")) return StatusCode(500, new { error = result.Message });
                if (result.Message.Contains("no fue localizado")) return NotFound(new { error = result.Message });
                return BadRequest(new { error = result.Message });
            }
            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetSuppliers()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _supplierService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSupplier(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var supplier = await _supplierService.GetByIdAsync(id);

            if (supplier == null || supplier.CompanyId != companyId) return NotFound();
            return Ok(supplier);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutSupplier(int id, [FromBody] Supplier supplier)
        {
            if (id != supplier.Id || !ModelState.IsValid) return BadRequest(ModelState);
            if (!CanManageMaster) return Forbid();
            var company = ObtenerEmpresaSegura();
            var master = await Master(company);
            if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
            var existing = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id &&
                s.CompanyId == company && s.InventoryId == master.Id && s.IsActive);
            if (existing is null) return NotFound();
            if (!Regex.IsMatch(supplier.Ruc ?? "", @"^\d{11}$")) return BadRequest(new { error = "RUC inválido." });
            if (await db.Suppliers.AnyAsync(s => s.CompanyId == company && s.Ruc == supplier.Ruc &&
                s.Id != id && s.IsActive))
                return Conflict(new { error = "Ya existe un proveedor con ese RUC." });

            existing.Ruc = supplier.Ruc;
            existing.BusinessName = supplier.BusinessName.Trim();
            existing.ContactName = supplier.ContactName;
            existing.Phone = supplier.Phone;
            existing.Email = supplier.Email;
            existing.Address = supplier.Address;
            existing.Estado = supplier.Estado;
            existing.Condicion = supplier.Condicion;
            existing.Distrito = supplier.Distrito;
            existing.Provincia = supplier.Provincia;
            existing.Departamento = supplier.Departamento;
            existing.StatusId = supplier.StatusId;
            existing.RelationshipDescription = supplier.RelationshipDescription?.Trim();
            if (!TryNormalizeFiscalData(supplier.FiscalDataJson, out var fiscalData))
                return BadRequest(new { error = "Los datos fiscales adicionales tienen un formato inválido." });
            // Mantener los datos anteriores si el cliente no envió una nueva consulta.
            existing.FiscalDataJson = fiscalData ?? existing.FiscalDataJson;
            await db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostSupplier([FromBody] Supplier supplier)
        {
            if (!ModelState.IsValid || !Regex.IsMatch(supplier.Ruc ?? "", @"^\d{11}$"))
                return BadRequest(new { error = "El proveedor necesita un RUC válido de 11 dígitos." });
            int company = ObtenerEmpresaSegura();
            var master = await Master(company);
            if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
            if (!await MayCreate(company, supplier.InventoryId) && supplier.InventoryId != master.Id)
                return Forbid();
            if (supplier.InventoryId == master.Id && !await MayCreate(company, 0))
                return Forbid();

            var existing = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s =>
                s.CompanyId == company && s.Ruc == supplier.Ruc && s.IsActive);
            if (existing is not null) { existing.Reused = true; return Ok(existing); }
            supplier.Id = 0;
            supplier.CompanyId = company;
            supplier.InventoryId = master.Id;
            supplier.IsActive = true;
            supplier.BusinessName = supplier.BusinessName.Trim();
            supplier.RelationshipDescription = supplier.RelationshipDescription?.Trim();
            if (!TryNormalizeFiscalData(supplier.FiscalDataJson, out var fiscalData))
                return BadRequest(new { error = "Los datos fiscales adicionales tienen un formato inválido." });
            supplier.FiscalDataJson = fiscalData;
            try
            {
                db.Suppliers.Add(supplier);
                await db.SaveChangesAsync();
                return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, supplier);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                pg.ConstraintName == "IX_Suppliers_Company_Ruc")
            {
                db.Entry(supplier).State = EntityState.Detached;
                var conflict = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s =>
                    s.CompanyId == company && s.Ruc == supplier.Ruc && s.IsActive);
                if (conflict is null) return Conflict(new { error = "RUC duplicado. Actualiza el catálogo." });
                conflict.Reused = true;
                return Ok(conflict);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            if (!CanManageMaster) return Forbid();
            int company = ObtenerEmpresaSegura();
            var master = await Master(company);
            if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s =>
                s.Id == id && s.CompanyId == company && s.InventoryId == master.Id);
            if (supplier is null) return NotFound();
            if (await db.Articles.AnyAsync(a => a.CompanyId == company && a.SupplierId == id))
                return Conflict(new { error = "El proveedor se utiliza en artículos y no se puede desactivar." });
            supplier.IsActive = false;
            await db.SaveChangesAsync();
            return NoContent();
        }
    }
}