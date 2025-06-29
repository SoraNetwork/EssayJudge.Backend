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
    public class StudentController : ControllerBase
    {
        private readonly EssayContext _context;
        private readonly ILogger<StudentController> _logger;

        public StudentController(EssayContext context, ILogger<StudentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StudentDto>>> GetStudents([FromQuery] Guid? id, [FromQuery] string? name, [FromQuery] Guid? classId)
        {
            _logger.LogInformation("Getting students with ID: {StudentId}, Name: {StudentName}, ClassId: {ClassId}", id, name, classId);
            var query = _context.Students.AsQueryable();

            if (id.HasValue)
            {
                query = query.Where(s => s.Id == id.Value);
            }

            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(s => s.Name.Contains(name));
            }

            if (classId.HasValue)
            {
                query = query.Where(s => s.ClassId == classId.Value);
            }

            var students = await query.Select(s => new StudentDto
            {
                Id = s.Id,
                StudentId = s.StudentId,
                Name = s.Name,
                ClassId = s.ClassId,
                CreatedAt = s.CreatedAt
            }).ToListAsync();

            _logger.LogInformation("Found {StudentCount} students.", students.Count);
            return Ok(students);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] string name, [FromForm] string studentId, [FromForm] Guid classId)
        {
            _logger.LogInformation("Creating a new student with name: {StudentName}, studentId: {StudentId}, classId: {ClassId}", name, studentId, classId);
            var student = new Student
            {
                Id = Guid.NewGuid(),
                Name = name,
                StudentId = studentId,
                ClassId = classId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created student with ID: {StudentId}", student.Id);
            return Ok(new { studentId = student.Id });
        }
    }
}
