using System;
using FFMediaToolkit.Common.Internal;
using FFMediaToolkit.Decoding.Internal;
using FFMediaToolkit.Helpers;

namespace FFMediaToolkit.Decoding;

/// <summary>
/// A base for streams of any kind of media.
/// </summary>
public class MediaStream : IDisposable
{
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaStream"/> class.
    /// </summary>
    /// <param name="stream">The associated codec.</param>
    /// <param name="options">Extra options.</param>
    internal MediaStream(Decoder stream, MediaOptions options)
    {
        _stream = stream;
        Options = options;

        _seekThreshold = TimeSpan.FromSeconds(0.5).ToTimestamp(Info.TimeBase);
    }

    /// <summary>
    /// Gets informations about this stream.
    /// </summary>
    public StreamInfo Info => _stream.Info;

    /// <summary>
    /// Gets the timestamp of the recently decoded frame in the media stream.
    /// </summary>
    public TimeSpan Position => Math.Max(_stream.RecentlyDecodedFrame.PresentationTimestamp, 0).ToTimeSpan(Info.TimeBase);

    /// <summary>
    /// Indicates whether the stream has buffered frame data.
    /// </summary>
    public bool IsBufferEmpty => _stream.IsBufferEmpty;

    /// <summary>
    /// Gets the options configured for this <see cref="MediaStream"/>.
    /// </summary>
    protected MediaOptions Options { get; }

    private Decoder _stream;

    private long _seekThreshold;

    private long _lastRequestedFrameTimestamp;

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (!isDisposed)
        {
            _stream.DiscardBufferedData();
            _stream.Dispose();
            isDisposed = true;
        }
    }

    /// <summary>
    /// Gets the data belonging to the next frame in the stream.
    /// </summary>
    /// <returns>The next frame's data.</returns>
    internal MediaFrame GetNextFrame()
    {
        MediaFrame frame = _stream.GetNextFrame();
        _lastRequestedFrameTimestamp = frame.PresentationTimestamp;
        return frame;
    }

    /// <summary>
    /// Seeks the stream to the specified time and returns the nearest frame's data.
    /// </summary>
    /// <param name="time">A specific point in time in this stream.</param>
    /// <returns>The nearest frame's data.</returns>
    internal MediaFrame GetFrame(TimeSpan time)
    {
        long ts = time.ToTimestamp(Info.TimeBase);
        MediaFrame frame = GetFrameByTimestamp(ts);
        return frame;
    }

    private MediaFrame GetFrameByTimestamp(long ts)
    {
        MediaFrame frame = _stream.RecentlyDecodedFrame;
        ts = Math.Clamp(ts, 0, Info.DurationRaw);

        if ((ts > (frame.PresentationTimestamp + _seekThreshold)) || (ts < _lastRequestedFrameTimestamp))
            _stream.OwnerFile.SeekFile(ts, Info.Index);

        _stream.SkipFrames(ts);

        _lastRequestedFrameTimestamp = ts;

        return _stream.RecentlyDecodedFrame;
    }
}