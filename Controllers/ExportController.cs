using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Models.DTOs;
using SoraEssayJudge.Services;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly IExcelExportService _excelExportService;

        public ExportController(IExcelExportService excelExportService)
        {
            _excelExportService = excelExportService;
        }

        [HttpPost("essays")]
        public async Task<IActionResult> ExportEssaySubmissions([FromBody] ExportFilterDto? filter = null)
        {
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
            [FromQuery] string? essayAssignmentIds = null, // 逗号分隔的多个ID
            [FromQuery] Guid? classId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var filter = new ExportFilterDto
            {
                EssayAssignmentId = essayAssignmentId,
                ClassId = classId,
                StartDate = startDate,
                EndDate = endDate
            };

            // 解析多个测验ID
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