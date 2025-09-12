using OpenCvSharp;
using System;
using Microsoft.EntityFrameworkCore;
using SoraEssayJudge.Data;

namespace SoraEssayJudge.Services;

public interface IPreProcessImageServiceV2
{
    Task<string> ProcessAndRecognizeImageAsync(string imagePath);
    Task<string> PreProcessImageAsync(string imagePath);
}

public class PreProcessImageServiceV2 : IPreProcessImageServiceV2
{
    private readonly ILogger<PreProcessImageServiceV2> _logger;
    private readonly string _processedImagePath;
    private readonly ProcessImageService _processImageService;

    private readonly EssayContext _context;

    public PreProcessImageServiceV2(ILogger<PreProcessImageServiceV2> logger, IConfiguration configuration, ProcessImageService processImageService, EssayContext context)
    {
        _logger = logger;
        _processedImagePath = Path.Combine(Directory.GetCurrentDirectory(), "essayfiles");
        _processImageService = processImageService;
        Directory.CreateDirectory(_processedImagePath);
        _context = context;
    }


    public async Task<string> PreProcessImageAsync(string imagePath)
    {
        try
        {
            using var img = Cv2.ImRead(imagePath);
            if (img == null || img.Empty())
            {
                _logger.LogError("无法读取图片: {Path}", imagePath);
                throw new InvalidOperationException("图片读取失败");
            }

            var processor = new AnswerSheetProcessorV2();
            using var result = processor.Process(img,false,false);

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

    public async Task<string> ProcessAndRecognizeImageAsync(string imagePath)
    {
        try
        {
            using var img = Cv2.ImRead(imagePath);
            if (img == null || img.Empty())
            {
                _logger.LogError("无法读取图片: {Path}", imagePath);
                throw new InvalidOperationException("图片读取失败");
            }

            var processor = new AnswerSheetProcessorV2();
            using var result = processor.Process(img, false, false);

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

            // 调用ProcessImageService进行识别
            var ocrProcessingModelSetting = await _context.AIModelUsageSettings
                    .Include(s => s.AIModel)
                    .FirstOrDefaultAsync(s => s.UsageType == "OcrProcessing" && s.IsEnabled);

            if (ocrProcessingModelSetting == null || ocrProcessingModelSetting.AIModel == null)
            {
                    _logger.LogError("No enabled 'OcrProcessing' model configured in AIModelUsageSettings");
                    throw new InvalidOperationException("OCR processing model is not configured.");
            }
            string recognizedText = await _processImageService.ProcessImageAsyncV2(outputPath, Guid.NewGuid(), ocrProcessingModelSetting.AIModel.ModelId);
            return recognizedText;
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片处理过程中发生错误: {Path}", imagePath);
            throw;
        }
    }
}

