using System.Threading.Tasks;
using SoraEssayJudge.Data;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace SoraEssayJudge.Services
{
    public class JudgeService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JudgeService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public JudgeService(IServiceProvider serviceProvider, ILogger<JudgeService> logger, IWebHostEnvironment webHostEnvironment)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task JudgeEssayAsync(Guid submissionId)
        {
            _logger.LogInformation("Starting essay judging process for submission ID: {SubmissionId}", submissionId);
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EssayContext>();
                var processImageService = scope.ServiceProvider.GetRequiredService<ProcessImageService>();
                var openAIService = scope.ServiceProvider.GetRequiredService<OpenAIService>();

                var submission = await context.EssaySubmissions
                                              .Include(s => s.EssayAssignment)
                                              .FirstOrDefaultAsync(s => s.Id == submissionId);
                
                if (submission == null)
                {
                    _logger.LogError("Submission with ID: {SubmissionId} not found in the database.", submissionId);
                    return;
                }

                try
                {
                    var errors = new List<string>();
                    var assignment = submission.EssayAssignment;

                    if (assignment == null)
                    {
                        _logger.LogError("EssayAssignment is null for submission ID: {SubmissionId}. Cannot proceed with judging.", submissionId);
                        submission.IsError = true;
                        submission.ErrorMessage = "Associated EssayAssignment not found.";
                        await context.SaveChangesAsync();
                        return;
                    }

                    // Check for student, but don't stop the process
                    if (submission.StudentId == null)
                    {
                        _logger.LogWarning("Student not identified for submission ID: {SubmissionId}", submissionId);
                        errors.Add("Student not identified for this submission.");
                    }

                    // TODO: Implement student recognition from image

                    // 由 ImageUrl 计算物理路径
                    string? imagePath = null;
                    if (!string.IsNullOrEmpty(submission.ImageUrl))
                    {
                        // 假设 ImageUrl 形如 /EssayFile/xxx.jpg
                        var fileName = submission.ImageUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var uploadsDir = System.IO.Path.Combine(_webHostEnvironment.ContentRootPath, "essayfiles");
                            imagePath = System.IO.Path.Combine(uploadsDir, fileName);
                        }
                    }
                    
                    if (imagePath == null)
                    {
                        _logger.LogError("Image path could not be determined for submission ID: {SubmissionId}. ImageUrl: {ImageUrl}", submissionId, submission.ImageUrl);
                        submission.IsError = true;
                        submission.ErrorMessage = "Image path could not be determined.";
                        await context.SaveChangesAsync();
                        return;
                    }
                    
                    _logger.LogInformation("Processing image for submission ID: {SubmissionId}, path: {ImagePath}", submissionId, imagePath);
                    string parsedText = await processImageService.ProcessImageAsync(imagePath!, submission.ColumnCount);
                    submission.ParsedText = parsedText;
                    // 自动提取第一行为 Title
                    if (!string.IsNullOrWhiteSpace(parsedText))
                    {
                        var lines = parsedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0)
                        {
                            submission.Title = lines[0];
                        }
                    }
                    
                    _logger.LogInformation("Image processed successfully for submission ID: {SubmissionId}. Parsed text length: {ParsedTextLength}", submissionId, parsedText.Length);

                    // Save the OCR result to the database immediately so it can be viewed while judging is in progress.
                    await context.SaveChangesAsync();

                    var judgePromptBuilder = new System.Text.StringBuilder();
                    judgePromptBuilder.Append("system: 你是一个作文评分系统。");
                    judgePromptBuilder.Append($"请根据以下作文内容进行评分，满分{{ {assignment.TotalScore} }}分。");
                    judgePromptBuilder.Append($"该作文等级为{assignment.Grade}，题目背景为“{assignment.TitleContext ?? "暂不知"}”。");
                    if (!string.IsNullOrWhiteSpace(assignment.ScoringCriteria))
                    {
                        judgePromptBuilder.Append(assignment.ScoringCriteria);
                    }
                    judgePromptBuilder.Append("其中存在错别字，你可以适当扣分（有可能存在OCR识别问题，建议扣分不超过2分）。");
                    judgePromptBuilder.Append("请辩证地评判。优点和缺点适当指出。如有题目，请注意是否偏题。");
                    judgePromptBuilder.Append($"基准分{assignment.BaseScore}分，在此基础上加分和扣分。");
                    judgePromptBuilder.Append("请给出评分和简单一句话的评语（评分原因，不暴露基准分）。");
                    judgePromptBuilder.Append("**最重要提醒** 请使用$$包裹分数输出，用##包裹评语，不可其他内容。请注意务必完整使用对应标记符包裹输出。");
                    judgePromptBuilder.Append("返回示例：$$50$$ ##示例评语## \n");
                    string judgePrompt = judgePromptBuilder.ToString();

                    var modelsToUse = new[] { "deepseek-v3" , "deepseek-r1" , "qwen-plus","qwq-plus" };
                    var results = new List<SoraEssayJudge.Models.AIResult>();
                    var scores = new List<int>();

                    foreach (var model in modelsToUse)
                    {
                        _logger.LogInformation("Getting judgment from model {ModelName} for submission ID: {SubmissionId}", model, submissionId);
                        string judgeResult = await openAIService.GetChatCompletionAsync(judgePrompt + parsedText, model);

                        var scoreMatch = Regex.Match(judgeResult, @"\$\$(.*?)\$\$");
                        var feedbackMatch = Regex.Match(judgeResult, @"##(.*?)##");

                        if (!feedbackMatch.Success)
                        {
                            feedbackMatch = Regex.Match(judgeResult, @"#(.*?)");
                        }

                        int? score = null;
                        string feedback;

                        if (scoreMatch.Success && feedbackMatch.Success && int.TryParse(scoreMatch.Groups[1].Value, out int parsedScore))
                        {
                            score = parsedScore;
                            scores.Add(parsedScore);
                            feedback = feedbackMatch.Groups[1].Value.Trim();
                            _logger.LogInformation("Model {ModelName} provided score: {Score} for submission ID: {SubmissionId}", model, score, submissionId);
                        }
                        else
                        {
                            feedback = "未提供有效输出。（不予评价）" + judgeResult;
                            _logger.LogWarning("Model {ModelName} failed to provide a valid score or feedback for submission ID: {SubmissionId}. Raw output: {RawOutput}", model, submissionId, judgeResult);
                        }

                        results.Add(new SoraEssayJudge.Models.AIResult
                        {
                            ModelName = model,
                            Feedback = feedback,
                            Score = score
                        });

                        // Update and save after each model's result
                        submission.AIResults = results.ToList(); // Create a new list to ensure EF Core tracks changes
                        await context.SaveChangesAsync();
                        _logger.LogInformation("Saved intermediate results for submission ID: {SubmissionId} after processing model {ModelName}", submissionId, model);
                    }

                    if (scores.Count > 1)
                    {
                        double average = scores.Average();
                        double variance = scores.Sum(s => Math.Pow(s - average, 2)) / scores.Count;
                        submission.FinalScore = average;
                        _logger.LogInformation("Calculated average score {AverageScore} with variance {Variance} for submission ID: {SubmissionId}", average, variance, submissionId);

                        if (variance > 5)
                        {
                            var errorMessage = $"Score variance ({variance:F2}) exceeds threshold of 5. Manual review required.";
                            _logger.LogWarning("High score variance for submission ID: {SubmissionId}. {ErrorMessage}", submissionId, errorMessage);
                            errors.Add(errorMessage);
                        }
                    }
                    else if (scores.Count == 1)
                    {
                        submission.FinalScore = scores.First();
                        _logger.LogInformation("Only one score available. Final score is {FinalScore} for submission ID: {SubmissionId}", submission.FinalScore, submissionId);
                    }
                    else
                    {
                        var errorMessage = "No valid scores were returned from the AI models.";
                        _logger.LogError("No valid scores for submission ID: {SubmissionId}. {ErrorMessage}", submissionId, errorMessage);
                        errors.Add(errorMessage);
                    }

                    submission.AIResults = results;

                    if (errors.Any())
                    {
                        submission.IsError = true;
                        submission.ErrorMessage = string.Join(" | ", errors);
                        _logger.LogWarning("Errors occurred during judging for submission ID: {SubmissionId}. Errors: {Errors}", submissionId, submission.ErrorMessage);
                    }
                    else
                    {
                        submission.IsError = false;
                        submission.ErrorMessage = null;
                    }

                    // Always generate the final report, even if there are errors.
                    // The report will include any available data and reflect the errors.
                    _logger.LogInformation("Generating final report for submission ID: {SubmissionId}", submissionId);
                    var reportPrompt = BuildReportPrompt(submission, parsedText);
                    submission.JudgeResult = await openAIService.GetChatCompletionAsync(reportPrompt, "deepseek-r1");
                    _logger.LogInformation("Final report generated successfully for submission ID: {SubmissionId}", submissionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unhandled exception occurred during judging for submission ID: {SubmissionId}", submissionId);
                    submission.IsError = true;
                    submission.ErrorMessage = ex.ToString(); // Log the full exception
                }
                finally
                {
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Finished essay judging process for submission ID: {SubmissionId}", submissionId);
                }
            }
        }

        private string BuildReportPrompt(SoraEssayJudge.Models.EssaySubmission submission, string parsedText)
        {
            var assignment = submission.EssayAssignment;
            var reportBuilder = new System.Text.StringBuilder();
            reportBuilder.AppendLine("system: 你是一个资深的语文老师，请根据以下信息生成一份详细的作文分析报告。");
            reportBuilder.AppendLine($"作文题目：{assignment!.TitleContext ?? "无"}");
            reportBuilder.AppendLine($"年级：{assignment.Grade}");
            reportBuilder.AppendLine($"满分：{assignment.TotalScore}");
            reportBuilder.AppendLine($"最终得分：{submission.FinalScore:F2}");
            reportBuilder.AppendLine("\n--- 作文原文 ---");
            reportBuilder.AppendLine(parsedText);
            reportBuilder.AppendLine("\n--- AI模型评分详情 ---");

            foreach (var result in submission.AIResults)
            {
                reportBuilder.AppendLine($"- 模型: {result.ModelName}, 分数: {result.Score?.ToString() ?? "N/A"}, 评语: {result.Feedback}");
            }

            reportBuilder.AppendLine("\n--- 分析报告要求 ---");
            reportBuilder.AppendLine("1. 综合所有模型的评分和评语，对作文的优点和缺点进行全面、客观的分析。");
            reportBuilder.AppendLine("2. 从立意、结构、语言、思想等多个维度进行评价。");
            reportBuilder.AppendLine("3. 提出具体的、有针对性的修改建议。");
            reportBuilder.AppendLine("4. 报告应语言流畅、专业、富有启发性。");
            reportBuilder.AppendLine("5. 最重要：请勿输出其他的提示内容，仅包含一份完整报告。");
            return reportBuilder.ToString();
        }
    }
}
