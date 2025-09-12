using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoraEssayJudge.Services;

public static class PointExtensions
{
    public static double DistanceTo(this Point p1, Point p2)
    {
        return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }

    public static double DistanceToSquared(this Point2f p1, Point2f p2)
    {
        return Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
    }

    public static Point2f ToPoint2f(this Point point)
    {
        return new Point2f(point.X, point.Y);
    }
}

public class AnswerSheetProcessorV2
{
    private const double SCAN_AREA_MIN_RATIO = 0.00014;
    private const double SCAN_AREA_MAX_RATIO = 0.00025;
    private const double ROTATE_SYMBOL_MIN_RATIO = 0.00005;
    private const double ROTATE_SYMBOL_MAX_RATIO = 0.0001;

    /// <summary>
    /// 处理答题卡图像
    /// </summary>
    /// <param name="imagePath">图像路径</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="enablePerspectiveCorrection">是否启用透视变换</param>
    /// <param name="enhanceScanLook">是否启用扫描增强</param>
    /// <returns>处理后的图像</returns>
    public Mat Process(string imagePath,
                      bool enablePerspectiveCorrection = false,
                      bool enhanceScanLook = false)
    {
        using Mat src = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (src.Empty())
        {
            Console.WriteLine($"Error: Could not load image from {imagePath}");
            return null;
        }

        Mat result = Process(src, enablePerspectiveCorrection, enhanceScanLook);

        return result;
    }

    /// <summary>
    /// 处理答题卡图像
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <param name="enablePerspectiveCorrection">是否启用透视变换</param>
    /// <param name="enhanceScanLook">是否启用扫描增强</param>
    /// <returns>处理后的图像</returns>
    public Mat Process(Mat image, bool enablePerspectiveCorrection = false, bool enhanceScanLook = false)
    {
        if (image == null || image.Empty())
            return null;

        // Create a copy of the input image that we'll work with
        using Mat src = image.Clone();
        Mat workingImage = src.Clone(); // This will be our working image throughout the process

        try
        {
            // 1. 透视变换（如果需要）- 在主要处理之前执行
            if (enablePerspectiveCorrection)
            {
                Mat correctedImage = ApplyPerspectiveCorrection(workingImage);
                if (correctedImage != null)
                {
                    workingImage.Dispose(); // Dispose the old working image
                    workingImage = correctedImage; // Use the corrected image for further processing
                }
            }

            // 2. 扫描增强（如果需要）- 在透视变换之后，主要处理之前执行
            if (enhanceScanLook)
            {
                EnhanceToScannedLook(workingImage);
            }

            // 3. 预处理图像
            using Mat edges = PreprocessImage(workingImage);

            // 4. 检测所有可能的标记
            var (scanAreas, rotateSymbolArea) = DetectMarkers(edges, workingImage.Size(), workingImage);

            if (scanAreas.Count < 4)
            {
                Console.WriteLine($"Error: Not enough scan areas found. Found: {scanAreas.Count}, Required: at least 4");
                return null;
            }

            Console.WriteLine($"Found {scanAreas.Count} scan areas");

            // 5. 计算平均距离
            Point[] scanAreasIndexPoint = scanAreas.Select(s => s.MinBy(p => p.X + p.Y)).ToArray();
            int averageWidth = GetAverageDistance(scanAreasIndexPoint.Select(p => p.X).ToArray());
            int averageHeight = GetAverageDistance(scanAreasIndexPoint.Select(p => p.Y).ToArray());

            // 6. 提取和拼接区域
            Mat result = ExtractAndStitchAreas(workingImage, scanAreas, scanAreasIndexPoint, averageWidth, averageHeight, rotateSymbolArea);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing image: {ex.Message}");
            return null;
        }
        finally
        {
            // Clean up the working image if it's different from the original src
            if (workingImage != src)
            {
                workingImage.Dispose();
            }
        }
    }

    private Mat PreprocessImage(Mat image)
    {
        using Mat gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

        Mat edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        return edges;
    }

