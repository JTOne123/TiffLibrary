﻿using System.Buffers;
using System.Threading.Tasks;
using TiffLibrary.PixelFormats;

namespace TiffLibrary.ImageEncoder.PhotometricEncoder
{
    internal class Rgb24Encoder<TPixel> : ITiffImageEncoderMiddleware<TPixel> where TPixel : unmanaged
    {
        private static readonly ushort[] s_bitsPerSample = new ushort[] { 8, 8, 8 };

        public async ValueTask InvokeAsync(TiffImageEncoderContext<TPixel> context, ITiffImageEncoderPipelineNode<TPixel> next)
        {
            TiffSize imageSize = context.ImageSize;
            int arraySize = 3 * imageSize.Width * imageSize.Height;
            using (IMemoryOwner<byte> pixelData = context.MemoryPool.Rent(arraySize))
            {
                using (var writer = new TiffMemoryPixelBufferWriter<TiffRgb24>(context.MemoryPool, pixelData.Memory, imageSize.Width, imageSize.Height))
                using (TiffPixelBufferWriter<TPixel> convertedWriter = context.ConvertWriter(writer.AsPixelBufferWriter()))
                {
                    await context.GetReader().ReadAsync(convertedWriter, context.CancellationToken).ConfigureAwait(false);
                }

                context.PhotometricInterpretation = TiffPhotometricInterpretation.RGB;
                context.BitsPerSample = new TiffValueCollection<ushort>(s_bitsPerSample);
                context.UncompressedData = pixelData.Memory.Slice(0, arraySize);

                await next.RunAsync(context).ConfigureAwait(false);

                context.UncompressedData = default;
            }

            TiffImageFileDirectoryWriter ifdWriter = context.IfdWriter;
            if (!(ifdWriter is null))
            {
                await ifdWriter.WriteTagAsync(TiffTag.PhotometricInterpretation, new TiffValueCollection<ushort>((ushort)context.PhotometricInterpretation)).ConfigureAwait(false);
                await ifdWriter.WriteTagAsync(TiffTag.BitsPerSample, context.BitsPerSample).ConfigureAwait(false);
                await ifdWriter.WriteTagAsync(TiffTag.SamplesPerPixel, new TiffValueCollection<ushort>(3));
            }
        }
    }
}
