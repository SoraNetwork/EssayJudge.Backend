using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;

namespace SoraEssayJudge.Services
{
    public class OpenAIService
    {
        private readonly ApiKeyService _apiKeyService;
        private readonly HttpClient _httpClient;

        public OpenAIService(IConfiguration configuration, ApiKeyService apiKeyService)
        {
            _apiKeyService = apiKeyService;
            _httpClient = new HttpClient();
        }

        public async Task<string> GetChatCompletionAsync(string userPrompt, string model = "qwen-plus-latest")
        {
            var apiKey = await _apiKeyService.GetApiKeyForModel(model);
            if (apiKey == null)
            {
                return "Error: No available OpenAI API key.";
            }
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Key);
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                },
                stream = true
            };

            var requestUri = $"{apiKey.Endpoint!}";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json")
            };

            try
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var fullContent = new StringBuilder();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = await reader.ReadLineAsync();
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                if (line.StartsWith("data: "))
                                {
                                    var jsonStr = line.Substring(6);
                                    if (jsonStr.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        var responseObject = JObject.Parse(jsonStr);
                                        var deltaContent = responseObject["choices"]?[0]?["delta"]?["content"]?.ToString();
                                        if (!string.IsNullOrEmpty(deltaContent))
                                        {
                                            fullContent.Append(deltaContent);
                                        }
                                    }
                                    catch (JsonReaderException)
                                    {
                                    }
                                }
                            }
                        }
                        return fullContent.ToString();
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        // Log or handle the error appropriately
                        return $"Error: {response.StatusCode} - {errorContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception
                return $"Exception: {ex.Message}";
            }
        }
    }
}
