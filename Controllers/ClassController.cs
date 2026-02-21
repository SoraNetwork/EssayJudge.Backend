using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using SoraEssayJudge.Dtos;
using Microsoft.Extensions.Configuration;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ClassController : ControllerBase
    {
        private readonly EssayContext _context;
        private readonly IConfiguration _config;

        public ClassController(EssayContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private bool CheckPermission()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            return UserAccessController.CheckAccess(token, 1, _context, _config);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetClasses()
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
        [HttpGet("{classId}")]
        public async Task<ActionResult<Class>> GetClassById(Guid classId)
        {
            var classEntity = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (classEntity == null)
            {
                return NotFound();
            }

            return Ok(classEntity);
        }
        [HttpGet("{classId}/students")]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByClass(Guid classId)
        {
            var students = await _context.Students.Where(s => s.ClassId == classId).ToListAsync();
            return Ok(students);
        }

        [HttpPost]
        public async Task<IActionResult> CreateClass([FromForm] string name)
        {
            if (!CheckPermission())
            {
                return StatusCode(403);
            }

            var newClass = new Class
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow
            };
            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();
            return Ok(newClass);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClass(Guid id)
        {
            if (!CheckPermission())
            {
                return StatusCode(403);
            }

            var classToDelete = await _context.Classes.FindAsync(id);
            if (classToDelete == null)
            {
                return NotFound();
            }

            _context.Classes.Remove(classToDelete);
            await _context.SaveChangesAsync();
            return NoContent();
        }

    }
}
