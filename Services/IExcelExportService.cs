using SoraEssayJudge.Models.DTOs;

namespace SoraEssayJudge.Services
{
    public interface IExcelExportService
    {
        Task<byte[]> ExportEssaySubmissionsAsync(ExportFilterDto? filter = null);
        Task<byte[]> ExportEssaySubmissionsWithDetailsAsync(ExportFilterDto? filter = null);
    }
}