using OpenCvSharp;
using System;

namespace SoraEssayJudge.Services;

public interface IPreProcessImageService
{
    Task<string> ProcessImageAsync(string imagePath);
}

public class PreProcessImageService : IPreProcessImageService
{
    private readonly ILogger<PreProcessImageService> _logger;
    private readonly string _processedImagePath;

    public PreProcessImageService(ILogger<PreProcessImageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _processedImagePath = Path.Combine(Directory.GetCurrentDirectory(), "essayfiles");
        Directory.CreateDirectory(_processedImagePath);
    }

    public async Task<string> ProcessImageAsync(string imagePath)
    {
        try
        {
            using var img = Cv2.ImRead(imagePath);
            if (img == null || img.Empty())
            {
                _logger.LogError("无法读取图片: {Path}", imagePath);
                throw new InvalidOperationException("图片读取失败");
            }

            var processor = new AnswerSheetProcessor();
            using var result = processor.Process(img);
            
            if (result == null)
            {
                _logger.LogError("图片处理失败: {Path}", imagePath);
                throw new InvalidOperationException("图片处理失败");
            }

            // 生成新的文件名，固定为.webp扩展名
            string fileName = $"{Guid.NewGuid()}.webp";
            string outputPath = Path.Combine(_processedImagePath, fileName);

            // 设置WebP编码参数，质量为90
            var parameters = new ImageEncodingParam(ImwriteFlags.WebPQuality, 90);
            await Task.Run(() => Cv2.ImWrite(outputPath, result, parameters));
            _logger.LogInformation("图片处理成功，压缩为WebP并保存至: {Path}", outputPath);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片处理过程中发生错误: {Path}", imagePath);
            throw;
        }
    }
}

