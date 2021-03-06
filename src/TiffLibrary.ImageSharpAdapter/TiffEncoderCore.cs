﻿using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TiffLibrary.ImageEncoder;
using TiffLibrary.PixelFormats;

namespace TiffLibrary.ImageSharpAdapter
{
    internal sealed class TiffEncoderCore
    {
        private readonly Configuration _configuration;
        private readonly ITiffEncoderOptions _options;
        private readonly MemoryPool<byte> _memoryPool;

        public TiffEncoderCore(Configuration configuration, ITiffEncoderOptions options)
        {
            _configuration = configuration;
            _options = options;
            _memoryPool = new ImageSharpMemoryPool(configuration.MemoryAllocator);
        }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream) where TPixel : unmanaged, IPixel<TPixel>
        {
            EncodeCoreAsync(image, new SyncStreamWrapper(stream)).GetAwaiter().GetResult();
        }

        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream) where TPixel : unmanaged, IPixel<TPixel>
        {
            return EncodeCoreAsync(image, stream);
        }


        private async Task EncodeCoreAsync<TPixel>(Image<TPixel> image, Stream stream) where TPixel : unmanaged, IPixel<TPixel>
        {
            using (TiffFileWriter writer = await TiffFileWriter.OpenAsync(stream, leaveOpen: true).ConfigureAwait(false))
            {
                TiffStreamOffset ifdOffset;

                using (TiffImageFileDirectoryWriter ifdWriter = writer.CreateImageFileDirectory())
                {
                    await EncodeImageAsync(image, ifdWriter).ConfigureAwait(false);

                    ifdOffset = await ifdWriter.FlushAsync().ConfigureAwait(false);
                }

                writer.SetFirstImageFileDirectoryOffset(ifdOffset);

                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        private Task EncodeImageAsync<TPixel>(Image<TPixel> image, TiffImageFileDirectoryWriter ifdWriter) where TPixel : unmanaged, IPixel<TPixel>
        {
            ITiffEncoderOptions options = _options;

            var builder = new TiffImageEncoderBuilder();
            builder.MemoryPool = _memoryPool;
            builder.PhotometricInterpretation = options.PhotometricInterpretation;
            builder.Compression = options.Compression;
            builder.IsTiled = options.IsTiled;
            builder.RowsPerStrip = options.RowsPerStrip;
            builder.TileSize = new TiffSize(options.TileSize.Width, options.TileSize.Height);
            builder.Predictor = options.Predictor;
            builder.EnableTransparencyForRgb = options.EnableTransparencyForRgb;
            builder.Orientation = options.Orientation;
            builder.DeflateCompressionLevel = options.DeflateCompressionLevel;
            builder.JpegOptions = new TiffJpegEncodingOptions { Quality = options.JpegQuality, OptimizeCoding = options.JpegOptimizeCoding };
            builder.HorizontalChromaSubSampling = options.HorizontalChromaSubSampling;
            builder.VerticalChromaSubSampling = options.VerticalChromaSubSampling;

            // Fast path for TiffLibrary-supported pixel formats
            if (typeof(TPixel) == typeof(L8))
            {
                return BuildAndEncodeAsync<L8, TiffGray8>(builder, Unsafe.As<Image<L8>>(image), ifdWriter);
            }
            if (typeof(TPixel) == typeof(L16))
            {
                return BuildAndEncodeAsync<L16, TiffGray16>(builder, Unsafe.As<Image<L16>>(image), ifdWriter);
            }
            if (typeof(TPixel) == typeof(Rgb24))
            {
                return BuildAndEncodeAsync<Rgb24, TiffRgb24>(builder, Unsafe.As<Image<Rgb24>>(image), ifdWriter);
            }
            if (typeof(TPixel) == typeof(Rgba32))
            {
                return BuildAndEncodeAsync<Rgba32, TiffRgba32>(builder, Unsafe.As<Image<Rgba32>>(image), ifdWriter);
            }
            if (typeof(TPixel) == typeof(Bgr24))
            {
                return BuildAndEncodeAsync<Bgr24, TiffBgr24>(builder, Unsafe.As<Image<Bgr24>>(image), ifdWriter);
            }
            if (typeof(TPixel) == typeof(Bgra32))
            {
                return BuildAndEncodeAsync<Bgra32, TiffBgra32>(builder, Unsafe.As<Image<Bgra32>>(image), ifdWriter);
            }
            if (typeof(TPixel) == typeof(Rgba64))
            {
                return BuildAndEncodeAsync<Rgba64, TiffRgba64>(builder, Unsafe.As<Image<Rgba64>>(image), ifdWriter);
            }

            // Slow path
            return EncodeImageSlowAsync(builder, image, ifdWriter);
        }

        private static async Task BuildAndEncodeAsync<TImageSharpPixel, TTiffPixel>(TiffImageEncoderBuilder builder, Image<TImageSharpPixel> image, TiffImageFileDirectoryWriter ifdWriter) where TImageSharpPixel : unmanaged, IPixel<TImageSharpPixel> where TTiffPixel : unmanaged
        {
            TiffImageEncoder<TTiffPixel> encoder = builder.Build<TTiffPixel>();
            await encoder.EncodeAsync(ifdWriter, new ImageSharpPixelBufferReader<TImageSharpPixel, TTiffPixel>(image)).ConfigureAwait(false);
        }

        private async Task EncodeImageSlowAsync<TPixel>(TiffImageEncoderBuilder builder, Image<TPixel> image, TiffImageFileDirectoryWriter ifdWriter) where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<Rgba32> img = image.CloneAs<Rgba32>(_configuration);
            TiffImageEncoder<TiffRgba32> encoder = builder.Build<TiffRgba32>();
            await encoder.EncodeAsync(ifdWriter, new ImageSharpPixelBufferReader<Rgba32, TiffRgba32>(img)).ConfigureAwait(false);
        }
    }
}
