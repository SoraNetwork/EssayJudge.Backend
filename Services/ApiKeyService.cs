using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using Microsoft.EntityFrameworkCore;

namespace SoraEssayJudge.Services
{
    public class ApiKeyService
    {
        private readonly EssayContext _context;

        public ApiKeyService(EssayContext context)
        {
            _context = context;
        }

        public async Task<ApiKey> GetAvailableKey(string serviceProvider)
        {
            return await _context.ApiKeys.FirstOrDefaultAsync(k => k.ServiceType == serviceProvider && k.IsEnabled);
        }
    }
}