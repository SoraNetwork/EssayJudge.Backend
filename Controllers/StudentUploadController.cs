using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoraEssayJudge.Data;
using SoraEssayJudge.Dtos;
using SoraEssayJudge.Services;
using SoraEssayJudge.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace SoraEssayJudge.Controllers;

[ApiController]
[Route("/essay/studentupload")]
public class StudentUploadController : ControllerBase
{
    private readonly EssayContext _context;
    private readonly ILogger<StudentUploadController> _logger;
    private readonly JudgeService _judge;
    private readonly IPreProcessImageService _preProcessImageService;
    private readonly IImageStitchingService _imageStitchingService;
    private readonly IPreProcessImageServiceV2 _preProcessImageServiceV2;
    private readonly string _uploadPath;

    public StudentUploadController(
        JudgeService judge,
        EssayContext context, 
        ILogger<StudentUploadController> logger,
        IPreProcessImageService preProcessImageService,
        IPreProcessImageServiceV2 preProcessImageServiceV2,
        IImageStitchingService imageStitchingService,
        IConfiguration configuration)
    {
        _judge = judge;
        _context = context;
        _logger = logger;
        _preProcessImageService = preProcessImageService;
        _preProcessImageServiceV2 = preProcessImageServiceV2;
        _imageStitchingService = imageStitchingService;
        _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "essayfiles");
        Directory.CreateDirectory(_uploadPath);
    }

    [HttpGet("query/{shortId}")]
    public async Task<ActionResult<EssaySubmission>> GetSubmissionByShortId(string shortId)
    {
        _logger.LogInformation("Querying for submission with short ID: {ShortId}", shortId);
        if (string.IsNullOrEmpty(shortId) || shortId.Length != 8)
        {
            _logger.LogWarning("Invalid shortId provided: {ShortId}. It must be 8 characters long.", shortId);
            return BadRequest(new { Message = "无效的查询ID。ID必须是8个字符。" });
        }


        // Using raw SQL for SQLite to match the last 8 characters of the GUID (stored as TEXT).
        var submission = await _context.EssaySubmissions
            .FromSql($"SELECT * FROM EssaySubmissions WHERE SUBSTR(Id, -8) = {shortId}")
            .Include(s => s.Student)
            .Include(s => s.EssayAssignment)
            .Include(s => s.AIResults)
            .FirstOrDefaultAsync();

        if (submission == null)
        {
            _logger.LogWarning("No submission found for short ID: {ShortId}", shortId);
            return NotFound(new { Message = "找不到对应的提交记录。" });
        }

        _logger.LogInformation("Found submission with ID {SubmissionId} for short ID: {ShortId}", submission.Id, shortId);
        return Ok(submission);
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
    [HttpPost("checkimg/V2")]
    public async Task<ActionResult<CheckImageResponseDto>> CheckImageV2(IFormFile file)
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
            // 保存并压缩为WebP
            string originalFileName = $"{Guid.NewGuid()}_original.webp";
            string originalFilePath = Path.Combine(_uploadPath, originalFileName);

            using (var stream = file.OpenReadStream())
            using (var image = await Image.LoadAsync(stream))
            {
                var encoder = new WebpEncoder
                {
                    Quality = 80 // 调整压缩质量
                };
                await image.SaveAsync(originalFilePath, encoder);
            }

            // 处理图片
            string processedImageName = await _preProcessImageServiceV2.PreProcessImageAsync(originalFilePath);

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
            // 保存并压缩为WebP
            string originalFileName = $"{Guid.NewGuid()}_original.webp";
            string originalFilePath = Path.Combine(_uploadPath, originalFileName);

            using (var stream = file.OpenReadStream())
            using (var image = await Image.LoadAsync(stream))
            {
                var encoder = new WebpEncoder
                {
                    Quality = 80 // 调整压缩质量
                };
                await image.SaveAsync(originalFilePath, encoder);
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

    [HttpPost("checkimg/columns")]
    public async Task<ActionResult<CheckImageResponseDto>> CheckImageColumns(IFormFileCollection files)
    {
        _logger.LogInformation("Received request for CheckImageColumns with {FileCount} files.", files.Count);

        if (files == null || files.Count == 0)
        {
            _logger.LogWarning("No files uploaded, returning 400 Bad Request.");
            return BadRequest(new { Message = "没有上传文件" });
        }

        foreach (var file in files)
        {
            if (!file.ContentType.StartsWith("image/"))
            {
                _logger.LogWarning("Invalid file type detected: {ContentType}. Returning 400 Bad Request.", file.ContentType);
                return BadRequest(new { Message = $"只接受图片文件，收到了不支持的类型: {file.ContentType}" });
            }
            _logger.LogInformation("File received: {FileName}, ContentType: {ContentType}, Size: {Length} bytes.", file.FileName, file.ContentType, file.Length);
        }

        try
        {
            var imageStreams = new List<Stream>();
            foreach (var file in files)
            {
                var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                imageStreams.Add(memoryStream);
            }

            _logger.LogInformation("All files copied to memory streams. Starting image stitching...");
            // 1. Stitch images together
            string stitchedImageName = await _imageStitchingService.StitchImagesAsync(imageStreams, _uploadPath);
            string stitchedImagePath = Path.Combine(_uploadPath, stitchedImageName);
            _logger.LogInformation("Image stitching complete. Stitched image saved as: {StitchedImageName}", stitchedImageName);

            _logger.LogInformation("Starting post-processing for the stitched image...");
            // 2. Process the stitched image (same logic as checkimg)
            string processedImageName = await _preProcessImageService.ProcessImageAsync(stitchedImagePath);
            _logger.LogInformation("Post-processing complete. Final image name: {ProcessedImageName}", processedImageName);


            // Clean up streams
            foreach (var stream in imageStreams)
            {
                stream.Dispose();
            }

            return Ok(new CheckImageResponseDto 
            { 
                Success = true,
                ProcessedImageUrl = "/essayfiles/" + processedImageName,
                Message = "图片拼接和处理成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the multi-image processing workflow.");
            return BadRequest(new { Message = "图片处理失败: " + ex.Message });
        }
    }
}
