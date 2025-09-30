using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Microsoft.IdentityModel.Tokens;

namespace SoraEssayJudge.Services
{
    public class OpenAIService
    {
        private readonly ApiKeyService _apiKeyService;
        private readonly HttpClient _httpClient;
        private readonly UploadTemporaryImageService _uploadTemporaryImageService;

        public OpenAIService(IConfiguration configuration, ApiKeyService apiKeyService, UploadTemporaryImageService uploadTemporaryImageService)
        {
            _apiKeyService = apiKeyService;
            _httpClient = new HttpClient();
            _uploadTemporaryImageService = uploadTemporaryImageService;
        }

        public async Task<string> GetChatCompletionAsync(string userPrompt, string model = "qwen-plus-latest", string? imagePath = null)
        {
            var apiKey = await _apiKeyService.GetApiKeyForModel(model);
            if (apiKey == null)
            {
                return "Error: No available OpenAI API key.";
            }
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Key);

            object requestBody;
            if (string.IsNullOrEmpty(imagePath))
            {
                requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                new { role = "user", content = userPrompt }
            },
                    temperature = 0.5,
                    top_p = 0.5,
                    stream = true,
                    enable_thinking = true,
                    thinking_budget = 500
                };
            }
            else
            {
                var image_Url = await _uploadTemporaryImageService.UploadFileAndGetUrlAsync(apiKey.Key, model, imagePath);
                requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new { url = image_Url }
                        },
                        new
                        {
                            type = "text",
                            text = userPrompt
                        }
                    }
                }
            },
                    temperature = 0.5,
                    top_p = 0.5,
                    stream = true,
                    enable_thinking = true,
                    thinking_budget = 500
                };
            }

            var requestUri = $"{apiKey.Endpoint!}";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("X-DashScope-OssResourceResolve", "enable");

            try
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var fullContent = new StringBuilder();
                        var thinkingContent = new StringBuilder();

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                // 跳过空行
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                // 处理 SSE 格式
                                if (line.StartsWith("data: "))
                                {
                                    var jsonStr = line.Substring(6).Trim();

                                    // 检查是否是结束标记
                                    if (jsonStr.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        var responseObject = JObject.Parse(jsonStr);

                                        // 提取普通内容
                                        var deltaContent = responseObject["choices"]?[0]?["delta"]?["content"]?.ToString();
                                        if (!string.IsNullOrEmpty(deltaContent))
                                        {
                                            fullContent.Append(deltaContent);
                                        }

                                        // 提取 thinking 内容（如果有）
                                        var thinkingDelta = responseObject["choices"]?[0]?["delta"]?["thinking"]?.ToString();
                                        if (!string.IsNullOrEmpty(thinkingDelta))
                                        {
                                            thinkingContent.Append(thinkingDelta);
                                        }

                                        // 检查是否有错误
                                        var error = responseObject["error"]?.ToString();
                                        if (!string.IsNullOrEmpty(error))
                                        {
                                            return $"Error in stream: {error}";
                                        }
                                    }
                                    catch (JsonReaderException ex)
                                    {
                                        // 记录无法解析的行，但继续处理
                                        Console.WriteLine($"Failed to parse JSON: {jsonStr}, Error: {ex.Message}");
                                    }
                                }
                                else if (line.StartsWith("event:") || line.StartsWith("id:"))
                                {
                                    // 忽略 SSE 的元数据行
                                    continue;
                                }
                                else
                                {
                                    // 记录意外的行格式
                                    Console.WriteLine($"Unexpected line format: {line}");
                                }
                            }
                        }

                        // 如果有 thinking 内容，可以选择性地包含或记录
                        if (thinkingContent.Length > 0)
                        {
                            Console.WriteLine($"Thinking process: {thinkingContent}");
                        }

                        // 检查是否真的获取到了内容
                        if (fullContent.Length == 0)
                        {
                            return "Error: No content received from API";
                        }

                        return fullContent.ToString();
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        return $"Error: {response.StatusCode} - {errorContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
        }
    }
}