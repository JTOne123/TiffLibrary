﻿using System;
using System.Buffers;
using System.Threading;
using TiffLibrary.PixelBuffer;
using TiffLibrary.PixelConverter;

namespace TiffLibrary.ImageDecoder
{
    /// <summary>
    /// Information of the current decoding process.
    /// </summary>
    /// <typeparam name="TDestinationPixel">The pixel type of the destination writer.</typeparam>
    public class TiffDefaultImageDecoderContext<TDestinationPixel> : TiffImageDecoderContext where TDestinationPixel : unmanaged
    {
        /// <summary>
        /// The memory pool to use when allocating large chunk of memory.
        /// </summary>
        public override MemoryPool<byte>? MemoryPool { get; set; }

        /// <summary>
        /// The <see cref="CancellationToken"/> that fires if the user has requested to abort the decoding pipeline.
        /// </summary>
        public override CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Parameters of how the TIFF file should be parsed.
        /// </summary>
        public override TiffOperationContext? OperationContext { get; set; }

        /// <summary>
        /// The content reader to read data from.
        /// </summary>
        public override TiffFileContentReader? ContentReader { get; set; }

        /// <summary>
        /// The regions in the stream to read each plane data from.
        /// </summary>
        public override TiffValueCollection<TiffStreamRegion> PlanarRegions { get; set; }

        /// <summary>
        /// Data decompressed from raw data. (It should contains enough data for SourceReadOffset and ReadSize)
        /// </summary>
        public override Memory<byte> UncompressedData { get; set; }

        /// <summary>
        /// Image size of the decompressed data.
        /// </summary>
        public override TiffSize SourceImageSize { get; set; }

        /// <summary>
        /// Read offset from source image (decompressed image).
        /// </summary>
        public override TiffPoint SourceReadOffset { get; set; }

        /// <summary>
        /// The size of the image to read from the uncompressed data and write to destination buffer.
        /// </summary>
        public override TiffSize ReadSize { get; set; }

        /// <summary>
        /// The pixel buffer writer.
        /// </summary>
        public TiffPixelBufferWriter<TDestinationPixel> DestinationWriter { get; set; }

        /// <summary>
        /// A factory instance for creating <see cref="TiffPixelConverter{TSource, TDestination}"/> object to convert from one pixel format to another.
        /// </summary>
        public ITiffPixelConverterFactory? PixelConverterFactory { get; set; }

        /// <summary>
        /// A function to get destination buffer in the specified pixel format.
        /// </summary>
        public sealed override TiffPixelBufferWriter<TPixel> GetWriter<TPixel>()
        {
            ITiffPixelConverterFactory? pixelConverterFactory = PixelConverterFactory;
            if (pixelConverterFactory is null)
            {
                throw new InvalidOperationException("Failed to acquire PixelConverterFactory");
            }

            ITiffPixelBufferWriter<TDestinationPixel> buffer = TiffPixelBufferUnsafeMarshal.GetBuffer(DestinationWriter, out TiffPoint offset, out TiffSize size);
            return pixelConverterFactory.CreateConverter<TPixel, TDestinationPixel>(buffer).Crop(offset, size);
        }
    }
}
