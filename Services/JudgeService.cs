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

        public async Task JudgeEssayAsync(Guid submissionId,bool enableV3 = false)
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
                    string parsedText = submission.ParsedText;
                    if (enableV3 && string.IsNullOrWhiteSpace(parsedText))
                    {
                        _logger.LogInformation("V3 OCR processing enabled for submission ID: {SubmissionId}", submissionId);
                        var ocrProcessingModelSetting = await context.AIModelUsageSettings
                            .Include(s => s.AIModel)
                            .FirstOrDefaultAsync(s => s.UsageType == "OcrV3" && s.IsEnabled);
                        if (ocrProcessingModelSetting == null || ocrProcessingModelSetting.AIModel == null)
                        {
                            _logger.LogError("No enabled 'OcrV3' model configured in AIModelUsageSettings for submission ID: {SubmissionId}.", submissionId);
                            submission.IsError = true;
                            submission.ErrorMessage = "OCRV3 model not configured.";
                            await context.SaveChangesAsync();
                            return;
                        }
                        if (string.IsNullOrEmpty(submission.ImageUrl))
                        {
                            _logger.LogError("ImageUrl is null or empty for submission ID: {SubmissionId}. Cannot proceed with V3 OCR processing.", submissionId);
                            submission.IsError = true;
                            submission.ErrorMessage = "ImageUrl is required for V3 OCR processing.";
                            await context.SaveChangesAsync();
                            return;
                        }
                        string webImageUrl = "https://api.ej.xingsora.cn" + submission.ImageUrl;

                        parsedText = await processImageService.ProcessImageAsyncV3(webImageUrl, submission.Id, ocrProcessingModelSetting.AIModel.ModelId);
                        submission.ParsedText = parsedText;
                    }
                        if (string.IsNullOrEmpty(parsedText))
                    {
                        // Fetch model settings from DB
                        var ocrProcessingModelSetting = await context.AIModelUsageSettings
                            .Include(s => s.AIModel)
                            .FirstOrDefaultAsync(s => s.UsageType == "OcrProcessing" && s.IsEnabled);

                        if (ocrProcessingModelSetting == null || ocrProcessingModelSetting.AIModel == null)
                        {
                            _logger.LogError("No enabled 'OcrProcessing' model configured in AIModelUsageSettings for submission ID: {SubmissionId}.", submissionId);
                            submission.IsError = true;
                            submission.ErrorMessage = "OCR processing model not configured.";
                            await context.SaveChangesAsync();
                            return;
                        }

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
                        parsedText = await processImageService.ProcessImageAsync(imagePath!, submission.ColumnCount, Guid.NewGuid(), ocrProcessingModelSetting.AIModel.ModelId);
                        submission.ParsedText = parsedText;
                    }
                    parsedText = submission.ParsedText;
                    // 自动提取第一行为 Title
                    if (!string.IsNullOrWhiteSpace(parsedText))
                    {
                        var lines = parsedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0)
                        {
                            submission.Title = lines[0];
                        }

                        if (lines[^1].Contains("$$"))
                        {
                            var studentName = lines[^1].Replace("$$", "").Trim();
                            _logger.LogInformation("Extracted potential student name: {StudentName} for submission ID: {SubmissionId}", studentName, submissionId);
                            submission.ParsedText = parsedText.Replace(lines[^1], "").Trim(); // Remove the last line containing the student name

                            parsedText = submission.ParsedText; // Update parsedText after removing the last line

                            // Attempt to find the student by name
                            var student = await context.Students.FirstOrDefaultAsync(s => s.Name == studentName);

                            if (student != null)
                            {
                                submission.StudentId = student.Id;
                                _logger.LogInformation("Found student with ID: {StudentId} for name: {StudentName}", student.Id, studentName);
                            }
                            else
                            {
                                _logger.LogWarning("Student with name: {StudentName} not found in the database for submission ID: {SubmissionId}", studentName, submissionId);
                                errors.Add($"Student '{studentName}' not found in the database.");
                            }
                        }
                    }
                    
                    if (submission.StudentId == null)
                    {
                        _logger.LogWarning("Student not identified for submission ID: {SubmissionId}", submissionId);
                        errors.Add("Student not identified for this submission.");
                    }

                    if (submission.ParsedText.Length <= 300)
                    {
                        _logger.LogWarning("Parsed text is too short (length: {ParsedTextLength}) for submission ID: {SubmissionId}. Minimum length is 300 characters.", parsedText.Length, submissionId);
                        errors.Add("Parsed text is too short. Minimum length is 300 characters.");
                        submission.IsError = true;
                        submission.ErrorMessage = string.Join(" | ", errors);
                        await context.SaveChangesAsync();
                        return;

                    }

                    _logger.LogInformation("Image processed successfully for submission ID: {SubmissionId}. Parsed text length: {ParsedTextLength}", submissionId, parsedText.Length);

                    // Save the OCR result to the database immediately so it can be viewed while judging is in progress.

                    var judgePromptBuilder = new System.Text.StringBuilder();
                    judgePromptBuilder.Append("system:假定你是一个高中语文教师，正在参与高考语文作文的批阅。");
                    judgePromptBuilder.Append($"请根据以下作文内容进行评分，满分{{ {assignment.TotalScore} }}分。");
                    judgePromptBuilder.Append($"该作文年级为{assignment.Grade}，题目背景为“{assignment.TitleContext ?? "暂不知"}”。");
                    judgePromptBuilder.Append($"该作文的字数为{submission.ParsedText.Length}，请根据题目要求注意字数多少。（一般在题目要求的90%以上为正常，少于80%的字数需要适当扣分）");
                    judgePromptBuilder.Append("其中可能存在错别字，你需要适当扣分（有可能存在OCR识别问题，扣分不超过3分）。");
                    judgePromptBuilder.Append("请辩证地评判。优点和缺点适当指出。如有题目，请注意是否偏题。");
                    judgePromptBuilder.Append($"基准分{assignment.BaseScore}分，在此基础上加分和扣分。");
                    judgePromptBuilder.Append("请给出评分和简单一句话的评语（评分原因，不暴露基准分）。");
                    judgePromptBuilder.Append("重要提示：评分应符合正态分布，即大部分学生的作文分数应接近基准分，只有少数优秀或较差的作文会有较高或较低的分数。");
                    judgePromptBuilder.Append("请注意，评分应考虑到作文的整体质量，而不仅仅是某一方面的表现。");
                    judgePromptBuilder.Append("作文批改需要注意以下几点：1.关注作文主旨核心，检查是否离题，检查学生的立意是否深刻清晰；2.关注学生作文中使用的事例是否合适，有无存在使用错误或价值观有问题的事例，使用的事例是否符合历史事实，观察学生引用的事例是否足够新颖；3.检查学生的论证逻辑是否严密，论证方法是否正确；4.观察学生的论证是否触及问题的本质5.作作文的给分需要及其严格，作文某一方面的优秀不能认为作文整体优秀而给出高分，分数应当尽量控制在三类到四类作文的区间");                                                                           
                    judgePromptBuilder.Append("**最重要提醒** 请使用$$包裹分数输出，用##包裹评语，不可其他内容。请注意务必完整使用对应标记符包裹输出。");
                    judgePromptBuilder.Append("返回示例：$$50$$ ##示例评语## \n");
                    judgePromptBuilder.Append("以下为作文的评分标准：");
                    if (!string.IsNullOrWhiteSpace(assignment.ScoringCriteria))
                    {
                        judgePromptBuilder.Append(assignment.ScoringCriteria);
                    }
                    string judgePrompt = judgePromptBuilder.ToString();

                    var modelsToUse = await context.AIModelUsageSettings
                        .Where(s => s.UsageType == "Judging" && s.IsEnabled && s.AIModel != null)
                        .Include(s => s.AIModel)
                        .Select(s => s.AIModel!.ModelId)
                        .ToArrayAsync();

                    if (modelsToUse.Length == 0)
                    {
                        var errorMessage = "No enabled 'Judging' models configured in AIModelUsageSettings.";
                        _logger.LogError(errorMessage + " for submission ID: {SubmissionId}.", submissionId);
                        errors.Add(errorMessage);
                        submission.IsError = true;
                        submission.ErrorMessage = string.Join(" | ", errors);
                        await context.SaveChangesAsync();
                        return;
                    }

                    var results = new List<SoraEssayJudge.Models.AIResult>();
                    var scores = new List<int>();
                    // 使用 SemaphoreSlim 控制并发请求数（可选，避免瞬时高并发）
                    var throttler = new SemaphoreSlim(initialCount: 4); // 调整并发度为4

                    var tasks = modelsToUse.Select(async model =>
                    {
                        await throttler.WaitAsync(); // 控制并发量
                        try
                        {
                            _logger.LogInformation("Getting judgment from model {ModelName} for submission ID: {SubmissionId}", model, submissionId);
                            string judgeResult = await openAIService.GetChatCompletionAsync(judgePrompt + parsedText, model);

                            var scoreMatch = Regex.Match(judgeResult, @"\$\$(.*?)\$\$");
                            var feedbackMatch = Regex.Match(judgeResult, @"##(.*?)##");

                            if (!feedbackMatch.Success)
                            {
                                feedbackMatch = Regex.Match(judgeResult, @"##(.*?)");
                            }

                            int? score = null;
                            string feedback;

                            if (scoreMatch.Success && feedbackMatch.Success && int.TryParse(scoreMatch.Groups[1].Value, out int parsedScore))
                            {
                                score = parsedScore;
                                feedback = feedbackMatch.Groups[1].Value.Trim();
                                _logger.LogInformation("Model {ModelName} provided score: {Score} for submission ID: {SubmissionId}", model, score, submissionId);
                                return (Model: model, Score: score, Feedback: feedback, Valid: true);
                            }
                            else
                            {
                                feedback = "未提供有效输出。（不予评价）" + judgeResult;
                                _logger.LogWarning("Model {ModelName} failed to provide valid output for submission ID: {SubmissionId}. Raw: {RawOutput}", model, submissionId, judgeResult);
                                return (Model: model, Score: (int?)null, Feedback: feedback, Valid: false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing model {ModelName} for submission ID: {SubmissionId}", model, submissionId);
                            return (Model: model, Score: (int?)null, Feedback: $"处理时发生错误: {ex.Message}", Valid: false);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }).ToList();

                    // 并行执行所有请求并等待完成
                    var modelResults = await Task.WhenAll(tasks);

                    // 按原始顺序处理结果并串行保存
                    foreach (var result in modelResults)
                    {
                        if (result.Valid && result.Score.HasValue)
                        {
                            scores.Add(result.Score.Value);
                        }

                        var aiResult = new SoraEssayJudge.Models.AIResult
                        {
                            ModelName = result.Model,
                            Feedback = result.Feedback,
                            Score = result.Score
                        };
                        results.Add(aiResult);

                        // 更新并保存当前结果（保持串行保存避免EF Core并发冲突）
                        submission.AIResults = results.ToList();
                        await context.SaveChangesAsync();

                        _logger.LogInformation("Saved results for model {ModelName} on submission ID: {SubmissionId}",
                            result.Model, submissionId);
                    }

                    if (scores.Count > 1)
                    {
                        double average = Math.Round(scores.Average(), 2);
                        double variance = scores.Sum(s => Math.Pow(s - average, 2)) / scores.Count;
                        submission.FinalScore = average;
                        _logger.LogInformation("Calculated average score {AverageScore} with variance {Variance} for submission ID: {SubmissionId}", average, variance, submissionId);

                        if (variance > 10)
                        {
                            var errorMessage = $"Score variance ({variance:F2}) exceeds threshold of 10. Manual review required.";
                            _logger.LogWarning("High score variance for submission ID: {SubmissionId}. {ErrorMessage}", submissionId, errorMessage);
                            errors.Add(errorMessage);
                            var scorelist = new List<int>(scores);
                            for(int i=0 ; i<scorelist.Count ; i++ )
                            {
                                if (scorelist[i] < average - 6 || scorelist[i] > average + 6)
                                {
                                    _logger.LogWarning("Score {Score} is significantly different from average {AverageScore} for submission ID: {SubmissionId}", scorelist[i], average, submissionId);
                                    errors.Add($"Score {scorelist[i]} deviates significantly from average {average}. Manual review recommended.");
                                    scores.Remove(scorelist[i]);
                                }
                            }
                            average = Math.Round(scores.Average(), 2);
                            submission.FinalScore = average;    
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
                    await context.SaveChangesAsync();

                    // Always generate the final report, even if there are errors.
                    // The report will include any available data and reflect the errors.
                    _logger.LogInformation("Generating final report for submission ID: {SubmissionId}", submissionId);
                    var reportPrompt = BuildReportPrompt(submission, parsedText);

                    var reportingModelSetting = await context.AIModelUsageSettings
                        .Include(s => s.AIModel)
                        .FirstOrDefaultAsync(s => s.UsageType == "Reporting" && s.IsEnabled);

                    if (reportingModelSetting == null || reportingModelSetting.AIModel == null)
                    {
                        var errorMessage = "No enabled 'Reporting' model configured in AIModelUsageSettings.";
                        _logger.LogError(errorMessage + " for submission ID: {SubmissionId}.", submissionId);
                        errors.Add(errorMessage);
                        if (submission.IsError)
                        {
                            submission.ErrorMessage += " | " + errorMessage;
                        }
                        else
                        {
                            submission.IsError = true;
                            submission.ErrorMessage = errorMessage;
                        }
                    }
                    else
                    {
                        submission.JudgeResult = await openAIService.GetChatCompletionAsync(reportPrompt, reportingModelSetting.AIModel.ModelId);
                        _logger.LogInformation("Final report generated successfully for submission ID: {SubmissionId}", submissionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unhandled exception occurred during judging for submission ID: {SubmissionId} , \n Error: {ex.ToString()}", submissionId , ex.ToString());
                    submission.IsError = true;
                    submission.ErrorMessage = ex.ToString(); // Log the full exception
                }
                finally
                {
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Finished essay judging process for submission ID: {SubmissionId}", submissionId);
                }

                return;
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
            reportBuilder.AppendLine($"该作文的字数为：{submission.ParsedText!.Length}");
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

            reportBuilder.AppendLine("\n不需要完全按照模板，需要个性化评测报告 --- 报告格式要求 --- ");
            reportBuilder.AppendLine("# 作文批改分析报告");
            reportBuilder.AppendLine("| 项目 | 内容 |");
            reportBuilder.AppendLine("| :--- | :--- |");
            reportBuilder.AppendLine("| **作文标题** | [在此处填写完整的作文标题] |");
            reportBuilder.AppendLine($"| **预估分数** | **{submission.FinalScore:F0} / {assignment.TotalScore} 分** |"); // Use actual score and total score
            reportBuilder.AppendLine("| **等级** | `[一类卷 / 二类卷上 / 二类卷下 / 三类卷等]` |");
            reportBuilder.AppendLine("### 综合评价");
            reportBuilder.AppendLine("*   **一句话总评：** `[例如：立意精准，论证有力，但语言细节有待打磨。]` 或 `[例如：结构完整，但思想深度不足，论据较为陈旧。]`");
            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine("## 一、多维度分析与诊断");
            reportBuilder.AppendLine("<!-- ");
            reportBuilder.AppendLine("    以下四个维度是高考作文评分的核心。");
            reportBuilder.AppendLine("    - “亮点”用于肯定学生的优点，建立信心。");
            reportBuilder.AppendLine("    - “问题”用于指出不足，明确方向。");
            reportBuilder.AppendLine("    - “具体示例”是关键，务必引用原文，使分析有据可依。");
            reportBuilder.AppendLine("    - “待改进”部分是最重要的，务必给出具体、可操作的修改建议。");
            reportBuilder.AppendLine("    - 注意：若没有优点或问题，可以不输出对应部分。");
            reportBuilder.AppendLine("-->");
            reportBuilder.AppendLine("### 1. 内容与立意 (发展等级 - 20分)");
            reportBuilder.AppendLine("*   **本项得分：** `[得分] / 20 分`");
            reportBuilder.AppendLine("*   **亮点：**");
            reportBuilder.AppendLine("    *   `[例如：审题准确，能够紧扣材料核心概念展开论述。]`");
            reportBuilder.AppendLine("    *   `[例如：立意新颖，能够从“……”的角度切入，体现了较强的思辨能力。]`");
            reportBuilder.AppendLine("*   **问题：**");
            reportBuilder.AppendLine("    *   `[例如：立意略显陈旧，未能跳出常规思维框架。]`");
            reportBuilder.AppendLine("    *   `[例如：思想内容稍显单薄，对问题的探讨停留在表面，缺乏深度。]`");
            reportBuilder.AppendLine("*   **具体示例：**");
            reportBuilder.AppendLine("    *   **优**：你在第三段写道：“……”，这个观点将个人选择与时代背景结合，体现了深刻的洞察力。");
            reportBuilder.AppendLine("    *   **待改进**：文中提到的“只要努力就能成功”这一观点，虽然积极，但略显简化。可以进一步探讨“努力”与“方法”、“机遇”之间的复杂关系，使论证更全面。");
            reportBuilder.AppendLine("### 2. 结构与逻辑 (基础等级 - 20分)");
            reportBuilder.AppendLine("*   **本项得分：** `[得分] / 20 分`");
            reportBuilder.AppendLine("*   **亮点：**");
            reportBuilder.AppendLine("    *   `[例如：文章结构清晰，总分总结构完整，段落划分合理。]`");
            reportBuilder.AppendLine("    *   `[例如：论证逻辑链条清晰，分论点之间层层递进，说服力强。]`");
            reportBuilder.AppendLine("*   **问题：**");
            reportBuilder.AppendLine("    *   `[例如：段落之间缺乏有效的过渡，显得有些跳跃。]`");
            reportBuilder.AppendLine("    *   `[例如：部分论据与分论点之间的关联性不强，存在“论据倒挂”现象。]`");
            reportBuilder.AppendLine("*   **具体示例：**");
            reportBuilder.AppendLine("    *   **优**：文章开头通过……引出中心论点，结尾……进行总结升华，首尾呼应，结构严谨。");
            reportBuilder.AppendLine("    *   **待改进**：第二段和第三段都是在罗列事例，但没有清晰的分论点统领，建议为每个事例段提炼一个明确的分论点句，置于段首。");
            reportBuilder.AppendLine("### 3. 语言与文采 (基础等级 - 20分)");
            reportBuilder.AppendLine("*   **本项得分：** `[得分] / 20 分`");
            reportBuilder.AppendLine("*   **亮点：**");
            reportBuilder.AppendLine("    *   `[例如：词汇丰富，用词精准，能运用多种修辞手法增强表达效果。]`");
            reportBuilder.AppendLine("    *   `[例如：句式灵活多变，长短句结合得当，文笔流畅自然。]`");
            reportBuilder.AppendLine("*   **问题：**");
            reportBuilder.AppendLine("    *   `[例如：语言表达较为平实，缺乏文采和感染力。]`");
            reportBuilder.AppendLine("    *   `[例如：存在语病或用词不当之处，影响了阅读的流畅性。]`");
            reportBuilder.AppendLine("*   **具体示例：**");
            reportBuilder.AppendLine("    *   **优**：句子“……”中，使用了比喻的修辞，生动形象地写出了……。");
            reportBuilder.AppendLine("    *   **待改进**：原文中“我们应该变得更好”这类表述比较口语化，可以修改为“吾辈当于时代洪流中，淬炼自我，砥砺前行”等书面语，提升语言格调。");
            reportBuilder.AppendLine("### 4. 规范与书写 (卷面分)");
            reportBuilder.AppendLine("*   **本项扣分：** `[0 / 1 / 2] 分`");
            reportBuilder.AppendLine("*   **评价：**");
            reportBuilder.AppendLine("    *   `[例如：卷面整洁，字迹清晰，标点符号使用规范，无错别字。]`");
            reportBuilder.AppendLine("    *   `[例如：存在少量涂改，个别字迹潦草，需注意卷面整洁度。]`");
            reportBuilder.AppendLine("    *   `[例如：标点符号使用不规范，如“一逗到底”，需要加强练习。]`");
            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine("## 二、核心问题与提升建议");
            reportBuilder.AppendLine("1.  **当前首要问题诊断：**");
            reportBuilder.AppendLine("    *   `[用一两句话概括当前最需要解决的核心问题，例如：文章最大的短板在于论证的深度不足，未能深入挖掘材料内涵。]`");
            reportBuilder.AppendLine("2.  **具体提升建议：**");
            reportBuilder.AppendLine("    *   **审题立意方面：** 建议采用“多向辐射法”进行审题，从不同角度（个人、社会、历史、未来）思考材料，寻找最佳切入点，避免立意大众化。");
            reportBuilder.AppendLine("    *   **结构论证方面：** 练习撰写作文提纲，尤其要明确分论点之间的逻辑关系（并列、递进、对比）。每个论据后，增加1-2句分析，阐明论据如何支撑你的观点。");
            reportBuilder.AppendLine("    *   **语言表达方面：** 准备一个“高级词汇/精妙句式”积累本，日常阅读时注意摘抄和模仿。写作时，有意识地替换平淡的词语，尝试运用排比、对偶等句式增强气势。");
            reportBuilder.AppendLine("    *   **素材积累方面：** 建议多关注《人民日报》评论、时事热点深度解析等，积累新鲜、有深度的论据，替代老旧的“司马迁、屈原、爱迪生”等通用素材。");
            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine("## 三、细节修改建议 （若不需要修改可不输出）");
            reportBuilder.AppendLine("1.  **错别字/病句：**（若不需要修改可不输出）");
            reportBuilder.AppendLine("    *   `[例如：第2段第3句“……”，应改为“……”]`");
            reportBuilder.AppendLine("    *   `[例如：第4段第1句“……”，存在语病，应改为“……”]`");
            reportBuilder.AppendLine("2.  **段落调整：** （若不需要修改可不输出）");
            reportBuilder.AppendLine("    *   `[例如：建议将第3段和第4段合并，形成更强的论证逻辑。]`");
            reportBuilder.AppendLine("    *   `[例如：第5段可以拆分为两段，分别讨论“……”和“……”]`");
            reportBuilder.AppendLine("3.  **语言润色：** （若不需要修改可不输出）");
            reportBuilder.AppendLine("    *   `[例如：第1段第2句“……”可以改为“……”以增强文采。]`");
            reportBuilder.AppendLine("    *   `[例如：第2段最后一句“……”可以改为“……”以使结尾更有力。]`");
            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine("## 四、总结");
            reportBuilder.AppendLine("[一个比较完善的总结。可以结合AI模型给出的评语 深度概括。]");
            reportBuilder.AppendLine("\n--- 报告内容结束 ---"); // Add an end marker
            return reportBuilder.ToString();
        }
    }
}
