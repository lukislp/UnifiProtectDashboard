namespace UnifiCameraDashboard.Services.Classification;

/// <summary>
/// A single decoded box before non-max suppression, in the model's own 640x640 letterboxed
/// pixel space - never mapped back to original image coordinates, since only the label and
/// confidence are ever used (the app shows labels, not bounding boxes).
/// </summary>
public readonly record struct RawDetection(float CenterX, float CenterY, float Width, float Height, int ClassId, float Confidence);

public readonly record struct LetterboxLayout(int NewWidth, int NewHeight, int PadX, int PadY, float Scale);

/// <summary>
/// Pure decode/NMS/letterbox math for YOLO11's raw ONNX output, kept free of ONNX Runtime and
/// image-decoding dependencies so it's directly unit-testable against synthetic data.
/// </summary>
public static class YoloPostProcessing
{
    private const int NumBoxCoords = 4;
    private const int NumClasses = 80;

    /// <summary>
    /// Decodes a raw [84, numPredictions] output tensor (row-major, channel-first - box coords
    /// 0-3 then 80 class scores, exactly as YOLO11/YOLOv8's ONNX export produces it) into
    /// per-box best-class detections above <paramref name="confidenceThreshold"/>.
    /// </summary>
    public static List<RawDetection> DecodeDetections(ReadOnlySpan<float> output, int numPredictions, float confidenceThreshold)
    {
        var results = new List<RawDetection>();

        for (var i = 0; i < numPredictions; i++)
        {
            var bestConfidence = 0f;
            var bestClass = -1;

            for (var c = 0; c < NumClasses; c++)
            {
                var confidence = output[(NumBoxCoords + c) * numPredictions + i];
                if (confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestClass = c;
                }
            }

            if (bestClass < 0 || bestConfidence < confidenceThreshold)
            {
                continue;
            }

            var centerX = output[i];
            var centerY = output[numPredictions + i];
            var width = output[(2 * numPredictions) + i];
            var height = output[(3 * numPredictions) + i];

            results.Add(new RawDetection(centerX, centerY, width, height, bestClass, bestConfidence));
        }

        return results;
    }

    /// <summary>Per-class greedy non-max suppression - overlapping boxes of different classes (e.g. a person on a bicycle) are never suppressed against each other.</summary>
    public static List<RawDetection> ApplyNms(List<RawDetection> detections, float iouThreshold)
    {
        var kept = new List<RawDetection>();

        foreach (var group in detections.GroupBy(d => d.ClassId))
        {
            var sorted = group.OrderByDescending(d => d.Confidence).ToList();
            var suppressed = new bool[sorted.Count];

            for (var i = 0; i < sorted.Count; i++)
            {
                if (suppressed[i])
                {
                    continue;
                }
                kept.Add(sorted[i]);

                for (var j = i + 1; j < sorted.Count; j++)
                {
                    if (!suppressed[j] && ComputeIou(sorted[i], sorted[j]) > iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }
        }

        return kept;
    }

    public static float ComputeIou(RawDetection a, RawDetection b)
    {
        var (ax1, ay1, ax2, ay2) = ToCorners(a);
        var (bx1, by1, bx2, by2) = ToCorners(b);

        var interX = Math.Max(0f, Math.Min(ax2, bx2) - Math.Max(ax1, bx1));
        var interY = Math.Max(0f, Math.Min(ay2, by2) - Math.Max(ay1, by1));
        var interArea = interX * interY;

        var areaA = (ax2 - ax1) * (ay2 - ay1);
        var areaB = (bx2 - bx1) * (by2 - by1);
        var unionArea = areaA + areaB - interArea;

        return unionArea <= 0f ? 0f : interArea / unionArea;
    }

    private static (float X1, float Y1, float X2, float Y2) ToCorners(RawDetection d)
        => (d.CenterX - (d.Width / 2), d.CenterY - (d.Height / 2), d.CenterX + (d.Width / 2), d.CenterY + (d.Height / 2));

    /// <summary>Scale-preserving resize-and-pad layout: how big the resized image is and where it sits inside a <paramref name="targetSize"/>-square canvas.</summary>
    public static LetterboxLayout ComputeLetterbox(int originalWidth, int originalHeight, int targetSize)
    {
        var scale = Math.Min((float)targetSize / originalWidth, (float)targetSize / originalHeight);
        var newWidth = (int)Math.Round(originalWidth * scale);
        var newHeight = (int)Math.Round(originalHeight * scale);
        var padX = (targetSize - newWidth) / 2;
        var padY = (targetSize - newHeight) / 2;

        return new LetterboxLayout(newWidth, newHeight, padX, padY, scale);
    }
}
