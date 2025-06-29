using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class EssaySubmissionSearchController : ControllerBase
    {
        private readonly EssayContext _context;
        public EssaySubmissionSearchController(EssayContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 按标题模糊查询作文，返回作文、学生姓名/ID，支持日期倒序和数量限制
        /// </summary>
        /// <param name="title">标题关键字</param>
        /// <param name="top">返回前N条</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? title, [FromQuery] int top = 10)
        {
            var query = _context.EssaySubmissions
                .Include(e => e.Student)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(e => e.Title != null && e.Title.Contains(title));
            }

            var result = await query
                .OrderByDescending(e => e.CreatedAt)
                .Take(top)
                .Select(e => new {
                    e.Id,
                    e.Title,
                    e.CreatedAt,
                    StudentId = e.StudentId,
                    StudentName = e.Student != null ? e.Student.Name : null,
                    e.FinalScore
                })
                .ToListAsync();
            return Ok(result);
        }
    }
}
