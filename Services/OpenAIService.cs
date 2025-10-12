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

        public async Task<string> GetFormatedChatCompletionAsync(string userPrompt, string model = "qwen-plus-latest", string? imagePath = null)
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
                    temperature = 0.65,
                    top_p = 0.5,
                    stream = true,
                    // enable_thinking = true,
                    // thinking_budget = 500
                    response_format = new
                    {
                        type= "json_object"
                    }
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
                    temperature = 0.65,
                    top_p = 0.5,
                    stream = true,
                    // enable_thinking = true,
                    // thinking_budget = 500
                    response_format = new
                    {
                        type = "json_object"
                    }
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
                                //         
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                //      SSE   ʽ
                                if (line.StartsWith("data: "))
                                {
                                    var jsonStr = line.Substring(6).Trim();

                                    //     Ƿ  ǽ      
                                    if (jsonStr.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        var responseObject = JObject.Parse(jsonStr);

                                        //   ȡ  ͨ    
                                        var deltaContent = responseObject["choices"]?[0]?["delta"]?["content"]?.ToString();
                                        if (!string.IsNullOrEmpty(deltaContent))
                                        {
                                            fullContent.Append(deltaContent);
                                        }

                                        //   ȡ thinking    ݣ     У 
                                        var thinkingDelta = responseObject["choices"]?[0]?["delta"]?["thinking"]?.ToString();
                                        if (!string.IsNullOrEmpty(thinkingDelta))
                                        {
                                            thinkingContent.Append(thinkingDelta);
                                        }

                                        //     Ƿ  д   
                                        var error = responseObject["error"]?.ToString();
                                        if (!string.IsNullOrEmpty(error))
                                        {
                                            return $"Error in stream: {error}";
                                        }
                                    }
                                    catch (JsonReaderException ex)
                                    {
                                        //   ¼ ޷        У           
                                        Console.WriteLine($"Failed to parse JSON: {jsonStr}, Error: {ex.Message}");
                                    }
                                }
                                else if (line.StartsWith("event:") || line.StartsWith("id:"))
                                {
                                    //      SSE   Ԫ      
                                    continue;
                                }
                                else
                                {
                                    //   ¼      и ʽ
                                    Console.WriteLine($"Unexpected line format: {line}");
                                }
                            }
                        }

                        //       thinking    ݣ     ѡ   Եذ      ¼
                        if (thinkingContent.Length > 0)
                        {
                            Console.WriteLine($"Thinking process: {thinkingContent}");
                        }

                        //     Ƿ   Ļ ȡ        
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
                    temperature = 0.65,
                    top_p = 0.5,
                    stream = true,
                    enable_thinking = true,
                    // thinking_budget = 500
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
                    temperature = 0.65,
                    top_p = 0.5,
                    stream = true,
                    enable_thinking = true,
                    // thinking_budget = 500
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
                                //         
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                //      SSE   ʽ
                                if (line.StartsWith("data: "))
                                {
                                    var jsonStr = line.Substring(6).Trim();

                                    //     Ƿ  ǽ      
                                    if (jsonStr.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        var responseObject = JObject.Parse(jsonStr);

                                        //   ȡ  ͨ    
                                        var deltaContent = responseObject["choices"]?[0]?["delta"]?["content"]?.ToString();
                                        if (!string.IsNullOrEmpty(deltaContent))
                                        {
                                            fullContent.Append(deltaContent);
                                        }

                                        //   ȡ thinking    ݣ     У 
                                        var thinkingDelta = responseObject["choices"]?[0]?["delta"]?["thinking"]?.ToString();
                                        if (!string.IsNullOrEmpty(thinkingDelta))
                                        {
                                            thinkingContent.Append(thinkingDelta);
                                        }

                                        //     Ƿ  д   
                                        var error = responseObject["error"]?.ToString();
                                        if (!string.IsNullOrEmpty(error))
                                        {
                                            return $"Error in stream: {error}";
                                        }
                                    }
                                    catch (JsonReaderException ex)
                                    {
                                        //   ¼ ޷        У           
                                        Console.WriteLine($"Failed to parse JSON: {jsonStr}, Error: {ex.Message}");
                                    }
                                }
                                else if (line.StartsWith("event:") || line.StartsWith("id:"))
                                {
                                    //      SSE   Ԫ      
                                    continue;
                                }
                                else
                                {
                                    //   ¼      и ʽ
                                    Console.WriteLine($"Unexpected line format: {line}");
                                }
                            }
                        }

                        //       thinking    ݣ     ѡ   Եذ      ¼
                        if (thinkingContent.Length > 0)
                        {
                            Console.WriteLine($"Thinking process: {thinkingContent}");
                        }

                        //     Ƿ   Ļ ȡ        
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