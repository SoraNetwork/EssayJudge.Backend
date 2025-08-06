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
            return await _context.ApiKeys
                .Include(k => k.AIModels)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiKey>> GetApiKey(Guid id)
        {
            var apiKey = await _context.ApiKeys
                .Include(k => k.AIModels)
                .FirstOrDefaultAsync(k => k.Id == id);

            if (apiKey == null)
            {
                return NotFound();
            }

            return apiKey;
        }

        [HttpPost]
        public async Task<ActionResult<ApiKey>> PostApiKey([FromForm] string serviceType, [FromForm] string key, [FromForm] string? secret, [FromForm] string? endpoint, [FromForm] string? description, [FromForm] List<string>? modelIds)
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
                UpdatedAt = DateTime.UtcNow,
            };

            if (modelIds != null && modelIds.Any())
            {
                apiKey.AIModels = new List<AIModel>();
                foreach (var modelId in modelIds.Distinct())
                {
                    apiKey.AIModels.Add(new AIModel { ModelId = modelId, ServiceType = apiKey.ServiceType });
                }
            }
        
            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetApiKey), new { id = apiKey.Id }, apiKey);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutApiKey(Guid id, 
            [FromForm] string serviceType, 
            [FromForm] string key, 
            [FromForm] bool isEnabled,
            [FromForm] string? secret, 
            [FromForm] string? endpoint, 
            [FromForm] string? description, 
            [FromForm] List<string>? modelIds)
        {
            var apiKeyToUpdate = await _context.ApiKeys
                .Include(k => k.AIModels)
                .ThenInclude(m => m.UsageSettings)
                .FirstOrDefaultAsync(k => k.Id == id);

            if (apiKeyToUpdate == null)
            {
                return NotFound();
            }

            // Update scalar properties from the provided parameters
            apiKeyToUpdate.ServiceType = serviceType;
            apiKeyToUpdate.Key = key;
            apiKeyToUpdate.Secret = secret;
            apiKeyToUpdate.Endpoint = endpoint;
            apiKeyToUpdate.Description = description;
            apiKeyToUpdate.IsEnabled = isEnabled;
            apiKeyToUpdate.UpdatedAt = DateTime.UtcNow;

            // Sync AIModels
            var existingModels = apiKeyToUpdate.AIModels?.ToList() ?? new List<AIModel>();
            var newModelIds = modelIds?.Distinct().ToList() ?? new List<string>();

            // Models to remove
            var modelsToRemove = existingModels
                .Where(m => !newModelIds.Contains(m.ModelId))
                .ToList();

            foreach (var model in modelsToRemove)
            {
                // Remove associated usage settings first
                if (model.UsageSettings != null && model.UsageSettings.Any())
                {
                    _context.AIModelUsageSettings.RemoveRange(model.UsageSettings);
                }
                _context.AIModels.Remove(model);
            }

            // Models to add
            var existingModelIds = existingModels.Select(m => m.ModelId).ToList();
            var modelIdsToAdd = newModelIds
                .Where(modelId => !existingModelIds.Contains(modelId))
                .ToList();

            foreach (var modelId in modelIdsToAdd)
            {
                apiKeyToUpdate.AIModels ??= new List<AIModel>();
                apiKeyToUpdate.AIModels.Add(new AIModel { ModelId = modelId, ServiceType = apiKeyToUpdate.ServiceType });
            }

            _context.Entry(apiKeyToUpdate).State = EntityState.Modified;

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
            var apiKey = await _context.ApiKeys
                .Include(k => k.AIModels)
                .ThenInclude(m => m.UsageSettings)
                .FirstOrDefaultAsync(k => k.Id == id);

            if (apiKey == null)
            {
                return NotFound();
            }

            if (apiKey.AIModels != null)
            {
                foreach (var model in apiKey.AIModels)
                {
                    if (model.UsageSettings != null && model.UsageSettings.Any())
                    {
                        _context.AIModelUsageSettings.RemoveRange(model.UsageSettings);
                    }
                }
                _context.AIModels.RemoveRange(apiKey.AIModels);
            }

            _context.ApiKeys.Remove(apiKey);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ApiKeyExists(Guid id)
        {
            return _context.ApiKeys.Any(e => e.Id == id);
        }

        [HttpGet("model-usage-settings")]
        public async Task<ActionResult<IEnumerable<AIModelUsageSetting>>> GetModelUsageSettings()
        {
            return await _context.AIModelUsageSettings
                .Include(s => s.AIModel)
                .OrderBy(s => s.UsageType)
                .ThenBy(s => s.AIModel != null ? s.AIModel.ModelId : "")
                .ToListAsync();
        }

        [HttpPost("model-usage-settings")]
        public async Task<ActionResult<AIModelUsageSetting>> CreateModelUsageSetting([FromForm] AIModelUsageSetting setting)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var modelExists = await _context.AIModels.AnyAsync(m => m.Id == setting.AIModelId);
            if (!modelExists)
            {
                ModelState.AddModelError(nameof(setting.AIModelId), "The specified AIModelId does not exist.");
                return BadRequest(ModelState);
            }

            setting.Id = Guid.NewGuid();
            setting.CreatedAt = DateTime.UtcNow;
            setting.UpdatedAt = DateTime.UtcNow;

            _context.AIModelUsageSettings.Add(setting);
            await _context.SaveChangesAsync();

            // It's better to have a GetById endpoint, but for now we return with the list endpoint's name.
            return CreatedAtAction(nameof(GetModelUsageSettings), new { id = setting.Id }, setting);
        }

        [HttpPut("model-usage-settings/{id}")]
        public async Task<IActionResult> UpdateModelUsageSetting(Guid id, [FromForm] string? usageType, [FromForm] bool? isEnabled, [FromForm] Guid? aiModelId)
        {
            var existingSetting = await _context.AIModelUsageSettings.FirstOrDefaultAsync(s => s.Id == id);

            if (existingSetting == null)
            {
            return NotFound();
            }

            // Update properties based on form data if provided
            if (usageType != null)
            {
            existingSetting.UsageType = usageType;
            }

            if (isEnabled.HasValue)
            {
            existingSetting.IsEnabled = isEnabled.Value;
            }

            if (aiModelId.HasValue && existingSetting.AIModelId != aiModelId.Value)
            {
             var modelExists = await _context.AIModels.AnyAsync(m => m.Id == aiModelId.Value);
             if (!modelExists)
             {
                 ModelState.AddModelError(nameof(aiModelId), "The specified AIModelId does not exist.");
                 return BadRequest(ModelState);
             }
             existingSetting.AIModelId = aiModelId.Value;
            }

            existingSetting.UpdatedAt = DateTime.UtcNow;

            try
            {
            await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
            if (!_context.AIModelUsageSettings.Any(e => e.Id == id))
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

        [HttpDelete("model-usage-settings/{id}")]
        public async Task<IActionResult> DeleteModelUsageSetting(Guid id)
        {
            var setting = await _context.AIModelUsageSettings.FindAsync(id);
            if (setting == null)
            {
                return NotFound();
            }

            _context.AIModelUsageSettings.Remove(setting);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("all-models")]
        public async Task<ActionResult<IEnumerable<AIModel>>> GetAllAIModels()
        {
            // Endpoint to get all available AI models
            return await _context.AIModels.ToListAsync();
        }
    }
}
