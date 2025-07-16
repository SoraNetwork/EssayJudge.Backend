using OpenCvSharp;

namespace SoraEssayJudge.Services;

/// <summary>
/// Helper extension methods for Point2f calculations.
/// </summary>
public static class PointExtensions
{
    /// <summary>
    /// Calculates the squared Euclidean distance between two points. Faster than DistanceTo if only comparing.
    /// </summary>
    public static double DistanceToSquared(this Point2f p1, Point2f p2)
    {
        return Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
    }
}

public class AnswerSheetProcessor
{
    // A3 paper dimensions (in pixels, based on 300 DPI), long side first
    private const int A3_LONG_SIDE = 3508;
    private const int A3_SHORT_SIDE = 2480;

    // Area range for filtering marker points
    private const double MIN_MARKER_AREA = 100;
    private const double MAX_MARKER_AREA = 5000;

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

    private List<Point> FindAllMarkerCenters(Mat image)
    {
        Mat gray = new Mat();
        if (image.Channels() == 3) Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else gray = image.Clone();

        var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 127, 255, ThresholdTypes.BinaryInv);
        
        Cv2.FindContours(thresh, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var markerCenters = new List<Point>();

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < MIN_MARKER_AREA || area > MAX_MARKER_AREA) continue;

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

    private Point2f[] FindFourCornerMarkers(List<Point> allMarkers)
    {
        if (allMarkers.Count < 4) return null;
        return Cv2.MinAreaRect(allMarkers).Points();
    }

    private List<Point> FindThreePointVerticalCluster(List<Point> markers, double maxHorizontalSpread = 150, double maxTotalHeight = 600)
    {
        if (markers.Count < 3) return null;
        
        var clusters = new List<List<Point>>();
        for (int i = 0; i < markers.Count - 2; i++)
        {
            for (int j = i + 1; j < markers.Count - 1; j++)
            {
                for (int k = j + 1; k < markers.Count; k++)
                {
                    var cluster = new List<Point> { markers[i], markers[j], markers[k] };
                    var xCoords = cluster.Select(p => p.X).OrderBy(x => x).ToList();
                    var yCoords = cluster.Select(p => p.Y).OrderBy(y => y).ToList();

                    double xSpread = xCoords.Last() - xCoords.First();
                    double ySpread = yCoords.Last() - yCoords.First();

                    if (xSpread <= maxHorizontalSpread && ySpread <= maxTotalHeight)
                    {
                        clusters.Add(cluster);
                    }
                }
            }
        }

        return clusters.OrderBy(c => c[0].X).FirstOrDefault();
    }

    private List<Point> FindThreePointHorizontalCluster(List<Point> markers, double maxVerticalSpread = 150, double maxTotalWidth = 600)
    {
        if (markers.Count < 3) return null;

        var clusters = new List<List<Point>>();
        for (int i = 0; i < markers.Count - 2; i++)
        {
            for (int j = i + 1; j < markers.Count - 1; j++)
            {
                for (int k = j + 1; k < markers.Count; k++)
                {
                    var cluster = new List<Point> { markers[i], markers[j], markers[k] };
                    var xCoords = cluster.Select(p => p.X).OrderBy(x => x).ToList();
                    var yCoords = cluster.Select(p => p.Y).OrderBy(y => y).ToList();

                    double xSpread = xCoords.Last() - xCoords.First();
                    double ySpread = yCoords.Last() - yCoords.First();

                    if (ySpread <= maxVerticalSpread && xSpread <= maxTotalWidth)
                    {
                        clusters.Add(cluster);
                    }
                }
            }
        }

        return clusters.OrderBy(c => c[0].Y).FirstOrDefault();
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

        var allMarkers = FindAllMarkerCenters(image);
        if (allMarkers.Count < 4)
        {
            Console.WriteLine("Error: Fewer than 4 marker points found.");
            return null;
        }

        var photoCornerPoints = FindFourCornerMarkers(allMarkers);
        if (photoCornerPoints == null || photoCornerPoints.Length < 4)
        {
            Console.WriteLine("Error: Could not identify 4 corner points.");
            return null;
        }

        List<Point> orientationCluster = (image.Height > image.Width)
            ? FindThreePointHorizontalCluster(allMarkers)
            : FindThreePointVerticalCluster(allMarkers);

        Point2f[] sourceCorners;

        if (orientationCluster != null)
        {
            var clusterCentroid = new Point2f(
                (float)orientationCluster.Average(p => p.X),
                (float)orientationCluster.Average(p => p.Y));

            var orderedByDist = photoCornerPoints.OrderBy(p => p.DistanceToSquared(clusterCentroid)).ToList();
            var rightCorner1 = orderedByDist[0];
            var rightCorner2 = orderedByDist[1];
            var leftCorner1 = orderedByDist[2];
            var leftCorner2 = orderedByDist[3];

            var quadCenter = new Point2f(
                photoCornerPoints.Average(p => p.X),
                photoCornerPoints.Average(p => p.Y));
            var rightMidpoint = new Point2f(
                (rightCorner1.X + rightCorner2.X) / 2,
                (rightCorner1.Y + rightCorner2.Y) / 2);
            
            var vecRight = new Point2f(
                rightMidpoint.X - quadCenter.X,
                rightMidpoint.Y - quadCenter.Y);
            var vecEdge1 = new Point2f(
                rightCorner2.X - rightCorner1.X,
                rightCorner2.Y - rightCorner1.Y);

            Point2f topRight, bottomRight;
            if ((vecRight.X * vecEdge1.Y - vecRight.Y * vecEdge1.X) > 0)
            {
                topRight = rightCorner1;
                bottomRight = rightCorner2;
            }
            else
            {
                topRight = rightCorner2;
                bottomRight = rightCorner1;
            }

            var vecRightEdge = new Point2f(
                bottomRight.X - topRight.X,
                bottomRight.Y - topRight.Y);
            var vecLeftEdge1 = new Point2f(
                leftCorner2.X - leftCorner1.X,
                leftCorner2.Y - leftCorner1.Y);

            Point2f topLeft, bottomLeft;
            if ((vecRightEdge.X * vecLeftEdge1.X + vecRightEdge.Y * vecLeftEdge1.Y) > 0)
            {
                topLeft = leftCorner1;
                bottomLeft = leftCorner2;
            }
            else
            {
                topLeft = leftCorner2;
                bottomLeft = leftCorner1;
            }

            sourceCorners = new[] { topLeft, topRight, bottomRight, bottomLeft };
        }
        else
        {
            sourceCorners = OrderCornersByDefault(photoCornerPoints);
        }

        var destinationPoints = new Point2f[] {
            new Point2f(0, 0),
            new Point2f(A3_LONG_SIDE - 1, 0),
            new Point2f(A3_LONG_SIDE - 1, A3_SHORT_SIDE - 1),
            new Point2f(0, A3_SHORT_SIDE - 1)
        };

        var perspectiveMatrix = Cv2.GetPerspectiveTransform(sourceCorners, destinationPoints);
        var finalImage = new Mat();
        Cv2.WarpPerspective(image, finalImage, perspectiveMatrix, new Size(A3_LONG_SIDE, A3_SHORT_SIDE));
        
        return EnhanceToScannedLook(finalImage);
    }
}
