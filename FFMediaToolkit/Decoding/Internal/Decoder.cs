﻿namespace FFMediaToolkit.Decoding.Internal
{
    using System;
    using System.Collections.Generic;
    using FFMediaToolkit.Common;
    using FFMediaToolkit.Common.Internal;
    using FFMediaToolkit.Helpers;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represents a input multimedia stream.
    /// </summary>
    internal unsafe class Decoder : Wrapper<AVCodecContext>
    {
        private bool reuseLastPacket;
        private MediaPacket packet;

        /// <summary>
        /// Initializes a new instance of the <see cref="Decoder"/> class.
        /// </summary>
        /// <param name="codec">The underlying codec.</param>
        /// <param name="stream">The multimedia stream.</param>
        /// <param name="owner">The container that owns the stream.</param>
        public Decoder(AVCodecContext* codec, AVStream* stream, InputContainer owner)
            : base(codec)
        {
            OwnerFile = owner;
            Info = StreamInfo.Create(stream, owner);
            switch (Info.Type)
            {
                case MediaType.Audio:
                    RecentlyDecodedFrame = new AudioFrame();
                    break;
                case MediaType.Video:
                    RecentlyDecodedFrame = new VideoFrame();
                    break;
                default:
                    throw new Exception("Tried to create a decoder from an unsupported stream or codec type.");
            }

            BufferedPackets = new Queue<MediaPacket>();
        }

        /// <summary>
        /// Gets informations about the stream.
        /// </summary>
        public StreamInfo Info { get; }

        /// <summary>
        /// Gets the media container that owns this stream.
        /// </summary>
        public InputContainer OwnerFile { get; }

        /// <summary>
        /// Gets the recently decoded frame.
        /// </summary>
        public MediaFrame RecentlyDecodedFrame { get; }

        /// <summary>
        /// Indicates whether the codec has buffered packets.
        /// </summary>
        public bool IsBufferEmpty => BufferedPackets.Count == 0;

        /// <summary>
        /// Gets a FIFO collection of media packets that the codec has buffered.
        /// </summary>
        private Queue<MediaPacket> BufferedPackets { get; }

        /// <summary>
        /// Adds the specified packet to the codec buffer.
        /// </summary>
        /// <param name="packet">The packet to be buffered.</param>
        public void BufferPacket(MediaPacket packet)
        {
            BufferedPackets.Enqueue(packet);
        }

        /// <summary>
        /// Reads the next frame from the stream.
        /// </summary>
        /// <returns>The decoded frame.</returns>
        public MediaFrame GetNextFrame()
        {
            ReadNextFrame();
            return RecentlyDecodedFrame;
        }

        /// <summary>
        /// Decodes frames until reach the specified time stamp. Useful to seek few frames forward.
        /// </summary>
        /// <param name="targetTs">The target time stamp.</param>
        public void SkipFrames(long targetTs)
        {
            do
            {
                ReadNextFrame();
            }
            while (RecentlyDecodedFrame.PresentationTimestamp < targetTs);
        }

        /// <summary>
        /// Flushes the codec buffers.
        /// </summary>
        public void FlushBuffers() => ffmpeg.avcodec_flush_buffers(Pointer);

        /// <inheritdoc/>
        protected override void OnDisposing()
        {
            RecentlyDecodedFrame.Dispose();
            FlushBuffers();
            ffmpeg.avcodec_close(Pointer);
        }

        private void ReadNextFrame()
        {
            ffmpeg.av_frame_unref(RecentlyDecodedFrame.Pointer);
            int error;

            do
            {
                DecodePacket(); // Gets the next packet and sends it to the decoder
                error = ffmpeg.avcodec_receive_frame(Pointer, RecentlyDecodedFrame.Pointer); // Tries to decode frame from the packets.
            }
            while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN) || error == -35); // The EAGAIN code means that the frame decoding has not been completed and more packets are needed.
            error.ThrowIfError("An error occurred while decoding the frame.");
        }

        private void DecodePacket()
        {
            if (!reuseLastPacket)
            {
                if (IsBufferEmpty)
                    OwnerFile.GetPacketFromStream(Info.Index);
                packet = BufferedPackets.Dequeue();
            }

            // Sends the packet to the decoder.
            var result = ffmpeg.avcodec_send_packet(Pointer, packet);

            if (result == ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                reuseLastPacket = true;
            }
            else
            {
                reuseLastPacket = false;
                result.ThrowIfError("Cannot send a packet to the decoder.");
                packet.Wipe();
            }
        }
    }
}
