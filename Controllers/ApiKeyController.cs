using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using Microsoft.AspNetCore.Authorization;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApiKeyController : ControllerBase
    {
        private readonly EssayContext _context;

        public ApiKeyController(EssayContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApiKey>>> GetApiKeys()
        {
            return await _context.ApiKeys.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiKey>> GetApiKey(Guid id)
        {
            var apiKey = await _context.ApiKeys.FindAsync(id);

            if (apiKey == null)
            {
                return NotFound();
            }

            return apiKey;
        }

        [HttpPost]
        public async Task<ActionResult<ApiKey>> PostApiKey([FromForm] string serviceType, [FromForm] string key, [FromForm] string? secret, [FromForm] string? endpoint, [FromForm] string? description)
        {
            var apiKey = new ApiKey
            {
                ServiceType = serviceType,
                Key = key,
                Secret = secret,
                Endpoint = endpoint,
                Description = description,
                IsEnabled = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        
            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetApiKey), new { id = apiKey.Id }, apiKey);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutApiKey(Guid id, ApiKey apiKey)
        {
            if (id != apiKey.Id)
            {
                return BadRequest();
            }

            apiKey.UpdatedAt = DateTime.UtcNow;
            _context.Entry(apiKey).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApiKeyExists(id))
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

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleApiKey(Guid id)
        {
            var apiKey = await _context.ApiKeys.FindAsync(id);
            if (apiKey == null)
            {
                return NotFound();
            }

            apiKey.IsEnabled = !apiKey.IsEnabled;
            apiKey.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteApiKey(Guid id)
        {
            var apiKey = await _context.ApiKeys.FindAsync(id);
            if (apiKey == null)
            {
                return NotFound();
            }

            _context.ApiKeys.Remove(apiKey);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ApiKeyExists(Guid id)
        {
            return _context.ApiKeys.Any(e => e.Id == id);
        }
    }
}