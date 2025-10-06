using Microsoft.AspNetCore.Mvc;
using SoraEssayJudge.Services;
using SoraEssayJudge.Data;
using System;
using System.Threading.Tasks;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DingTalkIntegrationController : ControllerBase
    {
        private readonly IEssayAssignmentSpreadsheetService _essayAssignmentSpreadsheetService;
        private readonly EssayContext _context;

        public DingTalkIntegrationController(
            IEssayAssignmentSpreadsheetService essayAssignmentSpreadsheetService,
            EssayContext context)
        {
            _essayAssignmentSpreadsheetService = essayAssignmentSpreadsheetService;
            _context = context;
        }

        /// <summary>
        /// Creates or ensures a spreadsheet exists for a specific essay assignment
        /// </summary>
        /// <param name="assignmentId">The ID of the essay assignment</param>
        /// <returns>The ID of the spreadsheet</returns>
        [HttpPost("ensure-assignment-spreadsheet/{assignmentId}")]
        public async Task<ActionResult<string>> EnsureAssignmentSpreadsheet(Guid assignmentId)
        {
            var spreadsheetId = await _essayAssignmentSpreadsheetService.EnsureAssignmentSpreadsheetAsync(assignmentId);
            
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                return BadRequest($"Failed to create or find spreadsheet for assignment {assignmentId}");
            }
            
            return Ok(new { spreadsheetId = spreadsheetId });
        }

        /// <summary>
        /// Processes a submission for DingTalk integration (adds to spreadsheet and sends message)
        /// </summary>
        /// <param name="submissionId">The ID of the essay submission</param>
        /// <returns>Status of the operation</returns>
        [HttpPost("process-submission/{submissionId}")]
        public async Task<IActionResult> ProcessSubmission(Guid submissionId)
        {
            var success = await _essayAssignmentSpreadsheetService.ProcessEssaySubmissionForSpreadsheetAsync(submissionId);
            
            if (!success)
            {
                return BadRequest($"Failed to process submission {submissionId} for DingTalk integration");
            }
            
            return Ok(new { message = "Submission processed successfully for DingTalk integration" });
        }
    }
}