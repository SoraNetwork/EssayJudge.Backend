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

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ClassController : ControllerBase
    {
        private readonly EssayContext _context;

        public ClassController(EssayContext context)
        {
            _context = context;
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

        [HttpGet("{classId}/students")]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByClass(Guid classId)
        {
            var students = await _context.Students.Where(s => s.ClassId == classId).ToListAsync();
            return Ok(students);
        }

        [HttpPost]
        public async Task<IActionResult> CreateClass([FromForm] string name)
        {
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
    }
}
