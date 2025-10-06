using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;

namespace SoraEssayJudge.Services
{
    public interface IDingTalkSheetService
    {
        Task<string> CreateSpreadsheetAsync(string title);
        Task<bool> AddSubmissionToSpreadsheetAsync(string spreadsheetId, EssaySubmission submission);
        Task<bool> AddSubmissionToSpreadsheetWithHeadersAsync(string spreadsheetId, EssaySubmission submission, string status, bool isHeader = false);
        Task<bool> ClearSpreadsheetAsync(string spreadsheetId);
        Task PushEssaySubmissionMessageAsync(EssaySubmission submission);
    }

    public class DingTalkSheetService : IDingTalkSheetService
    {
        private readonly HttpClient _httpClient;
        private readonly DingTalkConfiguration _dingTalkConfig;
        private readonly IMemoryCache _cache;
        private readonly EssayContext _context;
        private readonly ILogger<DingTalkSheetService> _logger;
        private const string AppAccessTokenCacheKey = "DingTalkAppAccessToken";

        public DingTalkSheetService(
            HttpClient httpClient, 
            IOptions<DingTalkConfiguration> dingTalkConfigOptions, 
            IMemoryCache cache, 
            EssayContext context,
            ILogger<DingTalkSheetService> logger)
        {
            _httpClient = httpClient;
            _dingTalkConfig = dingTalkConfigOptions.Value;
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        // Get the application access token for API calls
        public async Task<string> GetAccessTokenAsync()
        {
            if (_cache.TryGetValue(AppAccessTokenCacheKey, out string accessToken))
            {
                return accessToken;
            }

            var requestBody = new
            {
                appKey = _dingTalkConfig.AppKey,
                appSecret = _dingTalkConfig.AppSecret
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.dingtalk.com/v1.0/oauth2/accessToken", requestBody);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("无法从钉钉获取应用 Access Token。");
            }
            
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(tokenResponse.ExpireIn - 120));

            _cache.Set(AppAccessTokenCacheKey, tokenResponse.AccessToken, cacheEntryOptions);

            return tokenResponse.AccessToken;
        }

        // Create a new spreadsheet for an essay assignment
        public async Task<string> CreateSpreadsheetAsync(string title)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                
                var requestBody = new
                {
                    name = title,
                    fileType = "sheet",
                    spaceId = "1" // Default space ID, can be configured
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dingtalk.com/v1.0/drive/files");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create spreadsheet: {Error}", errorContent);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<CreateSpreadsheetResponse>(responseContent);
                
