using Serilog.Core;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Web;

namespace SoraEssayJudge.Services;

public class UploadTemporaryImageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UploadTemporaryImageService> _logger;

    public UploadTemporaryImageService(HttpClient httpClient, ILogger<UploadTemporaryImageService> Logger)
    {
        _httpClient = httpClient;
        _logger = Logger;
    }

    public async Task<string> UploadFileAndGetUrlAsync(string apiKey, string modelName, string filePath)
    {
        try
        {
            // 1. 获取上传凭证
            var policyData = await GetUploadPolicyAsync(apiKey, modelName);

            // 2. 上传文件到OSS
            var ossUrl = await UploadFileToOssAsync(policyData, filePath);

            // 3. 计算过期时间并返回结果
            var expireTime = DateTime.Now.AddHours(48);
            _logger.LogInformation("Successfully uploaded file to TemporaryOSS. URL: {OssUrl}, Expire Time: {ExpireTime}", ossUrl, expireTime);
            

            return ossUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            throw;
        }
    }

    private async Task<PolicyData> GetUploadPolicyAsync(string apiKey, string modelName)
    {
        var url = "https://dashscope.aliyuncs.com/api/v1/uploads";

        // 构建查询参数
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["action"] = "getPolicy";
        queryParams["model"] = modelName;

        var requestUrl = $"{url}?{queryParams}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to get upload policy: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<PolicyResponse>();
        return result?.Data ?? throw new Exception("Invalid policy response");
    }

    private async Task<string> UploadFileToOssAsync(PolicyData policyData, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var key = $"{policyData.UploadDir}/{fileName}";

        // 读取文件内容
        var fileBytes = await File.ReadAllBytesAsync(filePath);

        // 使用标准的边界字符串
        var boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N");

        // 手动构建 multipart/form-data 内容
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, new System.Text.UTF8Encoding(false));

        // 辅助方法：写入表单字段
        async Task WriteFieldAsync(string name, string value)
        {
            await writer.WriteAsync($"--{boundary}\r\n");
            await writer.WriteAsync($"Content-Disposition: form-data; name=\"{name}\"\r\n");
            await writer.WriteAsync("\r\n");
            await writer.WriteAsync($"{value}\r\n");
        }

        // 按照正确的顺序添加表单域
        await WriteFieldAsync("key", key);
        await WriteFieldAsync("OSSAccessKeyId", policyData.OssAccessKeyId);
        await WriteFieldAsync("policy", policyData.Policy);
        await WriteFieldAsync("success_action_status", "200");
        await WriteFieldAsync("signature", policyData.Signature);

        // 添加 OSS 特定的头部字段（如果存在）
        if (!string.IsNullOrEmpty(policyData.XOssObjectAcl))
            await WriteFieldAsync("x-oss-object-acl", policyData.XOssObjectAcl);

        if (!string.IsNullOrEmpty(policyData.XOssForbidOverwrite))
            await WriteFieldAsync("x-oss-forbid-overwrite", policyData.XOssForbidOverwrite);

        // 添加 file 字段（必须是最后一个）
        await writer.WriteAsync($"--{boundary}\r\n");
        await writer.WriteAsync($"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n");
        await writer.WriteAsync("Content-Type: application/octet-stream\r\n");
        await writer.WriteAsync("\r\n");
        await writer.FlushAsync();

        // 写入文件内容
        await memoryStream.WriteAsync(fileBytes, 0, fileBytes.Length);

        // 写入结束边界
        await writer.WriteAsync($"\r\n--{boundary}--\r\n");
        await writer.FlushAsync();

        // 准备请求
        memoryStream.Position = 0;
        var content = new StreamContent(memoryStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data")
        {
            Parameters = { new NameValueHeaderValue("boundary", boundary) }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, policyData.UploadHost)
        {
            Content = content
        };

        // 发送请求
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("OSS upload failed. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, errorContent);

            // 记录请求详情以便调试
            _logger.LogDebug("Upload details - Key: {Key}, Boundary: {Boundary}, FileSize: {Size}",
                key, boundary, fileBytes.Length);

            throw new Exception($"Failed to upload file to OSS: {errorContent}");
        }

        var ossUrl = $"oss://{key}";
        _logger.LogInformation("File uploaded successfully to: {Url}", ossUrl);

        return ossUrl;
    }

    /// <summary>
    /// 手动添加表单字段，确保 Content-Disposition 格式符合 OSS 要求
    /// </summary>
    private void AddFormField(MultipartFormDataContent formData, string fieldName, string value)
    {
        var content = new StringContent(value);

        // 清除默认的 Content-Type，让 OSS 能够正确解析
        content.Headers.ContentType = null;

        // 手动设置 Content-Disposition 头，确保格式正确
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = $"\"{fieldName}\""
        };

        formData.Add(content);
    }
}

public class PolicyResponse
{
    [JsonPropertyName("data")]
    public PolicyData Data { get; set; } = new();
}

public class PolicyData
{
    [JsonPropertyName("upload_dir")]
    public string UploadDir { get; set; } = string.Empty;

    [JsonPropertyName("oss_access_key_id")]
    public string OssAccessKeyId { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = string.Empty;

    [JsonPropertyName("x_oss_object_acl")]
    public string XOssObjectAcl { get; set; } = string.Empty;

    [JsonPropertyName("x_oss_forbid_overwrite")]
    public string XOssForbidOverwrite { get; set; } = string.Empty;

    [JsonPropertyName("upload_host")]
    public string UploadHost { get; set; } = string.Empty;
}