using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoraEssayJudge.Data;
using SoraEssayJudge.Dtos;
using SoraEssayJudge.Services;
using SoraEssayJudge.Models;

namespace SoraEssayJudge.Controllers;

[ApiController]
[Route("/essay/studentupload")]
public class StudentUploadController : ControllerBase
{
    private readonly EssayContext _context;
    private readonly ILogger<StudentUploadController> _logger;
    private readonly JudgeService _judge;
    private readonly IPreProcessImageService _preProcessImageService;
    private readonly string _uploadPath;

    public StudentUploadController(
        JudgeService judge,
        EssayContext context, 
        ILogger<StudentUploadController> logger,
        IPreProcessImageService preProcessImageService,
        IConfiguration configuration)
    {
        _judge = judge;
        _context = context;
        _logger = logger;
        _preProcessImageService = preProcessImageService;
        _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "essayfiles");
        Directory.CreateDirectory(_uploadPath);
    }

    [HttpGet("studentinfo")]
    public async Task<ActionResult<IEnumerable<object>>> GetStudentInfo()
    {
        var classes = await _context.Classes
            .Include(c => c.Students)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.CreatedAt,
                Students = c.Students.Select(s => new StudentSummaryDto
                {
                    Id = s.Id,
                    StudentId = s.StudentId,
                    Name = s.Name
                })
            })
            .ToListAsync();

        return Ok(classes);
    }

    [HttpGet("assignments/{studentId}")]
    public async Task<ActionResult<IEnumerable<EssayAssignmentDto>>> GetEssayAssignmentsForStudent(string studentId)
    {
        var student = await _context.Students
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.StudentId == studentId);

        if (student == null)
        {
            return NotFound(new { Message = "找不到该学生" });
        }

        var assignments = await _context.EssayAssignments
            .Where(a => !a.Submissions.Any(s => s.Student.StudentId == studentId))
            .Select(a => new EssayAssignmentDto
            {
                Id = a.Id,
                Grade = a.Grade,
                TotalScore = a.TotalScore,
                BaseScore = a.BaseScore,
                Description = a.Description,
                TitleContext = a.TitleContext,
                ScoringCriteria = a.ScoringCriteria,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(assignments);
    }

    [HttpPost("checkimg")]
    public async Task<ActionResult<CheckImageResponseDto>> CheckImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Message = "没有上传文件" });
        }

        if (!file.ContentType.StartsWith("image/"))
        {
            return BadRequest(new { Message = "只接受图片文件" });
        }

        try
        {
            // 保存原始图片
            string originalFileName = $"{Guid.NewGuid()}_original{Path.GetExtension(file.FileName)}";
            string originalFilePath = Path.Combine(_uploadPath, originalFileName);

            using (var stream = new FileStream(originalFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 处理图片
            string processedImageName = await _preProcessImageService.ProcessImageAsync(originalFilePath);

            return Ok(new CheckImageResponseDto 
            { 
                Success = true,
                ProcessedImageUrl = "/essayfiles/"+processedImageName,
                Message = "图片处理成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片处理失败");
            return BadRequest(new { Message = "图片处理失败: " + ex.Message });
        }
    }
    [HttpPost("submit/hasprased")]
    public async Task<ActionResult<EssaySubmissionDto>> SubmitEssayWithProcessedImage([FromForm] CreateEssaySubmissionPrasedDto dto)
    {
        if (string.IsNullOrEmpty(dto.StudentId) || string.IsNullOrEmpty(dto.PrasedText))
        {
            return BadRequest(new { Message = "提交信息不完整" });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.StudentId == dto.StudentId);

        if (student == null)
        {
            return NotFound(new { Message = "找不到该学生" });
        }

        var assignment = await _context.EssayAssignments
            .FirstOrDefaultAsync(a => a.Id == dto.EssayAssignmentId);

        if (assignment == null)
        {
            return NotFound(new { Message = "找不到该作业" });
        }

        var submission = new EssaySubmission
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            Student = student,
            EssayAssignmentId = assignment.Id,
            EssayAssignment = assignment,
            CreatedAt = DateTime.UtcNow,
            IsError = false,
            ParsedText = dto.PrasedText,
            Score = 0
        };

        _context.EssaySubmissions.Add(submission);
        await _context.SaveChangesAsync();

        _ = _judge.JudgeEssayAsync(submission.Id);

        return Ok(new EssaySubmissionDto
        {
            Id = submission.Id,
        });
    }


    [HttpPost("submit")]
    public async Task<ActionResult<EssaySubmissionDto>> SubmitEssay([FromForm] CreateEssaySubmissionDto dto)
    {
        if (string.IsNullOrEmpty(dto.StudentId) || string.IsNullOrEmpty(dto.ProcessedImageUrl))
        {
            return BadRequest(new { Message = "提交信息不完整" });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.StudentId == dto.StudentId);

        if (student == null)
        {
            return NotFound(new { Message = "找不到该学生" });
        }

        var assignment = await _context.EssayAssignments
            .FirstOrDefaultAsync(a => a.Id == dto.EssayAssignmentId);

        if (assignment == null)
        {
            return NotFound(new { Message = "找不到该作业" });
        }

        var submission = new EssaySubmission
        {
            ColumnCount = dto.ColumnCount,
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            Student = student,
            EssayAssignmentId = assignment.Id,
            EssayAssignment = assignment,
            ImageUrl = dto.ProcessedImageUrl,
            CreatedAt = DateTime.UtcNow,
            IsError = false,
            Score = 0
        };

        _context.EssaySubmissions.Add(submission);
        await _context.SaveChangesAsync();

        _ =  _judge.JudgeEssayAsync(submission.Id);

        return Ok(new EssaySubmissionDto
        {
            Id = submission.Id,
        });
    }
}