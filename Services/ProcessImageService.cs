using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SoraEssayJudge.Services
{
    public class ProcessImageService
    {
        private readonly RecognizeHandwritingService _recognizeHandwritingService;
        private readonly OpenAIService _openAIService;

        

        public ProcessImageService(RecognizeHandwritingService recognizeHandwritingService, OpenAIService openAIService)
        {
            _recognizeHandwritingService = recognizeHandwritingService;
            _openAIService = openAIService;
        }
        public async Task<string> ProcessImageAsyncV3(string imagePath, Guid id,string modelName)
        {
            string text;
            string userPrompt = @"
                system:
                # 作文提取

                ## 任务目标

                从提供的图片中提取完整的作文内容，并按要求进行重组和格式化。

                ## 图片特征说明

                - 图片可能包含多栏布局
                - 可能混杂非作文内容
                - 段落顺序可能存在混乱

                ## 处理要求

                ### 内容提取
                1. 准确识别图片中的文字内容
                2. 识别并重组因分栏而被分割的自然段落
                3. 根据文本内容和逻辑关系，确定正确的段落顺序

                ### 格式规范
                1. **标题格式**：必须包含作文标题，独立成段，不使用书名号《》
                2. **正文格式**：按照自然段落输出，确保语义连贯
                3. **纯净输出**：仅输出作文内容，不包含任何额外说明或注释

                ### 文字处理
                1. **错别字修正**：允许修正因OCR识别错误导致的明显错别字
                2. **原文保留**：不得修改原文的用词习惯或语法结构（即使是错误的）

                ### 姓名处理规范

                若OCR结果中识别到考生姓名：
                - 将考生姓名以 `$$考生姓名$$` 格式单独成段
                - 将此标识放在作文正文之后
                - 若未识别到考生姓名，则不输出此部分内容

                > **重要提示**：不得只输出 `$$考生姓名$$` 字样，必须填入实际识别到的考生姓名

                ## 输出示例

                ```
                文章标题

                正文第一段内容...

                正文第二段内容...

                $$张三$$
                ```
                ";
            string chatResult = await _openAIService.GetChatCompletionAsync(userPrompt, modelName, imagePath);
            return chatResult;
        }

        public async Task<string> ProcessImageAsyncV2(string imagePath, Guid id, string modelName)
        {
            string text;
            using (Image image = Image.Load(imagePath))
            {
                string result = await _recognizeHandwritingService.RecognizeAsync(imagePath);
                text = await _recognizeHandwritingService.ParseHandwritingResult(result);
            }
            
            string userPrompt = @"system:
                请从提供的OCR文本中提取完整的作文内容。该文本可能由多栏OCR结果合并而成，可能包含非作文内容，且段落分段可能混乱。
                请根据文本内容和逻辑，参照文字块的正确顺序的顺序重组段落，提取作文主体。
                输出要求：
                1.  务必包含作文标题，标题单独成段，不带书名号。
                2.  正文按自然段落输出。
                3.  仅输出作文内容，不含任何额外说明。
                4.  允许修正因OCR错误导致的错别字，但不得修改原文的用词或语法错误。

                若OCR结果中疑似包含考生姓名，请输出 $$考生姓名$$ 单独成段（包括用于识别的符号$$） 
                将真实的考生姓名用$$包裹起来 放在文本末尾 若没有 不输出。
                不要只输出 $$考生姓名$$ 你需要把真实姓名填回去

                例如：
                $$王伟$$

                OCR文本如下： \n
                ";

            string chatResult = await _openAIService.GetChatCompletionAsync(userPrompt + text, modelName);

            return chatResult;
        }
        public async Task<string> ProcessImageAsync(string imagePath, int columnCount, Guid id, string modelName)
        {
            var columnTexts = new List<string>();
            using (Image image = Image.Load(imagePath))
            {
                int columnWidth = image.Width / columnCount;
                for (int i = 0; i < columnCount; i++)
                {
                    var rect = new Rectangle(i * columnWidth, 0, columnWidth, image.Height);
                    using (Image columnImage = image.Clone(ctx => ctx.Crop(rect)))
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), $"{id}_column_{i}.webp");
                        columnImage.SaveAsWebp(tempPath);

                        string result = await _recognizeHandwritingService.RecognizeAsync(tempPath);
                        string text = await _recognizeHandwritingService.ParseHandwritingResult(result);
                        columnTexts.Add($"第{i + 1}栏：\n{text}\n");

                        File.Delete(tempPath);
                    }
                }
            }

            string combinedText = string.Join("\n", columnTexts);
            string userPrompt = @"system:
                请从提供的OCR文本中提取完整的作文内容。该文本可能由多栏OCR结果合并而成，可能包含非作文内容，且段落顺序可能因分栏处理而混乱。
                请根据文本内容和逻辑，参照文字块的正确顺序的顺序重组段落，提取作文主体。
                输出要求：
                1.  务必包含作文标题，标题单独成段，不带书名号。
                2.  正文按自然段落输出。
                3.  仅输出作文内容，不含任何额外说明。
                4.  允许修正因OCR错误导致的错别字，但不得修改原文的用词或语法错误。

                若OCR结果中疑似包含考生姓名，请输出 $$考生姓名$$ 单独成段（包括用于识别的符号$$） 
                将真实的考生姓名用$$包裹起来 放在文本末尾 若没有 不输出。
                不要只输出 $$考生姓名$$ 你需要把真实姓名填回去

                例如：
                $$王伟$$

                OCR文本如下：
                ";

            string chatResult = await _openAIService.GetChatCompletionAsync(userPrompt + combinedText, modelName);

            return chatResult;
        }
    }
}
