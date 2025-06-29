using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SoraEssayJudge.Dtos;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class EssayAssignmentController : ControllerBase
    {
        private readonly EssayContext _context;
        private readonly ILogger<EssayAssignmentController> _logger;

        public EssayAssignmentController(EssayContext context, ILogger<EssayAssignmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EssayAssignmentDto>>> GetAssignments([FromQuery] int? top, [FromQuery] Guid? id, [FromQuery] string? title)
        {
            _logger.LogInformation("Getting assignments with Top: {Top}, ID: {Id}, Title: {Title}", top, id, title);
            var query = _context.EssayAssignments.AsQueryable();

            if (id.HasValue)
            {
                query = query.Where(a => a.Id == id.Value);
            }

            if (!string.IsNullOrEmpty(title))
            {
                query = query.Where(a => a.TitleContext != null && a.TitleContext.Contains(title));
            }

            if (top.HasValue)
            {
                query = query.OrderByDescending(a => a.CreatedAt).Take(top.Value);
            }

            var assignments = await query.Select(a => new EssayAssignmentDto
            {
                Id = a.Id,
                Grade = a.Grade,
                TotalScore = a.TotalScore,
                TitleContext = a.TitleContext,
                CreatedAt = a.CreatedAt
            }).ToListAsync();

            _logger.LogInformation("Found {AssignmentCount} assignments.", assignments.Count);
            return Ok(assignments);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] string grade, [FromForm] int totalScore, [FromForm] int baseScore, [FromForm] string? titleContext, [FromForm] string? scoringCriteria)
        {
            _logger.LogInformation("Creating a new essay assignment with Grade: {Grade}, TotalScore: {TotalScore}", grade, totalScore);
            var assignment = new EssayAssignment
            {
                Id = Guid.NewGuid(),
                Grade = grade,
                TotalScore = totalScore,
                BaseScore = baseScore,
                TitleContext = titleContext,
                ScoringCriteria = scoringCriteria,
                CreatedAt = DateTime.UtcNow
            };

            _context.EssayAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created essay assignment with ID: {AssignmentId}", assignment.Id);
            return Ok(new { assignmentId = assignment.Id });
        }
    }
}
