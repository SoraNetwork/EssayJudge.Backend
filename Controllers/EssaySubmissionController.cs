using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Services;
using System.Threading.Tasks;
using SoraEssayJudge.Models;
using SoraEssayJudge.Data;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using SoraEssayJudge.Dtos;
using Microsoft.Extensions.Logging;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class EssaySubmissionController : ControllerBase
    {
        private readonly JudgeService _judgeService;
        private readonly EssayContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EssaySubmissionController> _logger;

        public EssaySubmissionController(JudgeService judgeService, EssayContext context, IWebHostEnvironment env, ILogger<EssaySubmissionController> logger)
        {
            _judgeService = judgeService;
            _context = context;
            _env = env;
            _logger = logger;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<IEnumerable<EssaySubmissionSummaryDto>>> GetSubmissionsSummary([FromQuery] int top, [FromQuery] Guid? studentId, [FromQuery] string? studentName)
        {
            _logger.LogInformation("Getting submission summary with Top: {Top}, StudentId: {StudentId}, StudentName: {StudentName}", top, studentId, studentName);
            if (!studentId.HasValue && string.IsNullOrEmpty(studentName))
            {
                _logger.LogWarning("GetSubmissionsSummary called without studentId or studentName.");
                return BadRequest("Either studentId or studentName must be provided.");
            }

            var query = _context.EssaySubmissions.AsQueryable();

            if (studentId.HasValue)
            {
                query = query.Where(s => s.StudentId == studentId.Value);
            }
            else if (!string.IsNullOrEmpty(studentName))
            {
                query = query.Where(s => s.Student != null && s.Student.Name.Contains(studentName));
            }

            var summaries = await query.OrderByDescending(s => s.CreatedAt)
                                       .Take(top)
                                       .Select(s => new EssaySubmissionSummaryDto
                                       {
                                           Id = s.Id,
                                           TitleContext = s.EssayAssignment != null ? s.EssayAssignment.TitleContext : null,
                                           FinalScore = s.FinalScore,
                                           IsError = s.IsError,
                                           CreatedAt = s.CreatedAt
                                       }).ToListAsync();

            _logger.LogInformation("Found {SummaryCount} submission summaries.", summaries.Count);
            return Ok(summaries);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] Guid essayAssignmentId, IFormFile imageFile, [FromForm] int columnCount)
        {
            _logger.LogInformation("Received new essay submission for assignment ID: {EssayAssignmentId}", essayAssignmentId);
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("Image file is required but was not provided.");
                return BadRequest("Image file is required.");
            }

            var assignment = await _context.EssayAssignments.FindAsync(essayAssignmentId);
            if (assignment == null)
            {
                _logger.LogWarning("Invalid EssayAssignmentId provided: {EssayAssignmentId}", essayAssignmentId);
                return BadRequest("Invalid EssayAssignmentId.");
            }

            // Create a unique path for the uploaded file
            var uploadsDir = Path.Combine(_env.ContentRootPath, "essayfiles");
            Directory.CreateDirectory(uploadsDir); // Ensure the directory exists
            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
            var filePath = Path.Combine(uploadsDir, uniqueFileName);

            _logger.LogInformation("Saving uploaded file to: {FilePath}", filePath);
            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            var submission = new EssaySubmission
            {
                Id = Guid.NewGuid(),
                EssayAssignmentId = essayAssignmentId,
                ImageUrl = $"/EssayFile/{uniqueFileName}", // 存储图片的URL
                ColumnCount = columnCount,
                CreatedAt = DateTime.UtcNow,
                StudentId = null // Will be updated later
            };

            _context.EssaySubmissions.Add(submission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Starting background judging process for submission ID: {SubmissionId}", submission.Id);
            _ = _judgeService.JudgeEssayAsync(submission.Id);

            return Ok(new { submissionId = submission.Id });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            _logger.LogInformation("Getting submission details for ID: {SubmissionId}", id);
            var submission = await _context.EssaySubmissions
                                           .Include(s => s.AIResults)
                                           .Include(s => s.EssayAssignment)
                                           .Include(s => s.Student)
                                           .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null)
            {
                _logger.LogWarning("Submission with ID: {SubmissionId} not found.", id);
                return NotFound();
            }

            // If judging is not complete (no AI results yet) and there's no error
            if (!submission.IsError && (submission.AIResults == null || !submission.AIResults.Any()))
            {
                if (!(submission.AIResults == null || !submission.AIResults.Any()))
                {
                    _logger.LogInformation("Submission {SubmissionId} is still being judged. AI results got.", id);
                    return Ok(new { status = "Judging is in progress.", parsedText = submission.ParsedText, AIResults = submission.AIResults });
                }
                // If OCR text is available, return it with a status message
                if (!string.IsNullOrEmpty(submission.ParsedText))
                {
                    _logger.LogInformation("Submission {SubmissionId} is still being judged. Returning current progress.", id);
                    return Ok(new { status = "Judging is in progress.", parsedText = submission.ParsedText });
                }
                // If OCR text is not yet available
                _logger.LogInformation("Submission {SubmissionId} is still being judged. OCR text not yet available.", id);
                return Ok(new { status = "Judging is in progress." });
            }

            _logger.LogInformation("Returning full submission details for ID: {SubmissionId}", id);
            // If judging is complete or there is an error, return the full submission object
            return Ok(submission);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubmission(Guid id, [FromBody] UpdateEssaySubmissionDto updateDto)
        {
            _logger.LogInformation("Updating submission {SubmissionId}", id);
            var submission = await _context.EssaySubmissions.FindAsync(id);
            if (submission == null)
            {
                _logger.LogWarning("Submission with ID: {SubmissionId} not found.", id);
                return NotFound();
            }

            if (updateDto.StudentId.HasValue)
            {
                submission.StudentId = updateDto.StudentId.Value;
            }
            if (updateDto.FinalScore.HasValue)
            {
                submission.Score = updateDto.FinalScore;
            }

            

            // 清除错误信息
            submission.IsError = false;
            if (submission.GetType().GetProperty("ErrorMessage") != null)
            {
                submission.GetType().GetProperty("ErrorMessage")?.SetValue(submission, null);
            }

            if (updateDto.FinalScore.HasValue)
            {
                submission.ErrorMessage = "Manual score updated.";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Submission {SubmissionId} updated successfully.", id);
            return Ok(submission);
        }
        [HttpDelete]
        public async Task<IActionResult> DeleteSubmission([FromQuery] Guid id)
        {
            _logger.LogInformation("Deleting submission with ID: {SubmissionId}", id);
            var submission = await _context.EssaySubmissions.FindAsync(id);
            if (submission == null)
            {
                _logger.LogWarning("Submission with ID: {SubmissionId} not found.", id);
                return NotFound();
            }

            _context.EssaySubmissions.Remove(submission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted submission with ID: {SubmissionId}", id);
            return NoContent();
        }
    }
}
