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
        [HttpGet("{id}")]
        public async Task<ActionResult<EssayAssignmentDto>> GetAssignmentById(Guid id)
        {
            _logger.LogInformation("Getting assignment with ID: {Id}", id);
            var assignment = await _context.EssayAssignments.FindAsync(id);
            if (assignment == null)
            {
                _logger.LogWarning("Assignment with ID: {Id} not found.", id);
                return NotFound();
            }

            var assignmentDto = new EssayAssignmentDto
            {
                Id = assignment.Id,
                Grade = assignment.Grade,
                TotalScore = assignment.TotalScore,
                BaseScore = assignment.BaseScore,
                TitleContext = assignment.TitleContext,
                Description = assignment.Description,
                ScoringCriteria = assignment.ScoringCriteria,
                CreatedAt = assignment.CreatedAt
            };

            _logger.LogInformation("Found assignment with ID: {Id}", id);
            return Ok(assignmentDto);
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
                CreatedAt = a.CreatedAt,
                Description = a.Description,
                BaseScore = a.BaseScore,
                ScoringCriteria = a.ScoringCriteria
            }).ToListAsync();

            _logger.LogInformation("Found {AssignmentCount} assignments.", assignments.Count);
            return Ok(assignments);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] string grade, [FromForm] int totalScore, [FromForm] int baseScore, [FromForm] string? Description, [FromForm] string? titleContext, [FromForm] string? scoringCriteria)
        {
            _logger.LogInformation("Creating a new essay assignment with Grade: {Grade}, TotalScore: {TotalScore}", grade, totalScore);
            var assignment = new EssayAssignment
            {
                Id = Guid.NewGuid(),
                Grade = grade,
                TotalScore = totalScore,
                BaseScore = baseScore,
                Description = Description,
                TitleContext = titleContext,
                ScoringCriteria = scoringCriteria,
                CreatedAt = DateTime.UtcNow
            };

            _context.EssayAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created essay assignment with ID: {AssignmentId}", assignment.Id);
            return Ok(new { assignmentId = assignment.Id });
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAssignment( Guid id)
        {
            _logger.LogInformation("Deleting essay assignment with ID: {Id}", id);
            var assignment = await _context.EssayAssignments.FindAsync(id);
            if (assignment == null)
            {
                _logger.LogWarning("Essay assignment with ID: {Id} not found.", id);
                return NotFound();
            }

            _context.EssayAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted essay assignment with ID: {Id}", id);
            return NoContent();
        }
        [HttpPut]
        public async Task<IActionResult> UpdateAssignment([FromBody] EssayAssignmentDto updateDto)
        {
            _logger.LogInformation("Updating essay assignment with ID: {Id}", updateDto.Id);
            var assignment = await _context.EssayAssignments.FindAsync(updateDto.Id);
            if (assignment == null)
            {
                _logger.LogWarning("Essay assignment with ID: {Id} not found.", updateDto.Id);
                return NotFound();
            }

            assignment.Grade = updateDto.Grade;
            assignment.TotalScore = updateDto.TotalScore;
            assignment.BaseScore = updateDto.BaseScore;
            assignment.Description = updateDto.Description;
            assignment.TitleContext = updateDto.TitleContext;
            assignment.ScoringCriteria = updateDto.ScoringCriteria;
            assignment.CreatedAt = DateTime.UtcNow; 

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully updated essay assignment with ID: {Id}", updateDto.Id);
            return NoContent();
        }
    }
}
