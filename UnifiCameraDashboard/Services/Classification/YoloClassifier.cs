using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace UnifiCameraDashboard.Services.Classification;

public readonly record struct Detection(string Label, float Confidence);

public interface IYoloClassifier
{
    IReadOnlyList<Detection> Classify(byte[] jpegBytes);
}

/// <summary>
/// Runs YOLO11n (ONNX, CPU) against a saved event thumbnail. See docs/decisions or the S2 PR
/// description for why the confidence threshold is day/night-adaptive: night IR footage is a
/// genuine out-of-distribution gap for a COCO-trained model (correct class ranks top but at
/// ~9-15% confidence, well under a normal ~25-40% bar), measured directly against real camera
/// footage - not fixable by a bigger model, and a dedicated night model isn't a simple swap (no
/// standard pretrained IR/thermal COCO-equivalent exists). One model, two thresholds instead.
/// </summary>
public sealed class YoloClassifier : IYoloClassifier, IDisposable
{
    private const int InputSize = 640;
    private const float DayConfidenceThreshold = 0.25f;
    private const float NightConfidenceThreshold = 0.10f;
    private const float NmsIouThreshold = 0.45f;

    // Mean per-pixel |R-G|+|G-B|+|R-B|, sampled. IR-illuminated night frames from these cameras
    // measured exactly 0.00 (perfectly grayscale - R=G=B per pixel); real color daytime frames
    // measured 60+. Huge margin either side of this threshold.
    private const double GrayscaleColorDiffThreshold = 5.0;
    private const int ColorSampleStride = 7;

    private readonly InferenceSession _session;
    private readonly string _inputName;

    public YoloClassifier(string modelPath)
    {
        var options = new Microsoft.ML.OnnxRuntime.SessionOptions
        {
            // Capped so classification never competes hard for CPU with the rest of a shared
            // 8GB Pi (k3s system pods, other apps) - this runs single-consumer/one-at-a-time
            // anyway (see EventClassificationService), so there's no concurrent-request reason
            // to want more threads here.
            IntraOpNumThreads = 2,
            InterOpNumThreads = 1,
        };
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.First();
    }

    public static string ResolveModelPath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Tools", "yolo", "yolo11n.onnx");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        var devPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "yolo", "yolo11n.onnx");
        if (File.Exists(devPath))
        {
            return devPath;
        }

        throw new FileNotFoundException(
            "YOLO model not found. Expected at Tools/yolo/yolo11n.onnx - see Tools/yolo/README.md for how to obtain it locally; the Docker image downloads it at build time.",
            "yolo11n.onnx");
    }

    public IReadOnlyList<Detection> Classify(byte[] jpegBytes)
    {
        using var image = SKBitmap.Decode(jpegBytes)
            ?? throw new InvalidOperationException("Could not decode event thumbnail as an image.");
        var threshold = IsLikelyNightIr(image) ? NightConfidenceThreshold : DayConfidenceThreshold;

        var layout = YoloPostProcessing.ComputeLetterbox(image.Width, image.Height, InputSize);
        using var canvas = BuildLetterboxCanvas(image, layout);
        var input = ToChwTensor(canvas);

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, input) };
        using var results = _session.Run(inputs);

        var output = results.First().AsTensor<float>();
        var numPredictions = output.Dimensions[2];
        var flat = output.ToArray();

        var raw = YoloPostProcessing.DecodeDetections(flat, numPredictions, threshold);
        var kept = YoloPostProcessing.ApplyNms(raw, NmsIouThreshold);

        return kept
            .GroupBy(d => d.ClassId)
            .Select(g => new Detection(CocoLabels.Names[g.Key], g.Max(d => d.Confidence)))
            .OrderByDescending(d => d.Confidence)
            .ToList();
    }

    private static bool IsLikelyNightIr(SKBitmap image)
    {
        long diffSum = 0;
        var sampleCount = 0;

        for (var y = 0; y < image.Height; y += ColorSampleStride)
        {
            for (var x = 0; x < image.Width; x += ColorSampleStride)
            {
                var pixel = image.GetPixel(x, y);
                diffSum += Math.Abs(pixel.Red - pixel.Green) + Math.Abs(pixel.Green - pixel.Blue) + Math.Abs(pixel.Red - pixel.Blue);
                sampleCount++;
            }
        }

        return sampleCount > 0 && diffSum / (double)sampleCount < GrayscaleColorDiffThreshold;
    }

    private static SKBitmap BuildLetterboxCanvas(SKBitmap source, LetterboxLayout layout)
    {
        var info = new SKImageInfo(InputSize, InputSize, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var canvas = new SKBitmap(info);
        canvas.Erase(new SKColor(114, 114, 114));

        using var resized = source.Resize(new SKImageInfo(layout.NewWidth, layout.NewHeight, SKColorType.Rgba8888, SKAlphaType.Opaque), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("Failed to resize event thumbnail for classification.");

        using (var skCanvas = new SKCanvas(canvas))
        {
            skCanvas.DrawBitmap(resized, layout.PadX, layout.PadY, SKSamplingOptions.Default, paint: null);
        }

        return canvas;
    }

    private static DenseTensor<float> ToChwTensor(SKBitmap canvas)
    {
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
        var channelSize = InputSize * InputSize;
        var buffer = tensor.Buffer.Span;
        var pixels = canvas.Bytes; // SKColorType.Rgba8888 -> R,G,B,A per pixel, row-major

        for (var i = 0; i < channelSize; i++)
        {
            var offset = i * 4;
            buffer[i] = pixels[offset] / 255f;
            buffer[channelSize + i] = pixels[offset + 1] / 255f;
            buffer[(2 * channelSize) + i] = pixels[offset + 2] / 255f;
        }

        return tensor;
    }

    public void Dispose() => _session.Dispose();
}
