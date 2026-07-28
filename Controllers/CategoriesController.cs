using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Data;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            var categories = await _context.Categories
                .Include(c => c.CategoryMeasurementUnits)
                .ToListAsync();

            foreach (var cat in categories)
            {
                if (cat.CategoryMeasurementUnits != null)
                {
                    cat.SelectedUnitIds = cat.CategoryMeasurementUnits.Select(cmu => cmu.MeasurementUnitId).ToList();
                }
            }

            return Ok(categories);
        }

        // GET: api/Categories/
        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            return category;
        }

        // PUT: api/Categories/
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCategory(int id, Category category)
        {
            if (id != category.Id) return BadRequest(new { mensaje = "El ID no coincide." });

            var categoriaExistente = await _context.Categories.FindAsync(id);
            if (categoriaExistente == null) return NotFound(new { mensaje = "Categoría no encontrada." });

            _context.Entry(categoriaExistente).CurrentValues.SetValues(category);

            if (category.SelectedUnitIds != null)
            {
                var unidadesViejas = await _context.CategoryMeasurementUnits
                                                   .Where(cmu => cmu.CategoryId == id)
                                                   .ToListAsync();

                _context.CategoryMeasurementUnits.RemoveRange(unidadesViejas);

                foreach (var unitId in category.SelectedUnitIds)
                {
                    _context.CategoryMeasurementUnits.Add(new CategoryMeasurementUnit
                    {
                        CategoryId = id,
                        MeasurementUnitId = unitId
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(categoriaExistente);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error crítico al guardar en BD", detalle = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<Category>> PostCategory(Category category)
        {
            category.CategoryMeasurementUnits = null;

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            if (category.SelectedUnitIds != null && category.SelectedUnitIds.Any())
            {
                foreach (var unitId in category.SelectedUnitIds)
                {
                    _context.CategoryMeasurementUnits.Add(new CategoryMeasurementUnit
                    {
                        CategoryId = category.Id,
                        MeasurementUnitId = unitId
                    });
                }
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction("GetCategory", new { id = category.Id }, category);
        }

        // DELETE: api/Categories/
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { mensaje = "Categoría no encontrada." });
            }

            bool tieneHijas = await _context.Categories.AnyAsync(c => c.ParentCategoryId == id);
            if (tieneHijas)
            {
                return BadRequest(new { mensaje = "No puedes eliminar una categoría padre que aún contiene subcategorías." });
            }

            bool tieneArticulos = await _context.Articles.AnyAsync(a => a.CategoryId == id);
            if (tieneArticulos)
            {
                return BadRequest(new { mensaje = "No puedes eliminar esta categoría porque existen artículos registrados en ella." });
            }

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Categoría eliminada con éxito." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error crítico al intentar eliminar la categoría.", detalle = ex.Message });
            }
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
    }
}
