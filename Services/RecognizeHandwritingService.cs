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
        private readonly IConfiguration _configuration;

        public RecognizeHandwritingService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private AlibabaCloud.OpenApiClient.Client CreateClient()
        {
            var credential = new Aliyun.Credentials.Client();
            var config = new AlibabaCloud.OpenApiClient.Models.Config
            {
                AccessKeySecret = _configuration["Aliyun:AccessKeySecret"],
                AccessKeyId = _configuration["Aliyun:AccessKeyId"],
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
            var client = CreateClient();
            var apiParams = CreateApiInfo();
            using var body = StreamUtil.ReadFromFilePath(filePath);
            var runtime = new RuntimeOptions();
            var request = new OpenApiRequest { Stream = body };
            var resp = await Task.Run(() => client.CallApi(apiParams, request, runtime));
            return AlibabaCloud.TeaUtil.Common.ToJSONString(resp);
        }

        public async Task<string> ParseHandwritingResult(string json)
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            var dataStr = root["body"]?["Data"]?.ToString();
            if (string.IsNullOrEmpty(dataStr)) return "Data 字段为空";

            var dataObj = Newtonsoft.Json.Linq.JObject.Parse(dataStr);
            var wordsInfo = dataObj["prism_wordsInfo"] as Newtonsoft.Json.Linq.JArray;
            if (wordsInfo == null) return "prism_wordsInfo 字段为空";

            var result = new System.Text.StringBuilder();
            foreach (var wordInfo in wordsInfo)
            {
                var word = wordInfo["word"]?.ToString();
                var x = wordInfo["x"]?.ToString();
                var y = wordInfo["y"]?.ToString();
                if (!string.IsNullOrEmpty(word) && x != null && y != null)
                {
                    result.Append($"{word}({x},{y}) ");
                }
            } 
            return result.ToString().Trim();

        }
    }
}
