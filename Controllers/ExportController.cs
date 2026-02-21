using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Models.DTOs;
using SoraEssayJudge.Services;
using Microsoft.Extensions.Configuration;
using SoraEssayJudge.Data;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly IExcelExportService _excelExportService;
        private readonly IConfiguration _config;
        private readonly EssayContext _context;

        public ExportController(IExcelExportService excelExportService, IConfiguration config, EssayContext context)
        {
            _excelExportService = excelExportService;
            _config = config;
            _context = context;
        }

        private bool CheckPermission()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            return UserAccessController.CheckAccess(token, 2, _context, _config);
        }

        [HttpPost("essays")]
        public async Task<IActionResult> ExportEssaySubmissions([FromBody] ExportFilterDto? filter = null)
        {
            if (!CheckPermission())
            {
                return StatusCode(403);
            }

            try
            {
                var fileBytes = await _excelExportService.ExportEssaySubmissionsAsync(filter);
                var fileName = GenerateFileName(filter);

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"导出失败: {ex.Message}" });
            }
        }

        [HttpGet("essays")]
        public async Task<IActionResult> ExportEssaySubmissionsGet(
            [FromQuery] Guid? essayAssignmentId = null,
            [FromQuery] string? essayAssignmentIds = null,
            [FromQuery] Guid? classId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (!CheckPermission())
            {
                return StatusCode(403);
            }

            var filter = new ExportFilterDto
            {
                EssayAssignmentId = essayAssignmentId,
                ClassId = classId,
                StartDate = startDate,
                EndDate = endDate
            };

            if (!string.IsNullOrEmpty(essayAssignmentIds))
            {
                var idStrings = essayAssignmentIds.Split(',');
                filter.EssayAssignmentIds = idStrings
                    .Where(id => Guid.TryParse(id, out _))
                    .Select(Guid.Parse)
                    .ToList();
            }

            return await ExportEssaySubmissions(filter);
        }

        private string GenerateFileName(ExportFilterDto? filter)
        {
            var baseName = "作文评分报告";
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (filter?.EssayAssignmentId != null)
            {
                return $"{baseName}_单个测验_{timestamp}.xlsx";
            }
            else if (filter?.EssayAssignmentIds != null && filter.EssayAssignmentIds.Any())
            {
                return $"{baseName}_多个测验_{timestamp}.xlsx";
            }
            else if (filter?.ClassId != null)
            {
                return $"{baseName}_班级筛选_{timestamp}.xlsx";
            }

            return $"{baseName}_{timestamp}.xlsx";
        }
    }
}