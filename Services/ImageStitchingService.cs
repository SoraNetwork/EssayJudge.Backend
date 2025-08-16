using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SoraEssayJudge.Services
{
    public class ImageStitchingService : IImageStitchingService
    {
        private readonly ILogger<ImageStitchingService> _logger;

        public ImageStitchingService(ILogger<ImageStitchingService> logger)
        {
            _logger = logger;
        }

        public async Task<string> StitchImagesAsync(IEnumerable<Stream> imageStreams, string uploadPath, int spacing = 20)
        {
            if (imageStreams == null || !imageStreams.Any())
            {
                throw new ArgumentException("No image streams provided.", nameof(imageStreams));
            }

            var processedMats = new List<Mat>();
            try
            {
                int imageIndex = 0;
                foreach (var stream in imageStreams)
                {
                    string fileId = $"img_{imageIndex++}";
                    stream.Position = 0;

                    byte[] imageData;
                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        imageData = memoryStream.ToArray();
                    }

                    using var originalMat = Mat.ImDecode(imageData, ImreadModes.Color);
                    if (originalMat.Empty())
                    {
                        _logger.LogWarning("Could not decode image stream {ImageIndex}.", imageIndex);
                        continue;
                    }

                    _logger.LogInformation("Processing image {FileId} - Original size: {Width}x{Height}",
                        fileId, originalMat.Width, originalMat.Height);

                    // Step 1: Try to extract and correct the paper from the image
                    Mat correctedPaper = ExtractAndCorrectPaper(originalMat);

                    Mat imageToProcess;
                    if (correctedPaper == null || correctedPaper.Empty())
                    {
                        _logger.LogInformation("Paper detection failed for image {FileId}. Using enhanced fallback processing.", fileId);
                        imageToProcess = ProcessImageFallback(originalMat);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully extracted paper from image {FileId}", fileId);
                        imageToProcess = correctedPaper;
                    }

                    if (imageToProcess == null || imageToProcess.Empty())
                    {
                        _logger.LogWarning("Failed to process image {FileId}. Skipping.", fileId);
                        continue;
                    }

                    // Step 2: Enhanced image processing for better OCR
                    Mat enhancedMat = EnhanceImageForOCR(imageToProcess);

                    processedMats.Add(enhancedMat);

                    // Clean up
                    if (imageToProcess != correctedPaper)
                    {
                        imageToProcess?.Dispose();
                    }
                    correctedPaper?.Dispose();
                }

                if (!processedMats.Any())
                {
                    throw new InvalidOperationException("No images could be processed successfully.");
                }

                _logger.LogInformation("Successfully processed {Count} images. Starting stitching...", processedMats.Count);

                // Step 3: Normalize heights and stitch
                var stitchedImage = StitchProcessedImages(processedMats, spacing);

                // Step 4: Save the final image as a compressed WebP
                string stitchedImageName = $"{Guid.NewGuid()}_stitched.webp";
                string stitchedImagePath = Path.Combine(uploadPath, stitchedImageName);

                var parameters = new ImageEncodingParam(ImwriteFlags.WebPQuality, 90);
                Cv2.ImWrite(stitchedImagePath, stitchedImage, parameters);
                _logger.LogInformation("Successfully stitched and compressed {ImageCount} images into {StitchedImagePath}",
                    processedMats.Count, stitchedImagePath);

                stitchedImage.Dispose();
                return stitchedImageName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stitch images with OpenCV.");
                throw;
            }
            finally
            {
                processedMats.ForEach(m => m?.Dispose());
            }
        }

        private Mat ExtractAndCorrectPaper(Mat source)
        {
            try
            {
                // Create a copy to work with
                using var workingImage = source.Clone();

                // Resize image if too large for better processing speed and accuracy
                double scale = 1.0;
                if (workingImage.Width > 1500 || workingImage.Height > 1500)
                {
                    scale = Math.Min(1500.0 / workingImage.Width, 1500.0 / workingImage.Height);
                    var newSize = new Size((int)(workingImage.Width * scale), (int)(workingImage.Height * scale));
                    Cv2.Resize(workingImage, workingImage, newSize);
                }

                using var gray = new Mat();
                Cv2.CvtColor(workingImage, gray, ColorConversionCodes.BGR2GRAY);

                // Enhanced preprocessing for better edge detection
                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

                // Try multiple edge detection approaches
                using var edges = new Mat();

                // Method 1: Standard Canny
                Cv2.Canny(blurred, edges, 30, 80);

                // Method 2: If first attempt doesn't find good contours, try different parameters
                Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(edges, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (contours.Length == 0 || !contours.Any(c => Cv2.ContourArea(c) > (gray.Width * gray.Height * 0.1)))
                {
                    // Try morphological operations to enhance edges
                    using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
                    using var morph = new Mat();
                    Cv2.MorphologyEx(blurred, morph, MorphTypes.Gradient, kernel);
                    Cv2.Canny(morph, edges, 50, 150);

                    Cv2.FindContours(edges, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                }

                if (contours.Length == 0)
                {
                    return null;
                }

                // Find the best paper-like contour
                var paperContour = FindBestPaperContour(contours, gray.Size());

                if (paperContour == null)
                {
                    return null;
                }

                // Try to approximate to quadrilateral
                var perimeter = Cv2.ArcLength(paperContour, true);
                var approx = Cv2.ApproxPolyDP(paperContour, 0.015 * perimeter, true);

                Point[] quadPoints = null;

                if (approx.Length == 4)
                {
                    quadPoints = approx.Select(p => new Point(p.X, p.Y)).ToArray();
                }
                else if (approx.Length > 4)
                {
                    // Try to find the best 4 corners from the approximation
                    quadPoints = FindBestQuadrilateral(approx.Select(p => new Point(p.X, p.Y)).ToArray());
                }

                if (quadPoints != null && quadPoints.Length == 4)
                {
                    // Perform perspective correction
                    var correctedImage = PerformPerspectiveCorrection(source, quadPoints, scale);
                    return correctedImage;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractAndCorrectPaper");
                return null;
            }
        }

        private Point[] FindBestPaperContour(Point[][] contours, Size imageSize)
        {
            var imageArea = imageSize.Width * imageSize.Height;

            // Filter contours by area and aspect ratio
            var candidates = contours
                .Where(c =>
                {
                    var area = Cv2.ContourArea(c);
                    var boundingRect = Cv2.BoundingRect(c);
                    var aspectRatio = (double)boundingRect.Width / boundingRect.Height;

                    return area > imageArea * 0.1 && // At least 10% of image
                           area < imageArea * 0.95 && // Less than 95% of image
                           aspectRatio > 0.3 && aspectRatio < 3.0; // Reasonable aspect ratio
                })
                .OrderByDescending(c => Cv2.ContourArea(c))
                .ToArray();

            return candidates.FirstOrDefault();
        }

        private Point[] FindBestQuadrilateral(Point[] points)
        {
            if (points.Length < 4) return null;

            try
            {
                // Find the convex hull first
                var hull = Cv2.ConvexHull(points);
                if (hull.Length < 4) return null;

                // Find the 4 corner points (most extreme points)
                var topLeft = hull.OrderBy(p => p.X + p.Y).First();
                var topRight = hull.OrderBy(p => p.Y - p.X).First();
                var bottomRight = hull.OrderByDescending(p => p.X + p.Y).First();
                var bottomLeft = hull.OrderByDescending(p => p.Y - p.X).First();

                return new[] { topLeft, topRight, bottomRight, bottomLeft };
            }
            catch
            {
                return null;
            }
        }

        private Mat PerformPerspectiveCorrection(Mat source, Point[] quadPoints, double scale)
        {
            try
            {
                // Adjust points back to original scale if we resized the image for detection
                var adjustedPoints = quadPoints.Select(p => new Point(
                    (int)(p.X / scale),
                    (int)(p.Y / scale)
                )).ToArray();

                // Order the points: top-left, top-right, bottom-right, bottom-left
                var orderedPoints = new Point[4];
                var sum = adjustedPoints.Select(p => p.X + p.Y).ToArray();
                orderedPoints[0] = adjustedPoints[Array.IndexOf(sum, sum.Min())]; // Top-left
                orderedPoints[2] = adjustedPoints[Array.IndexOf(sum, sum.Max())]; // Bottom-right

                var diff = adjustedPoints.Select(p => p.Y - p.X).ToArray();
                orderedPoints[1] = adjustedPoints[Array.IndexOf(diff, diff.Min())]; // Top-right
                orderedPoints[3] = adjustedPoints[Array.IndexOf(diff, diff.Max())]; // Bottom-left

                // Calculate dimensions
                double widthA = Point.Distance(orderedPoints[2], orderedPoints[3]);
                double widthB = Point.Distance(orderedPoints[1], orderedPoints[0]);
                int maxWidth = (int)Math.Max(widthA, widthB);

                double heightA = Point.Distance(orderedPoints[1], orderedPoints[2]);
                double heightB = Point.Distance(orderedPoints[0], orderedPoints[3]);
                int maxHeight = (int)Math.Max(heightA, heightB);

                // Ensure reasonable dimensions
                if (maxWidth < 100 || maxHeight < 100)
                {
                    return null;
                }

                // Prefer portrait orientation for documents
                if (maxWidth > maxHeight)
                {
                    (maxWidth, maxHeight) = (maxHeight, maxWidth);
                }

                Point2f[] srcPts = orderedPoints.Select(p => new Point2f(p.X, p.Y)).ToArray();
                Point2f[] dstPts = new Point2f[]
                {
                    new Point2f(0, 0),
                    new Point2f(maxWidth - 1, 0),
                    new Point2f(maxWidth - 1, maxHeight - 1),
                    new Point2f(0, maxHeight - 1)
                };

                using var matrix = Cv2.GetPerspectiveTransform(srcPts, dstPts);
                var result = new Mat();
                Cv2.WarpPerspective(source, result, matrix, new Size(maxWidth, maxHeight));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in perspective correction");
                return null;
            }
        }

        private Mat ProcessImageFallback(Mat originalMat)
        {
            try
            {
                var result = originalMat.Clone();

                // Auto-rotate based on aspect ratio
                if (ShouldRotateImage(result))
                {
                    var rotated = new Mat();
                    Cv2.Rotate(result, rotated, RotateFlags.Rotate90Clockwise);
                    result.Dispose();
                    result = rotated;
                }

                // Apply basic geometric corrections
                result = ApplyBasicCorrections(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback processing");
                return originalMat.Clone(); // Return original as last resort
            }
        }

        private bool ShouldRotateImage(Mat image)
        {
            // Rotate if image is significantly wider than tall (landscape -> portrait)
            double aspectRatio = (double)image.Width / image.Height;
            return aspectRatio > 1.3;
        }

        private Mat ApplyBasicCorrections(Mat image)
        {
            try
            {
                var result = image.Clone();

                // Apply slight sharpening
                using var kernel = new Mat(3, 3, MatType.CV_32F);
                float[] kernelData = { 0, -1, 0, -1, 5, -1, 0, -1, 0 };
                kernel.SetArray(kernelData);

                var sharpened = new Mat();
                Cv2.Filter2D(result, sharpened, -1, kernel);
                result.Dispose();

                return sharpened;
            }
            catch
            {
                return image.Clone();
            }
        }

        private Mat EnhanceImageForOCR(Mat source)
        {
            try
            {
                // Convert to grayscale
                using var gray = new Mat();
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

                // Enhanced processing for better OCR results
                var enhanced = new Mat();

                // Apply slight blur to reduce noise
                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);

                // Apply adaptive threshold with optimized parameters
                Cv2.AdaptiveThreshold(blurred, enhanced, 255, AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary, 21, 8);

                // Apply morphological operations to clean up text
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
                using var cleaned = new Mat();
                Cv2.MorphologyEx(enhanced, cleaned, MorphTypes.Close, kernel);

                var final = cleaned.Clone();
                return final;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enhancing image for OCR");
                // Fallback to simple grayscale conversion
                var fallback = new Mat();
                Cv2.CvtColor(source, fallback, ColorConversionCodes.BGR2GRAY);
                return fallback;
            }
        }

        private Mat StitchProcessedImages(List<Mat> processedMats, int spacing)
        {
            // Normalize heights
            int maxHeight = processedMats.Max(m => m.Height);
            var normalizedMats = new List<Mat>();

            foreach (var mat in processedMats)
            {
                if (mat.Height < maxHeight)
                {
                    var paddedMat = new Mat(maxHeight, mat.Width, mat.Type(), Scalar.All(255));
                    mat.CopyTo(new Mat(paddedMat, new Rect(0, 0, mat.Width, mat.Height)));
                    normalizedMats.Add(paddedMat);
                }
                else
                {
                    normalizedMats.Add(mat.Clone());
                }
            }

            // Calculate total width
            int totalWidth = normalizedMats.Sum(m => m.Width) + (spacing * (normalizedMats.Count - 1));
            var finalImage = new Mat(maxHeight, totalWidth, normalizedMats.First().Type(), Scalar.All(255));

            // Stitch images horizontally
            int currentX = 0;
            foreach (var mat in normalizedMats)
            {
                mat.CopyTo(new Mat(finalImage, new Rect(currentX, 0, mat.Width, mat.Height)));
                currentX += mat.Width + spacing;
            }

            // Clean up normalized mats (except the cloned ones that are in processedMats)
            for (int i = 0; i < normalizedMats.Count; i++)
            {
                if (normalizedMats[i] != processedMats[i])
                {
                    normalizedMats[i].Dispose();
                }
            }

            return finalImage;
        }
    }
}