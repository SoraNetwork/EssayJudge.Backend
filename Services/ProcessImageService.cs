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

        public async Task<string> ProcessImageAsync(string imagePath, int columnCount)
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
                        string tempPath = Path.Combine(Path.GetTempPath(), $"column_{i}.png");
                        columnImage.Save(tempPath);

                        string result = await _recognizeHandwritingService.RecognizeAsync(tempPath);
                        string text = await _recognizeHandwritingService.ParseHandwritingResult(result);
                        columnTexts.Add($"第{i + 1}栏：\n{text}\n");

                        File.Delete(tempPath);
                    }
                }
            }

            string combinedText = string.Join("\n", columnTexts);
            string userPrompt = "system: 这里是OCR的返回结果。中间可能有其他内容，请得到其中存在的作文内容。请注意，作文一般为三栏，OCR的结果可能会导致自然段混乱，请注意根据x y坐标的分布判断。优先左上，左下，中上，中下，右上，右下。 \n （请务必包括标题，请输出标题单独成段（不需要输出书名号）。自然段）仅输出这篇文章的内容，不要输出其他内容，可以修改部分因OCR错误导致的错别字（但原文用词不当补课修改）\n";
            
            string chatResult = await _openAIService.GetChatCompletionAsync(userPrompt + combinedText, "qwen-plus");

            return chatResult;
        }
    }
}
