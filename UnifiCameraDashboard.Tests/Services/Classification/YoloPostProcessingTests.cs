using UnifiCameraDashboard.Services.Classification;

namespace UnifiCameraDashboard.Tests.Services.Classification;

public class YoloPostProcessingTests
{
    [Theory]
    [InlineData(640, 360, 640, 640, 360, 0, 140)] // wide image: full width, letterboxed top/bottom
    [InlineData(360, 640, 640, 360, 640, 140, 0)] // tall image: full height, letterboxed left/right
    [InlineData(640, 640, 640, 640, 640, 0, 0)]   // already square at target: no padding
    [InlineData(100, 100, 640, 640, 640, 0, 0)]   // small square: upscaled to fill exactly
    public void ComputeLetterbox_ProducesExpectedLayout(int origW, int origH, int target, int expectedW, int expectedH, int expectedPadX, int expectedPadY)
    {
        var layout = YoloPostProcessing.ComputeLetterbox(origW, origH, target);

        Assert.Equal(expectedW, layout.NewWidth);
        Assert.Equal(expectedH, layout.NewHeight);
        Assert.Equal(expectedPadX, layout.PadX);
        Assert.Equal(expectedPadY, layout.PadY);
    }

    [Fact]
    public void DecodeDetections_PicksBestClassPerBoxAboveThreshold()
    {
        const int numPredictions = 2;
        // Layout: [4 box coords + 80 class scores] x numPredictions, row-major channel-first.
        var output = new float[84 * numPredictions];

        // Box 0: cx=100,cy=50,w=20,h=30, class 2 ("car") at 0.9, everything else low.
        output[(0 * numPredictions) + 0] = 100;
        output[(1 * numPredictions) + 0] = 50;
        output[(2 * numPredictions) + 0] = 20;
        output[(3 * numPredictions) + 0] = 30;
        output[(4 + 2) * numPredictions + 0] = 0.9f;
        output[(4 + 0) * numPredictions + 0] = 0.3f; // person, below threshold-relevant but not the max

        // Box 1: class 0 ("person") at 0.05 - below the confidence threshold entirely.
        output[(0 * numPredictions) + 1] = 10;
        output[(1 * numPredictions) + 1] = 10;
        output[(2 * numPredictions) + 1] = 5;
        output[(3 * numPredictions) + 1] = 5;
        output[(4 + 0) * numPredictions + 1] = 0.05f;

        var detections = YoloPostProcessing.DecodeDetections(output, numPredictions, confidenceThreshold: 0.25f);

        var kept = Assert.Single(detections);
        Assert.Equal(2, kept.ClassId);
        Assert.Equal(0.9f, kept.Confidence);
        Assert.Equal(100, kept.CenterX);
        Assert.Equal(50, kept.CenterY);
        Assert.Equal(20, kept.Width);
        Assert.Equal(30, kept.Height);
    }

    [Fact]
    public void DecodeDetections_EmptyWhenNothingClearsThreshold()
    {
        const int numPredictions = 1;
        var output = new float[84 * numPredictions];
        output[(4 + 0) * numPredictions] = 0.1f;

        var detections = YoloPostProcessing.DecodeDetections(output, numPredictions, confidenceThreshold: 0.25f);

        Assert.Empty(detections);
    }

    [Theory]
    [InlineData(0, 0, 10, 10, 0, 0, 10, 10, 1.0f)]   // identical boxes
    [InlineData(0, 0, 10, 10, 100, 100, 10, 10, 0f)] // disjoint boxes
    public void ComputeIou_HandlesIdenticalAndDisjointBoxes(float cxA, float cyA, float wA, float hA, float cxB, float cyB, float wB, float hB, float expected)
    {
        var a = new RawDetection(cxA, cyA, wA, hA, ClassId: 0, Confidence: 1f);
        var b = new RawDetection(cxB, cyB, wB, hB, ClassId: 0, Confidence: 1f);

        Assert.Equal(expected, YoloPostProcessing.ComputeIou(a, b), precision: 5);
    }

    [Fact]
    public void ComputeIou_PartialOverlapMatchesHandCalculatedValue()
    {
        // Box A: [0,0]-[10,10] (as corners). Box B: [5,5]-[15,15]. Intersection: [5,5]-[10,10] = 25.
        // Union: 100 + 100 - 25 = 175. IoU = 25/175.
        var a = new RawDetection(CenterX: 5, CenterY: 5, Width: 10, Height: 10, ClassId: 0, Confidence: 1f);
        var b = new RawDetection(CenterX: 10, CenterY: 10, Width: 10, Height: 10, ClassId: 0, Confidence: 1f);

        var iou = YoloPostProcessing.ComputeIou(a, b);

        Assert.Equal(25f / 175f, iou, precision: 5);
    }

    [Fact]
    public void ApplyNms_SuppressesOverlappingSameClassBoxes_KeepsHigherConfidence()
    {
        var strong = new RawDetection(CenterX: 50, CenterY: 50, Width: 20, Height: 20, ClassId: 2, Confidence: 0.9f);
        var weakerOverlap = new RawDetection(CenterX: 52, CenterY: 52, Width: 20, Height: 20, ClassId: 2, Confidence: 0.6f);

        var kept = YoloPostProcessing.ApplyNms([strong, weakerOverlap], iouThreshold: 0.45f);

        var only = Assert.Single(kept);
        Assert.Equal(0.9f, only.Confidence);
    }

    [Fact]
    public void ApplyNms_KeepsOverlappingBoxesOfDifferentClasses()
    {
        // A person on a bicycle: heavily overlapping boxes, but different classes - both real.
        var person = new RawDetection(CenterX: 50, CenterY: 50, Width: 20, Height: 20, ClassId: 0, Confidence: 0.8f);
        var bicycle = new RawDetection(CenterX: 50, CenterY: 50, Width: 20, Height: 20, ClassId: 1, Confidence: 0.85f);

        var kept = YoloPostProcessing.ApplyNms([person, bicycle], iouThreshold: 0.45f);

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void ApplyNms_KeepsNonOverlappingSameClassBoxes()
    {
        var carA = new RawDetection(CenterX: 10, CenterY: 10, Width: 10, Height: 10, ClassId: 2, Confidence: 0.7f);
        var carB = new RawDetection(CenterX: 500, CenterY: 500, Width: 10, Height: 10, ClassId: 2, Confidence: 0.75f);

        var kept = YoloPostProcessing.ApplyNms([carA, carB], iouThreshold: 0.45f);

        Assert.Equal(2, kept.Count);
    }
}
