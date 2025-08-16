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
                请从提供的OCR文本中提取完整的作文内容。该文本由三栏OCR结果合并而成，可能包含非作文内容，且段落顺序可能因分栏处理而混乱。
                请根据文本内容和逻辑，参照文字块的正确顺序的顺序重组段落，提取作文主体。
                输出要求：
                1.  务必包含作文标题，标题单独成段，不带书名号。
                2.  正文按自然段落输出。
                3.  仅输出作文内容，不含任何额外说明。
                4.  允许修正因OCR错误导致的错别字，但不得修改原文的用词或语法错误。

                OCR文本如下：

                若OCR结果中疑似包含考生姓名，请输出 $$考生姓名$$ 单独成段（包括用于识别的符号$$） 
                将真实的考生姓名用$$包裹起来 放在文本末尾 若没有 不输出。
                不要只输出 $$考生姓名$$ 你需要把真实姓名填回去

                例如：
                $$王伟$$
                ";
            
            string chatResult = await _openAIService.GetChatCompletionAsync(userPrompt + combinedText, modelName);

            return chatResult;
        }
    }
}
