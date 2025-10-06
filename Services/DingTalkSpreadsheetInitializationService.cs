using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoraEssayJudge.Data;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SoraEssayJudge.Services
{
    public class DingTalkSpreadsheetInitializationService : BackgroundService
    {
        private readonly ILogger<DingTalkSpreadsheetInitializationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public DingTalkSpreadsheetInitializationService(
            ILogger<DingTalkSpreadsheetInitializationService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DingTalk Spreadsheet Initialization Service is starting.");

            try
            {
                // Wait a bit for other services to initialize
                await Task.Delay(5000, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var spreadsheetService = scope.ServiceProvider.GetRequiredService<IEssayAssignmentSpreadsheetService>();
                var context = scope.ServiceProvider.GetRequiredService<EssayContext>();

                // Ensure spreadsheets exist for all existing assignments
                var assignments = await context.EssayAssignments.ToListAsync(stoppingToken);
                foreach (var assignment in assignments)
                {
                    try
                    {
                        await spreadsheetService.EnsureAssignmentSpreadsheetAsync(assignment.Id);
                        _logger.LogInformation("Ensured spreadsheet exists for assignment {AssignmentId}", assignment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to ensure spreadsheet for assignment {AssignmentId}", assignment.Id);
                    }
                }

                _logger.LogInformation("DingTalk Spreadsheet Initialization Service completed initialization.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DingTalk Spreadsheet Initialization Service encountered an error during startup.");
            }
        }
    }
}