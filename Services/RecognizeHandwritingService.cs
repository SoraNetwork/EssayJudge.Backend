using System;
using System.IO;
using System.Threading.Tasks;
using AlibabaCloud.OpenApiClient;
using AlibabaCloud.OpenApiClient.Models;
using Aliyun.Credentials;
using AlibabaCloud.TeaUtil.Models;
using AlibabaCloud.DarabonbaStream;
using Tea.Utils;
using Microsoft.Extensions.Configuration;

namespace SoraEssayJudge.Services
{
    public class RecognizeHandwritingService
    {
        private readonly ApiKeyService _apiKeyService;

        public RecognizeHandwritingService(ApiKeyService apiKeyService)
        {
            _apiKeyService = apiKeyService;
        }

        private AlibabaCloud.OpenApiClient.Client CreateClient(string accessKeyId, string accessKeySecret)
        {
            var config = new AlibabaCloud.OpenApiClient.Models.Config
            {
                AccessKeyId = accessKeyId,
                AccessKeySecret = accessKeySecret,
                Endpoint = "ocr-api.cn-hangzhou.aliyuncs.com"
            };
            return new AlibabaCloud.OpenApiClient.Client(config);
        }

        private static Params CreateApiInfo()
        {
            return new Params
            {
                Action = "RecognizeHandwriting",
                Version = "2021-07-07",
                Protocol = "HTTPS",
                Method = "POST",
                AuthType = "AK",
                Style = "V3",
                Pathname = "/",
                ReqBodyType = "json",
                BodyType = "json",
                
            };
        }

        public async Task<string> RecognizeAsync(string filePath)
        {
            var apiKey = await _apiKeyService.GetAvailableKey("Aliyun");
            if (apiKey == null)
            {
                return "Error: No available Aliyun API key.";
            }

            var client = CreateClient(apiKey.Key, apiKey.Secret!);
            var apiParams = CreateApiInfo();
            using var body = StreamUtil.ReadFromFilePath(filePath);
            var runtime = new RuntimeOptions();
            

            Dictionary<string, object> queries = new Dictionary<string, object>(){};
            queries["NeedRotate"] = true;
            queries["OutputTable"] = false;
            queries["NeedSortPage"] = true;
            queries["Paragraph"] = true;

            var request = new OpenApiRequest
            {
                Query = AlibabaCloud.OpenApiUtil.Client.Query(queries),
                Stream = body,
            };
            var resp = await Task.Run(() => client.CallApi(apiParams, request, runtime));
            return AlibabaCloud.TeaUtil.Common.ToJSONString(resp);
        }
        public static string ParseHandwritingResultSync(string json)
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            var dataStr = root["body"]?["Data"]?.ToString();
            if (string.IsNullOrEmpty(dataStr)) return "Data 字段为空";

            var dataObj = Newtonsoft.Json.Linq.JObject.Parse(dataStr);
            var paragraphsInfo = dataObj["prism_paragraphsInfo"] as Newtonsoft.Json.Linq.JArray;
            if (paragraphsInfo == null) return "prism_paragraphsInfo 字段为空";

            var result = new System.Text.StringBuilder();
            foreach (var paraInfo in paragraphsInfo)
            {
                var paragraphId = paraInfo["paragraphId"]?.ToObject<int>();
                var word = paraInfo["word"]?.ToString();
                
                if (paragraphId != null && !string.IsNullOrEmpty(word))
                {
                    result.Append($"Para{paragraphId + 1}. {word} ");
                }
            } 
            return result.ToString().Trim();
        }
        public async Task<string> ParseHandwritingResult(string json)
        {
            return await Task.Run(() => ParseHandwritingResultSync(json));
        }
    }
}
