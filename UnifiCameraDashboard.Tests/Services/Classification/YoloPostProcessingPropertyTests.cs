using FsCheck;
using FsCheck.Xunit;
using UnifiCameraDashboard.BackgroundServices;
using UnifiCameraDashboard.Services.Classification;

namespace UnifiCameraDashboard.Tests.Services.Classification;

/// <summary>
/// Property-based tests (FsCheck) for the pure geometry and scheduling helpers: letterbox
/// layout, IoU, non-max suppression and the digest/backfill clock arithmetic. Each invariant
/// runs against hundreds of generated sizes, boxes and timestamps.
/// </summary>
public class YoloPostProcessingPropertyTests
{
    [Property(MaxTest = 500)]
    public bool The_letterbox_never_exceeds_the_canvas_and_fills_one_axis(PositiveInt width, PositiveInt height, PositiveInt target)
    {
        var layout = YoloPostProcessing.ComputeLetterbox(width.Get, height.Get, target.Get);
        return layout.NewWidth <= target.Get && layout.NewHeight <= target.Get
            && layout.PadX >= 0 && layout.PadY >= 0 && layout.Scale > 0
            && Math.Abs(Math.Max(layout.NewWidth, layout.NewHeight) - target.Get) <= 1
            && layout.PadX == (target.Get - layout.NewWidth) / 2
            && layout.PadY == (target.Get - layout.NewHeight) / 2;
    }

    [Property(MaxTest = 500)]
    public bool Iou_is_symmetric_within_the_unit_interval_and_one_for_a_box_against_itself(
        byte ax, byte ay, byte aw, byte ah, byte bx, byte by, byte bw, byte bh)
    {
        var a = new RawDetection(ax, ay, aw + 1, ah + 1, 0, 1f);
        var b = new RawDetection(bx, by, bw + 1, bh + 1, 0, 1f);
        var ab = YoloPostProcessing.ComputeIou(a, b);
        var ba = YoloPostProcessing.ComputeIou(b, a);
        return ab >= 0f && ab <= 1f
            && Math.Abs(ab - ba) < 1e-6f
            && Math.Abs(YoloPostProcessing.ComputeIou(a, a) - 1f) < 1e-6f;
    }

    [Property(MaxTest = 300)]
    public bool Nms_keeps_a_subset_that_still_contains_the_most_confident_box_of_every_class(
        (byte X, byte Y, byte W, byte H, byte Class, byte Confidence)[] boxes, byte thresholdSeed)
    {
        var detections = boxes
            .Select(b => new RawDetection(b.X, b.Y, b.W + 1, b.H + 1, b.Class % 4, b.Confidence / 255f))
            .ToList();
        var threshold = thresholdSeed / 255f;

        var kept = YoloPostProcessing.ApplyNms(detections, threshold);

        var isSubset = kept.All(detections.Contains) && kept.Count <= detections.Count;
        var bestPerClassSurvives = detections
            .GroupBy(d => d.ClassId)
            .All(g => kept.Contains(g.OrderByDescending(d => d.Confidence).First()));
        var distinctClassesUntouched = detections.Select(d => d.ClassId).Distinct().Count() != detections.Count
            || kept.Count == detections.Count;
        return isSubset && bestPerClassSurvives && distinctClassesUntouched;
    }

    [Property(MaxTest = 500)]
    public bool The_next_digest_is_always_in_the_future_and_at_most_a_day_away(DateTime now, byte offsetSeed, string? timeOfDay)
    {
        if (now.Year is < 2 or > 9998)
            return true; // a day past DateTime.MaxValue is not a real-world "now"
        var offset = TimeSpan.FromMinutes(((offsetSeed % 57) - 28) * 30);
        var nowWithOffset = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Unspecified), offset);

        var delay = DailyDigestService.ComputeDelayUntilNext(timeOfDay!, nowWithOffset);

        return delay > TimeSpan.Zero && delay <= TimeSpan.FromDays(1);
    }

    [Property(MaxTest = 500)]
    public bool The_digest_resume_point_never_precedes_the_lookback_bound_and_never_moves_a_recent_watermark(
        DateTime now, bool hasWatermark, DateTime watermark, NonNegativeInt lookbackMinutes)
    {
        if (now.Year is < 2 or > 9998 || watermark.Year is < 2 or > 9998)
            return true;
        var lookback = TimeSpan.FromMinutes(lookbackMinutes.Get);
        DateTime? optionalWatermark = hasWatermark ? watermark : null;

        var since = DailyDigestService.ComputeSince(optionalWatermark, now, lookback);

        var earliest = now - lookback;
        var expected = optionalWatermark is { } w && w >= earliest ? w : earliest;
        return since == expected && since >= earliest;
    }

    [Property(MaxTest = 500)]
    public bool The_backfill_start_never_precedes_the_lookback_bound_and_never_moves_a_recent_watermark(
        DateTime now, bool hasWatermark, DateTime watermark, NonNegativeInt lookbackMinutes)
    {
        if (now.Year is < 2 or > 9998 || watermark.Year is < 2 or > 9998)
            return true;
        var lookback = TimeSpan.FromMinutes(lookbackMinutes.Get);
        var nowUtc = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Unspecified), TimeSpan.Zero);
        DateTimeOffset? optionalWatermark = hasWatermark
            ? new DateTimeOffset(DateTime.SpecifyKind(watermark, DateTimeKind.Unspecified), TimeSpan.Zero)
            : null;

        var start = EventIngestionService.ComputeBackfillStart(optionalWatermark, nowUtc, lookback);

        var earliest = nowUtc - lookback;
        var expected = optionalWatermark is { } w && w >= earliest ? w : earliest;
        return start == expected && start >= earliest;
    }
}
