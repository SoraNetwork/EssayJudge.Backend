using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SoraEssayJudge.Dtos;
using Newtonsoft.Json;
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
        public async Task<FormatedHandwritingResponseDto> ProcessImageAsyncV3Formated(string imagePath, Guid id, string modelName)
        {
            string userPrompt = @"system:
                作文提取
                任务目标
                从提供的图片中提取完整的作文内容，并按照指定的JSON格式进行结构化输出。

                图片特征说明
                图片可能包含多栏布局

                可能混杂非作文内容

                段落顺序可能存在混乱

                可能包含考生姓名、考号和标题信息

                关键信息位置特征
                标题位置特征
                标题通常出现在以下位置之一：

                图片最顶部，居中或居左排列

                正文内容之前，单独成行

                可能使用较大字号或加粗样式

                通常不包含在正文段落中

                可能位于考生信息下方或上方

                考生信息位置特征
                考生姓名和考号通常出现在：

                图片顶部区域，标题上方或下方

                固定格式区域（如指定填写框）

                可能分布在左右两侧或上下排列

                处理要求
                内容提取优先级
                首先识别标题：重点扫描图片顶部区域，识别明显的标题特征

                提取考生信息：识别考生姓名和考号信息

                提取正文内容：重组段落，确保逻辑连贯

                JSON输出格式
                必须严格按照以下JSON结构输出：

                json
                {
                  ""studentInfo"": {
                    ""name"": ""识别到的考生姓名"",
                    ""id"": ""识别到的考号""
                  },
                  ""essayInfo"": {
                    ""title"": ""作文标题"",
                    ""content"": ""完整的作文内容""
                  }
                }
                字段说明
                studentInfo：考生信息部分

                name：填入实际识别到的考生姓名

                id：填入实际识别到的考号（如果有考号一定是8位数字，如果不是，则不是考号，输出null）

                如未识别到考生姓名或考号，相应字段设为null

                essayInfo：作文信息部分

                title：必须准确识别并提取作文标题，标题应独立于正文，不使用书名号《》。
                标题如果小于4个字的，一般不是真正的标题，请你重新寻找可能存在的标题。

                content：完整的正文内容，按照自然段落组织，确保语义连贯

                标题识别特别要求
                重点扫描区域：优先处理图片顶部1/3区域，识别可能的标题文本

                格式特征：注意识别可能使用不同字号、字体或格式的文本作为标题

                位置验证：确认标题位于正文开始之前，且与正文有明显区分

                完整性：确保标题完整提取，不遗漏任何部分

                文字处理
                错别字修正：允许修正因OCR识别错误导致的明显错别字

                原文保留：不得修改原文的用词习惯或语法结构（即使是错误的）

                信息提取：准确识别并提取考生姓名、考号和标题信息

                考生信息处理规范
                若OCR结果中识别到考生信息：

                将考生姓名填入studentInfo.name字段

                将考号填入studentInfo.id字段

                如未识别到相应信息，将对应字段设为null

                输出要求
                必须输出完整的JSON对象

                确保JSON格式正确无误

                特别确保标题准确识别并正确填入title字段

                不包含任何额外的说明或注释

                准确提取并填写考生姓名和考号信息";

            string chatResult = await _openAIService.GetChatCompletionAsync(userPrompt, modelName, imagePath);
            try
            {
                var res = JsonConvert.DeserializeObject<FormatedHandwritingResponseDto>(chatResult);
                return res!;
            }
            catch(Exception ex)
            {
                throw;
            }
            
        }
        public async Task<string> ProcessImageAsyncV3(string imagePath, Guid id,string modelName)
        {
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
                1. **标题格式**：必须包含作文标题，独立成段，不使用书名号《》。标题小于4个字的，一般不是真正的标题，请重新寻找可能存在的标题。
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
