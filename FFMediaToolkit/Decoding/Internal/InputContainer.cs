﻿namespace FFMediaToolkit.Decoding.Internal;

using System;
using System.IO;

using Common;
using FFMediaToolkit.Common.Internal;
using Helpers;

using FFmpeg.AutoGen;

/// <summary>
/// Represents the multimedia file container.
/// </summary>
internal unsafe class InputContainer : Wrapper<AVFormatContext>
{
    private readonly avio_alloc_context_read_packet readCallback;

    private readonly avio_alloc_context_seek seekCallBack;

    private InputContainer(AVFormatContext* formatContext, int bufferSizeLimit)
        : base(formatContext)
    {
        Decoders = new Decoder[Pointer->nb_streams];
        MaxBufferSize = bufferSizeLimit;
    }

    private InputContainer(AVFormatContext* formatContext, avio_alloc_context_read_packet read, avio_alloc_context_seek seek, int bufferSizeLimit)
        : base(formatContext)
    {
        Decoders = new Decoder[Pointer->nb_streams];
        MaxBufferSize = bufferSizeLimit;
        readCallback = read;
        seekCallBack = seek;
    }

    private delegate void AVFormatContextDelegate(AVFormatContext* context);

    /// <summary>
    /// List of all stream codecs that have been opened from the file.
    /// </summary>
    public Decoder[] Decoders { get; }

    /// <summary>
    /// Gets the memory limit of packets stored in the decoder's buffer.
    /// </summary>
    public int MaxBufferSize { get; }

    /// <summary>
    /// Opens a media container and stream codecs from given path.
    /// </summary>
    /// <param name="path">A path to the multimedia file.</param>
    /// <param name="options">The media settings.</param>
    /// <returns>A new instance of the <see cref="InputContainer"/> class.</returns>
    public static InputContainer LoadFile(string path, MediaOptions options) => MakeContainer(path, options, _ => { });

    /// <summary>
    /// Opens a media container and stream codecs from given stream.
    /// </summary>
    /// <param name="stream">A stream of the multimedia file.</param>
    /// <param name="options">The media settings.</param>
    /// <returns>A new instance of the <see cref="InputContainer"/> class.</returns>
    public static InputContainer LoadStream(Stream stream, MediaOptions options) => MakeContainer(stream, options);

    /// <summary>
    /// Seeks all streams in the container to the first key frame before the specified time stamp.
    /// </summary>
    /// <param name="targetTs">The target time stamp in a stream time base.</param>
    /// <param name="streamIndex">The stream index. It will be used only to get the correct time base value.</param>
    public void SeekFile(long targetTs, int streamIndex)
    {
        ffmpeg.av_seek_frame(Pointer, streamIndex, targetTs, ffmpeg.AVSEEK_FLAG_BACKWARD).ThrowIfError($"Seek to {targetTs} failed.");

        foreach (Decoder decoder in Decoders)
        {
            decoder?.DiscardBufferedData();
        }
    }

    /// <summary>
    /// Reads a packet from the specified stream index and buffers it in the respective codec.
    /// </summary>
    /// <param name="streamIndex">Index of the stream to read from.</param>
    /// <returns>True if the requested packet was read, false if EOF ocurred and a flush packet was send to the buffer.</returns>
    public bool GetPacketFromStream(int streamIndex)
    {
        MediaPacket packet;
        do
        {
            if (!TryReadNextPacket(out packet))
            {
                Decoders[streamIndex].BufferPacket(MediaPacket.CreateFlushPacket(streamIndex));
                return false;
            }

            Decoder stream = Decoders[packet.StreamIndex];
            if (stream == null)
            {
                packet.Wipe();
                packet.Dispose();
                packet = null;
            }
            else
            {
                stream.BufferPacket(packet);
            }
        }
        while (packet?.StreamIndex != streamIndex);
        return true;
    }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
        foreach (Decoder decoder in Decoders)
        {
            decoder?.Dispose();
        }

        AVFormatContext* ptr = Pointer;
        ffmpeg.avformat_close_input(&ptr);
    }

    private static AVFormatContext* MakeContext(string url, MediaOptions options, AVFormatContextDelegate contextDelegate)
    {
        FFmpegLoader.LoadFFmpeg();

        AVFormatContext* context = ffmpeg.avformat_alloc_context();
        options.DemuxerOptions.ApplyFlags(context);
        AVDictionary* dict = new FFDictionary(options.DemuxerOptions.PrivateOptions, false).Pointer;

        contextDelegate(context);

        ffmpeg.avformat_open_input(&context, url, null, &dict)
              .ThrowIfError("An error occurred while opening the file");

        ffmpeg.avformat_find_stream_info(context, null)
              .ThrowIfError("Cannot find stream info");

        return context;
    }

    private static InputContainer MakeContainer(Stream input, MediaOptions options)
    {
        AvioStream avioStream = new(input);
        avio_alloc_context_read_packet read = (avio_alloc_context_read_packet)avioStream.Read;
        avio_alloc_context_seek seek = (avio_alloc_context_seek)avioStream.Seek;

        AVFormatContext* context = MakeContext(null, options, ctx =>
                                                              {
                                                                  const int BUFFER_LENGTH = 4096;
                                                                  byte* avioBuffer = (byte*)ffmpeg.av_malloc((ulong)BUFFER_LENGTH);

                                                                  ctx->pb = ffmpeg.avio_alloc_context(avioBuffer, BUFFER_LENGTH, 0, null, read, null, seek);
                                                                  if (ctx->pb == null)
                                                                  {
                                                                      throw new FFmpegException("Cannot allocate AVIOContext.");
                                                                  }
                                                              });

        InputContainer container = new(context, read, seek, options.PacketBufferSizeLimit);
        container.OpenStreams(options);
        return container;
    }

    private static InputContainer MakeContainer(string url, MediaOptions options, AVFormatContextDelegate contextDelegate)
    {
        AVFormatContext* context = MakeContext(url, options, contextDelegate);

        InputContainer container = new(context, options.PacketBufferSizeLimit);
        container.OpenStreams(options);
        return container;
    }

    /// <summary>
    /// Opens the streams in the file using the specified <see cref="MediaOptions"/>.
    /// </summary>
    /// <param name="options">The <see cref="MediaOptions"/> object.</param>
    private void OpenStreams(MediaOptions options)
    {
        for (int i = 0; i < Pointer->nb_streams; i++)
        {
            AVStream* stream = Pointer->streams[i];
            if (options.ShouldLoadStreamsOfType(stream->codecpar->codec_type))
            {
                try
                {
                    Decoders[i] = DecoderFactory.OpenStream(this, options, stream);
                }
                catch (Exception)
                {
                    Decoders[i] = null;
                }
            }
        }
    }

    /// <summary>
    /// Reads the next packet from this file.
    /// </summary>
    private bool TryReadNextPacket(out MediaPacket packet)
    {
        packet = MediaPacket.AllocateEmpty();
        int result = ffmpeg.av_read_frame(Pointer, packet.Pointer); // Gets the next packet from the file.

        // Check if the end of file error occurred
        if (result < 0)
        {
            packet.Dispose();
            if (result == ffmpeg.AVERROR_EOF)
            {
                return false;
            }
            else
            {
                result.ThrowIfError("Cannot read next packet from the file");
            }
        }

        return true;
    }
}