    private (List<Point[]>, Point[]) DetectMarkers(Mat edges, Size imageSize, Mat src)
    {
        Cv2.FindContours(edges,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        double totalSize = edges.Width * edges.Height;
        double scanAreaMinSize = totalSize * SCAN_AREA_MIN_RATIO;
        double scanAreaMaxSize = totalSize * SCAN_AREA_MAX_RATIO;
        double rotateSymbolAreaMinSize = totalSize * ROTATE_SYMBOL_MIN_RATIO;
        double rotateSymbolAreaMaxSize = totalSize * ROTATE_SYMBOL_MAX_RATIO;

        List<Point[]> scanAreas = new List<Point[]>();
        Point[] rotateSymbolArea = null;

        foreach (var contour in contours)
        {
            double epsilon = 0.05 * Cv2.ArcLength(contour, true);
            Point[] approx = Cv2.ApproxPolyDP(contour, epsilon, true);
            double size = Cv2.ContourArea(approx);

            Console.WriteLine(size); // 保留原程序的调试输出

            if (approx.Length == 4)
            {
                if (size > rotateSymbolAreaMinSize && size < rotateSymbolAreaMaxSize)
                {
                    // 在源图像上标记旋转符号区域（红色）
                    Cv2.Polylines(src, new Point[][] { approx }, true, Scalar.Red, 1);
                    rotateSymbolArea = approx;
                }

                if (size > scanAreaMinSize && size < scanAreaMaxSize)
                {
                    scanAreas.Add(approx);
                }
            }
        }

        return (scanAreas, rotateSymbolArea);
    }

    private Mat ApplyPerspectiveCorrection(Mat image)
    {
        try
        {
            // 使用边缘检测找到最大的四边形轮廓（通常是纸张边缘）
            using Mat gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            using Mat blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

            using Mat edges = new Mat();
            Cv2.Canny(blurred, edges, 50, 150);

            // 形态学操作以连接边缘
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            Cv2.Dilate(edges, edges, kernel);
            Cv2.Erode(edges, edges, kernel);

            Cv2.FindContours(edges, out Point[][] contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 找到最大的四边形轮廓
            Point[] largestQuad = null;
            double maxArea = 0;

            foreach (var contour in contours)
            {
                double epsilon = 0.02 * Cv2.ArcLength(contour, true);
                Point[] approx = Cv2.ApproxPolyDP(contour, epsilon, true);

                if (approx.Length == 4)
                {
                    double area = Cv2.ContourArea(approx);
                    if (area > maxArea && area > image.Width * image.Height * 0.1) // 至少占图像的10%
                    {
                        maxArea = area;
                        largestQuad = approx;
                    }
                }
            }

            if (largestQuad == null)
            {
                Console.WriteLine("Warning: Could not find document boundary for perspective correction");
                return null;
            }

            // 按顺序排列四个角点：左上、右上、右下、左下
            Point[] orderedPoints = OrderPoints(largestQuad);

            // 计算目标矩形的尺寸
            double widthTop = orderedPoints[1].DistanceTo(orderedPoints[0]);
            double widthBottom = orderedPoints[2].DistanceTo(orderedPoints[3]);
            double heightLeft = orderedPoints[3].DistanceTo(orderedPoints[0]);
            double heightRight = orderedPoints[2].DistanceTo(orderedPoints[1]);

            int maxWidth = (int)Math.Max(widthTop, widthBottom);
            int maxHeight = (int)Math.Max(heightLeft, heightRight);

            // 定义目标点
            Point2f[] srcPoints = orderedPoints.Select(p => p.ToPoint2f()).ToArray();
            Point2f[] dstPoints = {
                new Point2f(0, 0),
                new Point2f(maxWidth - 1, 0),
                new Point2f(maxWidth - 1, maxHeight - 1),
                new Point2f(0, maxHeight - 1)
            };

            // 应用透视变换
            using Mat transform = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);
            Mat result = new Mat();
            Cv2.WarpPerspective(image, result, transform, new Size(maxWidth, maxHeight));

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in perspective correction: {ex.Message}");
            return null;
        }
    }

    private Point[] OrderPoints(Point[] points)
    {
        // 按 x+y 排序找到左上和右下
        var sortedBySum = points.OrderBy(p => p.X + p.Y).ToArray();
        Point topLeft = sortedBySum[0];
        Point bottomRight = sortedBySum[3];

        // 按 x-y 排序找到右上和左下
        var sortedByDiff = points.OrderBy(p => p.X - p.Y).ToArray();
        Point topRight = sortedByDiff[3];
        Point bottomLeft = sortedByDiff[0];

        return new Point[] { topLeft, topRight, bottomRight, bottomLeft };
    }

    private void EnhanceToScannedLook(Mat image)
    {
        // 改进的扫描增强算法，避免过度锐化
        using Mat gray = new Mat();
        if (image.Channels() == 3)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else
            image.CopyTo(gray);

        // 1. 自适应直方图均衡化（温和的对比度增强）
        using var clahe = Cv2.CreateCLAHE(1.5, new Size(8, 8)); // 降低clipLimit避免过度增强
        using Mat enhanced = new Mat();
        clahe.Apply(gray, enhanced);

        // 2. 双边滤波保持边缘同时去噪
        using Mat filtered = new Mat();
        Cv2.BilateralFilter(enhanced, filtered, 9, 75, 75);

        // 3. 温和的锐化（使用更小的kernel）
        using Mat kernel = new Mat(3, 3, MatType.CV_32F, new Scalar(0));
        // 手动设置kernel值
        kernel.Set<float>(0, 1, -0.25f);
        kernel.Set<float>(1, 0, -0.25f);
        kernel.Set<float>(1, 1, 2.0f);
        kernel.Set<float>(1, 2, -0.25f);
        kernel.Set<float>(2, 1, -0.25f);

        using Mat sharpened = new Mat();
        Cv2.Filter2D(filtered, sharpened, -1, kernel);

        // 4. 温和的对比度调整（移除复杂的伽马校正）
        using Mat final = new Mat();
        sharpened.ConvertTo(final, MatType.CV_8U, 1.2, 15); // 稍微增加对比度和亮度

        // 将结果复制回原图像
        if (image.Channels() == 3)
        {
            using Mat color = new Mat();
            Cv2.CvtColor(final, color, ColorConversionCodes.GRAY2BGR);
            color.CopyTo(image);
        }
        else
        {
            final.CopyTo(image);
        }
    }

    private Mat ExtractAndStitchAreas(Mat src, List<Point[]> scanAreas, Point[] scanAreasIndexPoint,
                                     int averageWidth, int averageHeight, Point[] rotateSymbolArea)
    {
        // 完全按照 Program.cs 的逻辑实现
        Point leftTopstandardPoint = GetClosestPoint(new Point(0, 0), scanAreasIndexPoint);
        List<Point> standardPoints = new List<Point>();

        for (int i = 0; i < (scanAreas.Count - 2) / 2; i++)
            standardPoints.Add(leftTopstandardPoint + new Point(averageWidth * i, 0));

        List<Mat> resultAreas = new List<Mat>();

        foreach (var item in standardPoints)
        {
            // Left Top
            Point leftTopIndexPoint = GetClosestPoint(item, scanAreasIndexPoint);
            Point[] leftTopArea = scanAreas[Array.IndexOf(scanAreasIndexPoint, leftTopIndexPoint)];
            Point leftTopPoint = leftTopArea.MinBy(p => p.X + p.Y);

            // Left Bottom 
            Point leftBottomIndexPoint = GetClosestPoint(item + new Point(0, averageHeight), scanAreasIndexPoint);
            Point[] leftBottomArea = scanAreas[Array.IndexOf(scanAreasIndexPoint, leftBottomIndexPoint)];
            Point leftBottomPoint = leftBottomArea.MinBy(p => p.X - p.Y);

            // Right Bottom
            Point rightBottomIndexPoint = GetClosestPoint(item + new Point(averageWidth, averageHeight), scanAreasIndexPoint);
            Point[] rightBottomArea = scanAreas[Array.IndexOf(scanAreasIndexPoint, rightBottomIndexPoint)];
            Point rightBottomPoint = rightBottomArea.MaxBy(p => p.X + p.Y);

            // Right Top
            Point rightTopIndexPoint = GetClosestPoint(item + new Point(averageWidth, 0), scanAreasIndexPoint);
            Point[] rightTopArea = scanAreas[Array.IndexOf(scanAreasIndexPoint, rightTopIndexPoint)];
            Point rightTopPoint = rightTopArea.MaxBy(p => p.X - p.Y);

            int left = Math.Max(leftTopPoint.X, leftBottomPoint.X);
            int right = Math.Max(rightTopPoint.X, rightBottomPoint.X);
            int top = Math.Max(leftTopPoint.Y, rightTopPoint.Y);
            int bottom = Math.Max(leftBottomPoint.Y, rightBottomPoint.Y);

            Rect rect = new Rect()
            {
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top
            };

            resultAreas.Add(new Mat(src, rect));
        }

        // 调整大小并拼接 - 与 Program.cs 完全一致
        var firstArea = resultAreas.First();
        foreach (var item in resultAreas.Skip(1))
            Cv2.Resize(item, item, new Size(firstArea.Width, firstArea.Height));

        Mat result = new Mat();
        Cv2.VConcat(resultAreas, result);

        // 应用旋转（如果需要）- 保持原程序的方向校正逻辑
        if (rotateSymbolArea != null && rotateSymbolArea[0].Y > averageHeight)
            Cv2.Rotate(result, result, RotateFlags.Rotate180);

        // 清理资源
        foreach (var area in resultAreas)
            area.Dispose();

        return result;
    }

    private static Point GetClosestPoint(Point point, Point[] points)
    {
        Point closestPoint = points[0];
        double minDistance = point.DistanceTo(closestPoint);

        foreach (var p in points)
        {
            double distance = point.DistanceTo(p);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = p;
            }
        }

        return closestPoint;
    }

    private static int GetAverageDistance(int[] coordinates, int minLimit = 50)
    {
        int totalDistance = 0;
        int count = 0;

        coordinates = coordinates.OrderBy(x => x).ToArray(); // 使用LINQ排序替代Array.Sort
        for (int i = 0; i < coordinates.Length - 1; i++)
        {
            if (coordinates[i + 1] - coordinates[i] < minLimit)
                continue;

            totalDistance += coordinates[i + 1] - coordinates[i];
            count++;
        }

        return count > 0 ? totalDistance / count : 0;
    }
}