using OpenCvSharp;
using System.Collections.Concurrent;

namespace SoraEssayJudge.Services;

public class AnswerSheetProcessor
{
    private const int A3_LONG_SIDE = 3508;
    private const int A3_SHORT_SIDE = 2480;
    private const double MIN_MARKER_AREA = 100;
    private const double MAX_MARKER_AREA = 5000;
    
    // 优化：添加缩放比例以提高处理速度
    private const double PROCESSING_SCALE = 0.25; // 处理时缩放到原图的1/4

    public Mat EnhanceToScannedLook(Mat image)
    {
        Mat gray = new Mat();
        if (image.Channels() == 3) Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else gray = image.Clone();
        
        var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
        clahe.Apply(gray, gray);
        
        var sharpened = new Mat();
        var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(0, 0), 3);
        Cv2.AddWeighted(gray, 1.5, blurred, -0.5, 0, sharpened);
        
        var result = new Mat();
        Cv2.AdaptiveThreshold(sharpened, result, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 25, 10);
        
        return result;
    }

    /// <summary>
    /// 优化：使用缩放后的图像进行快速方向检测
    /// </summary>
    private Mat AutoRotateImage(Mat image)
    {
        if (image.Height > image.Width)
        {
            Mat rotated = new Mat();
            Cv2.Rotate(image, rotated, RotateFlags.Rotate90Clockwise);
            return rotated;
        }
        return image.Clone();
    }

    /// <summary>
    /// 优化：在缩放图像上进行方向检测，然后应用到原图
    /// </summary>
    private Mat CorrectOrientationByLMarker(Mat image)
    {
        // 缩放图像以提高处理速度
        Mat scaledImage = new Mat();
        Cv2.Resize(image, scaledImage, new Size(0, 0), PROCESSING_SCALE, PROCESSING_SCALE);
        
        var allMarkers = FindAllMarkerCenters(scaledImage);
        if (allMarkers.Count < 3) return image.Clone();

        var lMarker = FindLShapeMarkerOptimized(allMarkers);
        if (lMarker == null) return image.Clone();

        bool needsRotation = IsLMarkerInWrongPosition(lMarker, scaledImage.Size());
        
        if (needsRotation)
        {
            Mat rotated = new Mat();
            Cv2.Rotate(image, rotated, RotateFlags.Rotate180);
            return rotated;
        }

        return image.Clone();
    }

    /// <summary>
    /// 优化：使用更高效的算法寻找L形标记
    /// </summary>
    private List<Point> FindLShapeMarkerOptimized(List<Point> markers)
    {
        if (markers.Count < 3) return null;

        // 预先筛选候选点，减少计算量
        var candidateMarkers = markers.Take(Math.Min(50, markers.Count)).ToList(); // 限制候选点数量
        
        // 使用空间索引优化搜索
        var clusters = new List<List<Point>>();
        const double maxHorizontalSpread = 150 * PROCESSING_SCALE;
        const double maxTotalHeight = 600 * PROCESSING_SCALE;
        
        // 优化：先按位置预分组，减少比较次数
        var groupedByRegion = GroupMarkersByRegion(candidateMarkers);
        
        foreach (var region in groupedByRegion)
        {
            if (region.Count < 3) continue;
            
            var bestCluster = FindBestVerticalCluster(region, maxHorizontalSpread, maxTotalHeight);
            if (bestCluster != null)
            {
                clusters.Add(bestCluster);
            }
        }

        return clusters.OrderBy(c => c[0].X).FirstOrDefault();
    }

    /// <summary>
    /// 优化：将标记点按区域分组
    /// </summary>
    private List<List<Point>> GroupMarkersByRegion(List<Point> markers)
    {
        const int regionSize = 200;
        var regionGroups = new Dictionary<string, List<Point>>();
        
        foreach (var marker in markers)
        {
            int regionX = (int)(marker.X / regionSize);
            int regionY = (int)(marker.Y / regionSize);
            string regionKey = $"{regionX}_{regionY}";
            
            if (!regionGroups.ContainsKey(regionKey))
                regionGroups[regionKey] = new List<Point>();
            
            regionGroups[regionKey].Add(marker);
        }
        
        return regionGroups.Values.Where(g => g.Count >= 3).ToList();
    }

    /// <summary>
    /// 优化：在较小的候选集合中寻找最佳垂直聚类
    /// </summary>
    private List<Point> FindBestVerticalCluster(List<Point> candidates, double maxHorizontalSpread, double maxTotalHeight)
    {
        if (candidates.Count < 3) return null;
        
        // 限制搜索范围以提高性能
        int maxCandidates = Math.Min(20, candidates.Count);
        var limitedCandidates = candidates.Take(maxCandidates).ToList();
        
        List<Point> bestCluster = null;
        double bestScore = double.MaxValue;
        
        for (int i = 0; i < limitedCandidates.Count - 2; i++)
        {
            for (int j = i + 1; j < limitedCandidates.Count - 1; j++)
            {
                for (int k = j + 1; k < limitedCandidates.Count; k++)
                {
                    var cluster = new List<Point> { limitedCandidates[i], limitedCandidates[j], limitedCandidates[k] };
                    
                    if (IsValidVerticalCluster(cluster, maxHorizontalSpread, maxTotalHeight))
                    {
                        double score = CalculateClusterScore(cluster);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestCluster = cluster;
                        }
                    }
                }
            }
        }
        
        return bestCluster;
    }

    /// <summary>
    /// 优化：快速验证聚类是否有效
    /// </summary>
    private bool IsValidVerticalCluster(List<Point> cluster, double maxHorizontalSpread, double maxTotalHeight)
    {
        var xCoords = cluster.Select(p => p.X).ToList();
        var yCoords = cluster.Select(p => p.Y).ToList();
        
        double xSpread = xCoords.Max() - xCoords.Min();
        double ySpread = yCoords.Max() - yCoords.Min();
        
        return xSpread <= maxHorizontalSpread && ySpread <= maxTotalHeight;
    }

    /// <summary>
    /// 计算聚类质量分数
    /// </summary>
    private double CalculateClusterScore(List<Point> cluster)
    {
        // 计算紧密度分数（越小越好）
        var xCoords = cluster.Select(p => p.X).ToList();
        var yCoords = cluster.Select(p => p.Y).ToList();
        
        double xSpread = xCoords.Max() - xCoords.Min();
        double ySpread = yCoords.Max() - yCoords.Min();
        
        return xSpread + ySpread * 0.1; // 偏向垂直排列
    }

    private bool IsLMarkerInWrongPosition(List<Point> lMarker, Size imageSize)
    {
        if (lMarker == null || lMarker.Count != 3) return false;

        var centerX = lMarker.Average(p => p.X);
        var centerY = lMarker.Average(p => p.Y);

        bool isInRightHalf = centerX > imageSize.Width * 0.5;
        bool isInBottomHalf = centerY > imageSize.Height * 0.5;

        return !(isInRightHalf && isInBottomHalf);
    }

    /// <summary>
    /// 优化：缓存标记点检测结果
    /// </summary>
    private List<Point> FindAllMarkerCenters(Mat image)
    {
        Mat gray = new Mat();
        if (image.Channels() == 3) Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else gray = image.Clone();

        // 优化：使用更高效的模糊和阈值处理
        var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0); // 减小核大小
        var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu); // 使用Otsu自动阈值
        
        Cv2.FindContours(thresh, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var markerCenters = new List<Point>();

        // 优化：预先计算面积范围
        double minArea = MIN_MARKER_AREA * PROCESSING_SCALE * PROCESSING_SCALE;
        double maxArea = MAX_MARKER_AREA * PROCESSING_SCALE * PROCESSING_SCALE;

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < minArea || area > maxArea) continue;

            var M = Cv2.Moments(contour);
            if (M.M00 != 0)
            {
                int centerX = (int)(M.M10 / M.M00);
                int centerY = (int)(M.M01 / M.M00);
                markerCenters.Add(new Point(centerX, centerY));
            }
        }
        return markerCenters;
    }

    /// <summary>
    /// 优化：在原图上进行精确的四角点检测
    /// </summary>
    private Point2f[] FindFourCornerMarkers(Mat originalImage)
    {
        // 在原图上进行精确检测
        var allMarkers = FindAllMarkerCenters(originalImage);
        if (allMarkers.Count < 4) return null;
        
        return Cv2.MinAreaRect(allMarkers).Points();
    }

    private Point2f[] OrderCornersByDefault(Point2f[] corners)
    {
        var sortedByY = corners.OrderBy(p => p.Y).ToArray();
        var topPoints = new[] { sortedByY[0], sortedByY[1] }.OrderBy(p => p.X).ToArray();
        var bottomPoints = new[] { sortedByY[2], sortedByY[3] }.OrderByDescending(p => p.X).ToArray();
        return new[] { topPoints[0], topPoints[1], bottomPoints[0], bottomPoints[1] };
    }

    public Mat Process(Mat image)
    {
        if (image == null || image.Empty()) return null;

        Console.WriteLine($"Processing image: {image.Width}x{image.Height}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 第一步：自动旋转竖直图像
        Mat orientedImage = AutoRotateImage(image);
        Console.WriteLine($"Auto rotation completed in {stopwatch.ElapsedMilliseconds}ms");

        // 第二步：方向纠正（在缩放图像上进行）
        Mat correctedImage = CorrectOrientationByLMarker(orientedImage);
        Console.WriteLine($"Orientation correction completed in {stopwatch.ElapsedMilliseconds}ms");

        // 第三步：四角点检测（在原图上进行以保证精度）
        var photoCornerPoints = FindFourCornerMarkers(correctedImage);
        if (photoCornerPoints == null || photoCornerPoints.Length < 4)
        {
            Console.WriteLine("Error: Could not identify 4 corner points.");
            return null;
        }

        Console.WriteLine($"Corner detection completed in {stopwatch.ElapsedMilliseconds}ms");

        // 使用默认排序方式
        Point2f[] sourceCorners = OrderCornersByDefault(photoCornerPoints);

        var destinationPoints = new Point2f[] {
            new Point2f(0, 0),
            new Point2f(A3_LONG_SIDE - 1, 0),
            new Point2f(A3_LONG_SIDE - 1, A3_SHORT_SIDE - 1),
            new Point2f(0, A3_SHORT_SIDE - 1)
        };

        var perspectiveMatrix = Cv2.GetPerspectiveTransform(sourceCorners, destinationPoints);
        var finalImage = new Mat();
        Cv2.WarpPerspective(correctedImage, finalImage, perspectiveMatrix, new Size(A3_LONG_SIDE, A3_SHORT_SIDE));
        
        Console.WriteLine($"Perspective transformation completed in {stopwatch.ElapsedMilliseconds}ms");

        var result = EnhanceToScannedLook(finalImage);
        Console.WriteLine($"Total processing time: {stopwatch.ElapsedMilliseconds}ms");
        
        return result;
    }
}