using System.Diagnostics;
using System.Threading.Channels;
using UnifiCameraDashboard.Services;
using UnifiCameraDashboard.Services.Classification;

namespace UnifiCameraDashboard.BackgroundServices;

public sealed record ClassificationRequest(string UnifiEventId, byte[] ImageBytes);

public interface IClassificationQueue
{
    /// <summary>
    /// Enqueues a classification request; never blocks the caller (event ingestion is the more
    /// critical path) and never drops it either - the queue is unbounded, so every enqueued
    /// image eventually gets classified by the single background consumer, however large the
    /// backlog grows in the meantime (e.g. during a large historical-backfill catch-up burst).
    /// Returns false only if classification is disabled via configuration.
    /// </summary>
    bool TryEnqueue(ClassificationRequest request);
}

/// <summary>
/// Classifies event thumbnails with YOLO, one at a time - a single-consumer unbounded-channel
/// queue, deliberately not parallel, to keep CPU/memory load predictable on a shared 8GB Pi.
/// Unbounded rather than a small bounded queue: every event must eventually get classified, not
/// just whatever fits in a fixed-size buffer - a burst (e.g. backfilling hundreds of historical
/// events at once) just grows the backlog, which the single consumer works through over time.
/// Each classification's log line reports the current backlog depth, so how far behind real-time
/// this falls during a burst - and how long it takes to drain - is directly observable.
/// </summary>
public sealed class EventClassificationService : BackgroundService, IClassificationQueue
{
    private readonly Channel<ClassificationRequest> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IInstanceLock _instanceLock;
    private readonly ILogger<EventClassificationService> _logger;

    public EventClassificationService(IServiceProvider serviceProvider, IConfiguration configuration, IInstanceLock instanceLock, ILogger<EventClassificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _instanceLock = instanceLock;
        _logger = logger;
        _channel = Channel.CreateUnbounded<ClassificationRequest>();
    }

    public bool TryEnqueue(ClassificationRequest request)
    {
        if (!IsEnabled())
        {
            return false;
        }

        // Always succeeds - the channel is unbounded, nothing to reject.
        return _channel.Writer.TryWrite(request);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Event classification disabled via configuration (EventClassification:Enabled=false)");
            return;
        }

        // Same write-instance lock as EventIngestionService (see there) - during a rolling
        // update, only one pod's copy of this service should be draining the queue and writing
        // classification results at a time.
        await _instanceLock.WhenAcquiredAsync(stoppingToken);

        // IYoloClassifier is a singleton (the ONNX model is loaded once) - resolvable directly
        // from the root provider, no scope needed for it.
        var classifier = _serviceProvider.GetRequiredService<IYoloClassifier>();

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ClassifyAsync(classifier, request);
        }
    }

    private async Task ClassifyAsync(IYoloClassifier classifier, ClassificationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var detections = classifier.Classify(request.ImageBytes);
            stopwatch.Stop();

            var labels = detections.Select(d => d.Label).ToList();

            using var scope = _serviceProvider.CreateScope();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            await eventRepository.SetYoloLabelsAsync(request.UnifiEventId, labels);

            // Structured, grep-able per-classification line - this is the raw data the S2
            // measurement week summarizes into ms/image, RAM, filter rate, and (queueDepth)
            // how far behind real-time the unbounded queue falls during a burst.
            var workingSetMb = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
            _logger.LogInformation(
                "YOLO classify {EventId}: {DurationMs}ms, labels=[{Labels}], workingSet={WorkingSetMB:0.0}MB, queueDepth={QueueDepth}",
                request.UnifiEventId, stopwatch.ElapsedMilliseconds, string.Join(",", labels), workingSetMb, _channel.Reader.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify event {EventId}", request.UnifiEventId);
        }
    }

    private bool IsEnabled() => _configuration.GetValue("EventClassification:Enabled", true);
}
