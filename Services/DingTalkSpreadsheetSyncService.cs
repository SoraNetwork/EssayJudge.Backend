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
    public class DingTalkSpreadsheetSyncService : BackgroundService
    {
        private readonly ILogger<DingTalkSpreadsheetSyncService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public DingTalkSpreadsheetSyncService(
            ILogger<DingTalkSpreadsheetSyncService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DingTalk Spreadsheet Sync Service is starting.");

            try
            {
                // 等待一段时间让其他服务初始化
                await Task.Delay(10000, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Starting scheduled sync of DingTalk spreadsheets at {Time}", DateTime.UtcNow);

                    try
                    {
                        await SyncAllAssignmentsWithStudents(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred during scheduled sync of DingTalk spreadsheets.");
                    }

                    // 每5分钟同步一次
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DingTalk Spreadsheet Sync Service encountered a fatal error.");
            }
        }

        private async Task SyncAllAssignmentsWithStudents(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var spreadsheetService = scope.ServiceProvider.GetRequiredService<IEssayAssignmentSpreadsheetService>();
            var context = scope.ServiceProvider.GetRequiredService<EssayContext>();

            // 获取所有作文作业
            var assignments = await context.EssayAssignments.ToListAsync(cancellationToken);
            _logger.LogInformation("Found {Count} essay assignments to sync", assignments.Count);

            foreach (var assignment in assignments)
            {
                try
                {
                    _logger.LogInformation("Syncing spreadsheet for assignment {AssignmentId}", assignment.Id);
                    
                    // 确保表格存在并同步数据
                    await spreadsheetService.SyncAssignmentSpreadsheetAsync(assignment.Id);
                    
                    _logger.LogInformation("Successfully synced spreadsheet for assignment {AssignmentId}", assignment.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync spreadsheet for assignment {AssignmentId}", assignment.Id);
                }
            }

            _logger.LogInformation("Completed scheduled sync of all DingTalk spreadsheets");
        }
    }
}