                return result?.FileId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating spreadsheet with title: {Title}", title);
                return null;
            }
        }

        // Add submission data to the spreadsheet
        public async Task<bool> AddSubmissionToSpreadsheetAsync(string spreadsheetId, EssaySubmission submission)
        {
            try
            {
                // Get the assignment data to include in the spreadsheet
                var assignment = await _context.EssayAssignments.FindAsync(submission.EssayAssignmentId);
                if (assignment == null)
                {
                    _logger.LogWarning("Assignment not found for submission ID: {SubmissionId}", submission.Id);
                    return false;
                }

                var student = await _context.Students
                                          .Include(s => s.Class)
                                          .FirstOrDefaultAsync(s => s.Id == submission.StudentId);
                string studentName = student?.Name ?? "未知学生";
                string studentId = student?.StudentId ?? "未知学号";
                string className = student?.Class?.Name ?? "未知班级";

                // Preparing header row if it doesn't exist yet
                var headerRow = new List<object> { 
                    "班级", "学生姓名", "学号", "作文标题", "系统评分", "人工评分", "AI评语1", "AI评语2", "AI评语3", "状态", "提交时间" 
                };
                
                // Preparing data row
                var dataRow = new List<object> { 
                    className,
                    studentName, 
                    studentId, 
                    submission.Title ?? "未识别标题", 
                    submission.Score?.ToString() ?? "未评分", 
                    submission.FinalScore?.ToString() ?? "未评分",
                    GetAIResultAtIndex(submission, 0), // AI评语1
                    GetAIResultAtIndex(submission, 1), // AI评语2
                    GetAIResultAtIndex(submission, 2), // AI评语3
                    submission.IsError ? "错误" : "已完成", 
                    submission.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var accessToken = await GetAccessTokenAsync();
                
                // First, let's try to append the header row and data row to the sheet
                var values = new List<List<object>> { headerRow, dataRow };

                var requestBody = new
                {
                    file_id = spreadsheetId,
                    sheet_id = "0", // Default sheet id
                    rows = values.Select(row => new {
                        cells = row.Select(cell => new { value = cell.ToString() }).ToArray()
                    }).ToArray()
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://oapi.dingtalk.com/topapi/sheet/rows/append");
                request.Headers.Add("x-acs-dingtalk-access-token", accessToken);
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to add submission to spreadsheet {SpreadsheetId}: {Error}", spreadsheetId, errorContent);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding submission {SubmissionId} to spreadsheet {SpreadsheetId}", submission.Id, spreadsheetId);
                return false;
            }
        }

        // Add submission data to the spreadsheet with proper headers and status
        public async Task<bool> AddSubmissionToSpreadsheetWithHeadersAsync(string spreadsheetId, EssaySubmission submission, string status, bool isHeader = false)
        {
            try
            {
                // Get the assignment data to include in the spreadsheet
                var assignment = await _context.EssayAssignments.FindAsync(submission.EssayAssignmentId);
                if (assignment == null && !isHeader)
                {
                    _logger.LogWarning("Assignment not found for submission ID: {SubmissionId}", submission.Id);
                    return false;
                }

                string className = "";
                string studentName = "";
                string studentId = "";

                if (submission.Student != null)
                {
                    className = submission.Student.Class?.Name ?? "未知班级";
                    studentName = submission.Student.Name;
                    studentId = submission.Student.StudentId;
                }
                else if (submission.StudentId.HasValue)
                {
                    var student = await _context.Students
                                              .Include(s => s.Class)
                                              .FirstOrDefaultAsync(s => s.Id == submission.StudentId);
                    if (student != null)
                    {
                        className = student.Class?.Name ?? "未知班级";
                        studentName = student.Name;
                        studentId = student.StudentId;
                    }
                }

                // Preparing data row
                var dataRow = new List<object> { 
                    className,
                    studentName, 
                    studentId, 
                    submission.Title ?? (assignment?.TitleContext ?? "未识别标题"), 
                    submission.Score?.ToString() ?? "未评分", 
                    submission.FinalScore?.ToString() ?? "未评分",
                    GetAIResultAtIndex(submission, 0), // AI评语1
                    GetAIResultAtIndex(submission, 1), // AI评语2
                    GetAIResultAtIndex(submission, 2), // AI评语3
                    status, // Status
                    submission.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var accessToken = await GetAccessTokenAsync();
                
                var requestBody = new
                {
                    file_id = spreadsheetId,
                    sheet_id = "0", // Default sheet id
                    rows = new[] {
                        new {
                            cells = dataRow.Select(cell => new { value = cell?.ToString() ?? "" }).ToArray()
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://oapi.dingtalk.com/topapi/sheet/rows/append");
                request.Headers.Add("x-acs-dingtalk-access-token", accessToken);
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to add submission to spreadsheet {SpreadsheetId}: {Error}", spreadsheetId, errorContent);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding submission {SubmissionId} to spreadsheet {SpreadsheetId}", submission.Id, spreadsheetId);
                return false;
            }
        }

        // Helper method to get AI result at specific index
        private string GetAIResultAtIndex(EssaySubmission submission, int index)
        {
            if (submission.AIResults == null || submission.AIResults.Count <= index)
            {
                return "";
            }

            var result = submission.AIResults.ElementAt(index);
            return result.Feedback ?? $"评分:{result.Score}";
        }

        // Method to clear spreadsheet content (for re-syncing)
        public async Task<bool> ClearSpreadsheetAsync(string spreadsheetId)
        {
            // For DingTalk sheets, there might not be a direct "clear" API
            // Instead, we'll add a method that could potentially clear content by adding a new row
            // Or we could implement this differently based on actual API availability
            // For now, we'll just log that this is a placeholder
            _logger.LogInformation("Clearing spreadsheet operation requested for {SpreadsheetId}", spreadsheetId);
            // In a real implementation, this would use the appropriate DingTalk API to clear sheet content
            return true;
        }

        // Push essay submission message to DingTalk
        public async Task PushEssaySubmissionMessageAsync(EssaySubmission submission)
        {
            try
            {
                // Get the assignment data to include in the message
                var assignment = await _context.EssayAssignments.FindAsync(submission.EssayAssignmentId);
                if (assignment == null)
                {
                    _logger.LogWarning("Assignment not found for submission ID: {SubmissionId}", submission.Id);
                    return;
                }

                var student = await _context.Students.FindAsync(submission.StudentId);
                string studentName = student?.Name ?? "未知学生";

                // Construct message content
                string messageText = $"作文提交通知：\n" +
                                    $"学生姓名：{studentName}\n" +
                                    $"作文题目：{assignment.TitleContext}\n" +
                                    $"提交时间：{submission.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                                    $"得分：{(submission.FinalScore.HasValue ? submission.FinalScore.Value.ToString("F2") : "暂未评分")}\n" +
                                    $"批改状态：{(submission.IsError ? "错误" : "已完成")}\n";

                var accessToken = await GetAccessTokenAsync();

                // Send the message using DingTalk's message API
                await SendTextMessageToDingTalk(accessToken, messageText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing essay submission message for submission ID: {SubmissionId}", submission.Id);
            }
        }

        private async Task<bool> SendTextMessageToDingTalk(string accessToken, string messageText)
        {
            try
            {
                // This is a placeholder for the actual message sending implementation
                // For this example, we'll use a simplified approach
                // In a real implementation, you'd send to a specific user/chat/group
                
                // First, get user ID by union ID (configured in appsettings)
                var userId = await GetUserIdByUnionId(accessToken, _dingTalkConfig.OperatorUnionId);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Could not get user ID for union ID: {UnionId}", _dingTalkConfig.OperatorUnionId);
                    return false;
                }

                // Send message to the user
                var requestBody = new
                {
                    userId = userId,
                    msg = new
                    {
                        msgtype = "text",
                        text = new
                        {
                            content = messageText
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"https://oapi.dingtalk.com/topapi/message/corpconversation/asyncsend_v2?access_token={accessToken}");
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send message to DingTalk: {Error}", errorContent);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to DingTalk");
                return false;
            }
        }

        private async Task<string> GetUserIdByUnionId(string accessToken, string unionId)
        {
            try
            {
                // Get user ID by union ID
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://oapi.dingtalk.com/topapi/user/getbyunionid?access_token={accessToken}&unionid={unionId}");
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get user ID by union ID: {Error}", errorContent);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<GetUserIdByUnionIdResponse>(responseContent);
                
                return result?.Result?.UserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID by union ID: {UnionId}", unionId);
                return null;
            }
        }
    }

    public class CreateSpreadsheetResponse
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string FileId { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string RequestId { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int Code { get; set; }
    }
    
    public class GetUserIdByUnionIdResponse
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int ErrCode { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string ErrMsg { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public GetUserIdByUnionIdResult Result { get; set; }
    }
    
    public class GetUserIdByUnionIdResult
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string UserId { get; set; }
    }
}