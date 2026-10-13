namespace MpvSkiaPoc;

internal readonly record struct FrameSummary(
    double Fps, double AvgMs, double MaxMs, double MedianMs, int Hitches, int Samples,
    double VideoFps, double MpvMs, double SkiaMs);

/// <summary>Rolling frame-pacing statistics for the render loop.</summary>
internal sealed class FrameStats
{
    private const int N = 240;
    private readonly double[] _intervals = new double[N];
    private int _count, _index;
    private double _last = -1, _now;
    private readonly Queue<double> _frames = new();
    private readonly Queue<double> _videoFrames = new();
    private double _mpvAcc, _skiaAcc;
    private int _acc;

    public void Frame(double now, double mpvMs, double skiaMs, bool newVideoFrame)
    {
        _now = now;
        if (_last >= 0)
        {
            _intervals[_index] = (now - _last) * 1000.0;
            _index = (_index + 1) % N;
            if (_count < N) _count++;
        }
        _last = now;

        _frames.Enqueue(now);
        if (newVideoFrame) _videoFrames.Enqueue(now);
        Prune();

        _mpvAcc += mpvMs;
        _skiaAcc += skiaMs;
        _acc++;
    }

    private void Prune()
    {
        while (_frames.Count > 0 && _frames.Peek() < _now - 1.0) _frames.Dequeue();
        while (_videoFrames.Count > 0 && _videoFrames.Peek() < _now - 1.0) _videoFrames.Dequeue();
    }

    /// <summary>Summarises the window; per-frame CPU averages are reset on each call.</summary>
    public FrameSummary Compute()
    {
        Prune();
        if (_count == 0) return default;

        var sorted = _intervals.AsSpan(0, _count).ToArray();
        Array.Sort(sorted);
        double median = sorted[_count / 2];
        double avg = sorted.Average();
        int hitches = sorted.Count(x => x > median * 1.5);

        double mpv = _acc > 0 ? _mpvAcc / _acc : 0;
        double skia = _acc > 0 ? _skiaAcc / _acc : 0;
        _mpvAcc = _skiaAcc = 0;
        _acc = 0;

        return new FrameSummary(_frames.Count, avg, sorted[^1], median, hitches, _count,
            _videoFrames.Count, mpv, skia);
    }
}
