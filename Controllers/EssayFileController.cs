using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EssayFileController : ControllerBase
    {
        private readonly string _basePath = Path.Combine(Directory.GetCurrentDirectory(), "essayfiles");

        [AllowAnonymous]
        [HttpGet("{fileName}")]
        public IActionResult GetEssayImage(string fileName)
        {
            var filePath = Path.Combine(_basePath, fileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();
            var contentType = "image/jpeg";
            return PhysicalFile(filePath, contentType);
        }
    }
}
