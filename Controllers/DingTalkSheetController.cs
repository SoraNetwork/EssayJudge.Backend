using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Services;
using SoraEssayJudge.Models;
using SoraEssayJudge.Data;
using System;
using System.Threading.Tasks;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DingTalkSheetController : ControllerBase
    {
        private readonly IDingTalkSheetService _dingTalkSheetService;
        private readonly EssayContext _context;

        public DingTalkSheetController(IDingTalkSheetService dingTalkSheetService, EssayContext context)
        {
            _dingTalkSheetService = dingTalkSheetService;
            _context = context;
        }

        /// <summary>
        /// Creates a new AI sheet (spreadsheet) in DingTalk for an essay assignment
        /// </summary>
        /// <param name="request">The request containing the sheet title</param>
        /// <returns>The ID of the created spreadsheet</returns>
        [HttpPost("create-spreadsheet")]
        public async Task<ActionResult<string>> CreateSpreadsheet([FromBody] CreateSpreadsheetRequest request)
        {
            var spreadsheetId = await _dingTalkSheetService.CreateSpreadsheetAsync(request.Title);
            
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                return BadRequest("Failed to create spreadsheet");
            }
            
            return Ok(new { spreadsheetId = spreadsheetId });
        }

        /// <summary>
        /// Adds essay submission data to an existing spreadsheet
        /// </summary>
        /// <param name="request">The request containing spreadsheet ID and submission ID</param>
        /// <returns>Result of the operation</returns>
        [HttpPost("add-submission")]
        public async Task<IActionResult> AddSubmission([FromBody] AddSubmissionRequest request)
        {
            var submission = await _context.EssaySubmissions.FindAsync(request.SubmissionId);
            if (submission == null)
            {
                return NotFound("Submission not found");
            }

            var success = await _dingTalkSheetService.AddSubmissionToSpreadsheetAsync(request.SpreadsheetId, submission);
            
            if (!success)
            {
                return BadRequest("Failed to add submission to spreadsheet");
            }
            
            return Ok(new { message = "Submission added successfully" });
        }

        /// <summary>
        /// Pushes an essay submission notification message to DingTalk
        /// </summary>
        /// <param name="request">The request containing submission ID</param>
        /// <returns>Result of the operation</returns>
        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            var submission = await _context.EssaySubmissions.FindAsync(request.SubmissionId);
            if (submission == null)
            {
                return NotFound("Submission not found");
            }

            await _dingTalkSheetService.PushEssaySubmissionMessageAsync(submission);
            
            return Ok(new { message = "Message sent successfully" });
        }
    }

    public class CreateSpreadsheetRequest
    {
        public string Title { get; set; }
    }

    public class AddSubmissionRequest
    {
        public string SpreadsheetId { get; set; }
        public Guid SubmissionId { get; set; }
    }

    public class SendMessageRequest
    {
        public Guid SubmissionId { get; set; }
    }
}