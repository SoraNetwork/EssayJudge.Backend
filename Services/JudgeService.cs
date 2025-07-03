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
                    string parsedText = await processImageService.ProcessImageAsync(imagePath!, submission.ColumnCount, Guid.NewGuid());
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

            reportBuilder.AppendLine("\n不要完全按照模板，需要个性化评测报告 --- 报告格式要求 --- "); 
            reportBuilder.AppendLine("# 作文批改分析报告");
            reportBuilder.AppendLine("| 项目 | 内容 |");
            reportBuilder.AppendLine("| :--- | :--- |");
            reportBuilder.AppendLine("| **作文题目** | [在此处填写完整的作文题目] |");
            reportBuilder.AppendLine($"| **预估分数** | **{submission.FinalScore:F0} / {assignment.TotalScore} 分** |"); // Use actual score and total score
            reportBuilder.AppendLine("## 综合评价");
            reportBuilder.AppendLine($"*   **总分：** **{submission.FinalScore:F0} / {assignment.TotalScore} 分**"); // Use actual score and total score
            reportBuilder.AppendLine("*   **等级划分：** `[一类卷 / 二类卷上 / 二类卷下 / 三类卷等]`");
            reportBuilder.AppendLine("*   **一句话总评：** `[例如：立意精准，论证有力，但语言细节有待打磨。]` 或 `[例如：结构完整，但思想深度不足，论据较为陈旧。]`");
            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine("## 三、多维度分析与诊断");
            reportBuilder.AppendLine("<!-- ");
            reportBuilder.AppendLine("    以下四个维度是高考作文评分的核心。");
            reportBuilder.AppendLine("    - “亮点”用于肯定学生的优点，建立信心。");
            reportBuilder.AppendLine("    - “问题”用于指出不足，明确方向。");
            reportBuilder.AppendLine("    - “具体示例”是关键，务必引用原文，使分析有据可依。");
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
            reportBuilder.AppendLine("## 四、核心问题与提升建议");
            reportBuilder.AppendLine("1.  **当前首要问题诊断：**");
            reportBuilder.AppendLine("    *   `[用一两句话概括当前最需要解决的核心问题，例如：文章最大的短板在于论证的深度不足，未能深入挖掘材料内涵。]`");
            reportBuilder.AppendLine("2.  **具体提升建议：**");
            reportBuilder.AppendLine("    *   **审题立意方面：** 建议采用“多向辐射法”进行审题，从不同角度（个人、社会、历史、未来）思考材料，寻找最佳切入点，避免立意大众化。");
            reportBuilder.AppendLine("    *   **结构论证方面：** 练习撰写作文提纲，尤其要明确分论点之间的逻辑关系（并列、递进、对比）。每个论据后，增加1-2句分析，阐明论据如何支撑你的观点。");
            reportBuilder.AppendLine("    *   **语言表达方面：** 准备一个“高级词汇/精妙句式”积累本，日常阅读时注意摘抄和模仿。写作时，有意识地替换平淡的词语，尝试运用排比、对偶等句式增强气势。");
            reportBuilder.AppendLine("    *   **素材积累方面：** 建议多关注《人民日报》评论、时事热点深度解析等，积累新鲜、有深度的论据，替代老旧的“司马迁、屈原、爱迪生”等通用素材。");
            reportBuilder.AppendLine("---");
            reportBuilder.AppendLine("## 五、总结");
            reportBuilder.AppendLine("[一个比较完善的总结。可以结合AI模型给出的评语 深度概括。]");
            reportBuilder.AppendLine("\n--- 报告内容结束 ---"); // Add an end marker
            return reportBuilder.ToString();
        }
    }
}
