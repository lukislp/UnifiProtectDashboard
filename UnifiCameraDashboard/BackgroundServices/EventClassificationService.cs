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
    /// critical path). Returns false if classification is disabled or the queue is full - a full
    /// queue means classification is falling behind real load, which is itself part of what S2
    /// is meant to measure, so it's logged rather than silently swallowed.
    /// </summary>
    bool TryEnqueue(ClassificationRequest request);
}

/// <summary>
/// Classifies event thumbnails with YOLO, one at a time - a single-consumer bounded-channel
/// queue, deliberately not parallel, to keep CPU/memory load predictable on a shared 8GB Pi.
/// </summary>
public sealed class EventClassificationService : BackgroundService, IClassificationQueue
{
    private const int QueueCapacity = 20;

    private readonly Channel<ClassificationRequest> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventClassificationService> _logger;
    private long _droppedCount;

    public EventClassificationService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<EventClassificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _channel = Channel.CreateBounded<ClassificationRequest>(QueueCapacity);
    }

    public bool TryEnqueue(ClassificationRequest request)
    {
        if (!IsEnabled())
        {
            return false;
        }

        if (_channel.Writer.TryWrite(request))
        {
            return true;
        }

        var dropped = Interlocked.Increment(ref _droppedCount);
        _logger.LogWarning(
            "Classification queue full - dropped request for event {EventId} (total dropped this run: {Dropped})",
            request.UnifiEventId, dropped);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Event classification disabled via configuration (EventClassification:Enabled=false)");
            return;
        }

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
            // measurement week summarizes into ms/image, RAM, and filter rate.
            var workingSetMb = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
            _logger.LogInformation(
                "YOLO classify {EventId}: {DurationMs}ms, labels=[{Labels}], workingSet={WorkingSetMB:0.0}MB",
                request.UnifiEventId, stopwatch.ElapsedMilliseconds, string.Join(",", labels), workingSetMb);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify event {EventId}", request.UnifiEventId);
        }
    }

    private bool IsEnabled() => _configuration.GetValue("EventClassification:Enabled", true);
}
