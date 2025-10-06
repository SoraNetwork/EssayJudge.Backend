using Microsoft.Extensions.Logging;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using SoraEssayJudge.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SoraEssayJudge.Services
{
    public interface IEssayAssignmentSpreadsheetService
    {
        Task<string> EnsureAssignmentSpreadsheetAsync(Guid assignmentId);
        Task<bool> ProcessEssaySubmissionForSpreadsheetAsync(Guid submissionId);
        Task<bool> SyncAssignmentSpreadsheetAsync(Guid assignmentId);
    }

    public class EssayAssignmentSpreadsheetService : IEssayAssignmentSpreadsheetService
    {
        private readonly EssayContext _context;
        private readonly IDingTalkSheetService _dingTalkSheetService;
        private readonly ILogger<EssayAssignmentSpreadsheetService> _logger;
        private static readonly ConcurrentDictionary<Guid, string> _assignmentSpreadsheetCache = new ConcurrentDictionary<Guid, string>();
        private static readonly ConcurrentDictionary<Guid, DateTime> _lastSyncCache = new ConcurrentDictionary<Guid, DateTime>();

        public EssayAssignmentSpreadsheetService(
            EssayContext context, 
            IDingTalkSheetService dingTalkSheetService,
            ILogger<EssayAssignmentSpreadsheetService> logger)
        {
            _context = context;
            _dingTalkSheetService = dingTalkSheetService;
            _logger = logger;
        }

        public async Task<string> EnsureAssignmentSpreadsheetAsync(Guid assignmentId)
        {
            // Check if we already have the spreadsheet ID in cache
            if (_assignmentSpreadsheetCache.TryGetValue(assignmentId, out string cachedSpreadsheetId))
            {
                return cachedSpreadsheetId;
            }

            // Get the assignment details
            var assignment = await _context.EssayAssignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                _logger.LogWarning("Assignment with ID {AssignmentId} not found", assignmentId);
                return null;
            }

            // Create a new spreadsheet for this assignment
            var spreadsheetTitle = $"作文作业_{assignment.TitleContext ?? "Untitled"}_{assignment.CreatedAt:yyyyMMdd}";
            var spreadsheetId = await _dingTalkSheetService.CreateSpreadsheetAsync(spreadsheetTitle);

            if (!string.IsNullOrEmpty(spreadsheetId))
            {
                // Cache the spreadsheet ID
                _assignmentSpreadsheetCache.TryAdd(assignmentId, spreadsheetId);
                _logger.LogInformation("Created spreadsheet {SpreadsheetId} for assignment {AssignmentId}", spreadsheetId, assignmentId);
                
                // Add the header row to the spreadsheet
                await AddHeaderRowToSpreadsheet(spreadsheetId);
            }

            return spreadsheetId;
        }

        private async Task<bool> AddHeaderRowToSpreadsheet(string spreadsheetId)
        {
            var headerRow = new List<object> { 
                "班级", "学生姓名", "学号", "作文标题", "系统评分", "人工评分", "AI评语1", "AI评语2", "AI评语3", "状态", "提交时间"
            };

            var submission = new EssaySubmission
            {
                EssayAssignmentId = Guid.Empty, // Placeholder for header
                Title = "",
                Score = 0,
                FinalScore = 0,
                IsError = false,
                CreatedAt = DateTime.UtcNow
            };

            // Add header row to the spreadsheet
            bool addedToSheet = await _dingTalkSheetService.AddSubmissionToSpreadsheetWithHeadersAsync(spreadsheetId, submission, "标题行", isHeader: true);
            return addedToSheet;
        }

        public async Task<bool> ProcessEssaySubmissionForSpreadsheetAsync(Guid submissionId)
        {
            var submission = await _context.EssaySubmissions
                                          .Include(s => s.Student)
                                          .ThenInclude(s => s.Class)
                                          .Include(s => s.EssayAssignment)
                                          .Include(s => s.AIResults)
                                          .FirstOrDefaultAsync(s => s.Id == submissionId);
            
            if (submission == null)
            {
                _logger.LogWarning("Submission with ID {SubmissionId} not found", submissionId);
                return false;
            }

            // Get the assignment for this submission
            var assignment = await _context.EssayAssignments.FindAsync(submission.EssayAssignmentId);
            if (assignment == null)
            {
                _logger.LogWarning("Assignment with ID {AssignmentId} not found for submission {SubmissionId}", 
                    submission.EssayAssignmentId, submissionId);
                return false;
            }

            // Ensure a spreadsheet exists for this assignment
            string spreadsheetId = await EnsureAssignmentSpreadsheetAsync(submission.EssayAssignmentId);
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                _logger.LogWarning("Could not ensure spreadsheet for assignment {AssignmentId}", submission.EssayAssignmentId);
                return false;
            }

            // Add the submission data to the spreadsheet
            bool addedToSheet = await _dingTalkSheetService.AddSubmissionToSpreadsheetWithHeadersAsync(spreadsheetId, submission, "已提交");
            if (addedToSheet)
            {
                _logger.LogInformation("Added submission {SubmissionId} to spreadsheet {SpreadsheetId}", 
                    submissionId, spreadsheetId);
            }
            else
            {
                _logger.LogWarning("Failed to add submission {SubmissionId} to spreadsheet {SpreadsheetId}", 
                    submissionId, spreadsheetId);
            }

            // Send a message about the submission to DingTalk
            await _dingTalkSheetService.PushEssaySubmissionMessageAsync(submission);
            _logger.LogInformation("Sent DingTalk message for submission {SubmissionId}", submissionId);

            // Sync the entire spreadsheet to include missing students
            await SyncAssignmentSpreadsheetAsync(submission.EssayAssignmentId);

            return addedToSheet;
        }

        public async Task<bool> SyncAssignmentSpreadsheetAsync(Guid assignmentId)
        {
            string spreadsheetId = await EnsureAssignmentSpreadsheetAsync(assignmentId);
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                _logger.LogWarning("Could not ensure spreadsheet for assignment {AssignmentId} during sync", assignmentId);
                return false;
            }

            // Get the assignment
            var assignment = await _context.EssayAssignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                _logger.LogWarning("Assignment with ID {AssignmentId} not found during sync", assignmentId);
                return false;
            }

            // Get all submissions for this assignment
            var submissions = await _context.EssaySubmissions
                                           .Include(s => s.Student)
                                           .ThenInclude(s => s.Class)
                                           .Include(s => s.AIResults)
                                           .Where(s => s.EssayAssignmentId == assignmentId)
                                           .ToListAsync();

            // Get all students who are supposed to submit for this assignment
            // This includes all students from classes that have at least one submission for this assignment
            var studentsInAssignmentClasses = await _context.Students
                .Include(s => s.Class)
                .Where(s => _context.EssaySubmissions
                    .Where(sub => sub.EssayAssignmentId == assignmentId)
                    .Select(sub => sub.StudentId)
                    .Contains(s.Id) // Students who have submitted
                    || 
                    _context.Students.Where(s2 => s2.ClassId == s.ClassId)
                        .Any(s2 => _context.EssaySubmissions
                            .Where(sub => sub.EssayAssignmentId == assignmentId)
                            .Select(sub => sub.StudentId)
                            .Contains(s2.Id))) // Students from the same class as those who submitted
                .Distinct()
                .ToListAsync();

            // Add header row if it doesn't exist (only once per sync)
            // In a real implementation, you might want to check if header exists first
            // For now, we'll add it each time, but in production you'd want to be more sophisticated
            // Add all students (both submitted and non-submitted) to the spreadsheet
            foreach (var student in studentsInAssignmentClasses)
            {
                var studentSubmission = submissions.FirstOrDefault(s => s.StudentId == student.Id);
                
                if (studentSubmission != null)
                {
                    // Student has submitted - add the submission data
                    await _dingTalkSheetService.AddSubmissionToSpreadsheetWithHeadersAsync(spreadsheetId, studentSubmission, "已提交");
                }
                else
                {
                    // Student has not submitted - add placeholder
                    var placeholderSubmission = new EssaySubmission
                    {
                        EssayAssignmentId = assignmentId,
                        StudentId = student.Id,
                        Title = assignment.TitleContext,
                        Score = null,
                        FinalScore = null,
                        IsError = false,
                        ErrorMessage = "未提交",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _dingTalkSheetService.AddSubmissionToSpreadsheetWithHeadersAsync(spreadsheetId, placeholderSubmission, "未提交");
                }
            }

            _logger.LogInformation("Synced spreadsheet {SpreadsheetId} for assignment {AssignmentId}", spreadsheetId, assignmentId);
            return true;
        }

        // Helper method to get the latest activity time for an assignment (last submission, etc.)
        private async Task<DateTime> GetLastAssignmentActivityAsync(Guid assignmentId)
        {
            var latestSubmission = await _context.EssaySubmissions
                .Where(s => s.EssayAssignmentId == assignmentId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.CreatedAt)
                .FirstOrDefaultAsync();
                
            var assignment = await _context.EssayAssignments
                .Where(a => a.Id == assignmentId)
                .Select(a => a.CreatedAt)
                .FirstOrDefaultAsync();
                
            // Return the most recent of assignment creation or latest submission
            return latestSubmission > assignment ? latestSubmission : assignment;
        }
    }
}