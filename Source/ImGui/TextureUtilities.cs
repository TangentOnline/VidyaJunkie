using System.Globalization;
using System.Runtime.CompilerServices;
using QoiSharp.Codec;
using QoiSharp.Exceptions;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

namespace VidyaJunkie;

public static class TextureUtilities {
	public static IImageDecoder GetDecoder(string filepath) {
		string ext = Path.GetExtension(filepath).ToLower(CultureInfo.CurrentCulture);
		return ext switch {
			".png" => new PngDecoder(),
			".jpg" => new JpegDecoder(),
			".jpeg" => new JpegDecoder(),
			".bmp" => new BmpDecoder(),
			".gif" => new GifDecoder(),
			".tga" => new TgaDecoder(),
			".tiff" => new TiffDecoder(),
			".webp" => new WebpDecoder(),
			_ => throw new Exception("Unsupported image format")
		};
	}

	public static IImageDecoder GetDecoder(List<string> extensions) {
		return extensions switch {
			{ Count: > 0 } when extensions.Contains("png", StringComparer.CurrentCultureIgnoreCase) => new PngDecoder(),
			{ Count: > 0 } when extensions.Contains("jpg", StringComparer.CurrentCultureIgnoreCase) => new JpegDecoder(),
			{ Count: > 0 } when extensions.Contains("jpeg", StringComparer.CurrentCultureIgnoreCase) => new JpegDecoder(),
			{ Count: > 0 } when extensions.Contains("bmp", StringComparer.CurrentCultureIgnoreCase) => new BmpDecoder(),
			{ Count: > 0 } when extensions.Contains("gif", StringComparer.CurrentCultureIgnoreCase) => new GifDecoder(),
			{ Count: > 0 } when extensions.Contains("tga", StringComparer.CurrentCultureIgnoreCase) => new TgaDecoder(),
			{ Count: > 0 } when extensions.Contains("tiff", StringComparer.CurrentCultureIgnoreCase) => new TiffDecoder(),
			{ Count: > 0 } when extensions.Contains("webp", StringComparer.CurrentCultureIgnoreCase) => new WebpDecoder(),
			_ => throw new Exception("Unsupported image format")
		};
	}

	public static byte[] EncodeQoi(Span<byte> Data, int Width, int Height, Channels Channels, ColorSpace ColorSpace) {
		if (Width == 0) {
			DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(15, 1);
			interpolatedStringHandler.AppendLiteral("Invalid width: ");
			interpolatedStringHandler.AppendFormatted(Width);
			throw new QoiEncodingException(interpolatedStringHandler.ToStringAndClear());
		}

		if (Height == 0 || Height >= QoiCodec.MaxPixels / Width) {
			DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(44, 2);
			interpolatedStringHandler.AppendLiteral("Invalid height: ");
			interpolatedStringHandler.AppendFormatted(Height);
			interpolatedStringHandler.AppendLiteral(". Maximum for this image is ");
			interpolatedStringHandler.AppendFormatted(QoiCodec.MaxPixels / Width - 1);
			throw new QoiEncodingException(interpolatedStringHandler.ToStringAndClear());
		}

		int width = Width;
		int height = Height;
		byte channels = (byte)Channels;
		byte colorSpace = (byte)ColorSpace;
		byte[] array = new byte[14 + QoiCodec.Padding.Length + width * height * channels];
		array[0] = (byte)(QoiCodec.Magic >> 24);
		array[1] = (byte)(QoiCodec.Magic >> 16);
		array[2] = (byte)(QoiCodec.Magic >> 8);
		array[3] = (byte)QoiCodec.Magic;
		array[4] = (byte)(width >> 24);
		array[5] = (byte)(width >> 16);
		array[6] = (byte)(width >> 8);
		array[7] = (byte)width;
		array[8] = (byte)(height >> 24);
		array[9] = (byte)(height >> 16);
		array[10] = (byte)(height >> 8);
		array[11] = (byte)height;
		array[12] = channels;
		array[13] = colorSpace;
		byte[] numArray1 = new byte[256];
		byte r1 = 0;
		byte g1 = 0;
		byte b1 = 0;
		byte a1 = byte.MaxValue;
		byte maxValue = byte.MaxValue;
		int num1 = 0;
		int num2 = 14;
		bool flag = channels == 4;
		int num3 = width * height * channels;
		int num4 = num3 - channels;
		int num5 = 0;
		for (int index1 = 0; index1 < num3; index1 += channels) {
			byte num6 = Data[index1];
			byte num7 = Data[index1 + 1];
			byte num8 = Data[index1 + 2];
			if (flag) {
				maxValue = Data[index1 + 3];
			}

			if (RgbaEquals(r1, g1, b1, a1, num6, num7, num8, maxValue)) {
				++num1;
				if (num1 == 62 || index1 == num4) {
					array[num2++] = (byte)(192 | (num1 - 1));
					num1 = 0;
				}
			} else {
				if (num1 > 0) {
					array[num2++] = (byte)(192 | (num1 - 1));
					num1 = 0;
				}

				int hashTableIndex = QoiCodec.CalculateHashTableIndex(num6, num7, num8, maxValue);
				if (RgbaEquals(num6, num7, num8, maxValue, numArray1[hashTableIndex], numArray1[hashTableIndex + 1], numArray1[hashTableIndex + 2], numArray1[hashTableIndex + 3])) {
					array[num2++] = (byte)(0 | (hashTableIndex / 4));
				} else {
					numArray1[hashTableIndex] = num6;
					numArray1[hashTableIndex + 1] = num7;
					numArray1[hashTableIndex + 2] = num8;
					numArray1[hashTableIndex + 3] = maxValue;
					if (maxValue == a1) {
						int num9 = num6 - r1;
						int num10 = num7 - g1;
						int num11 = num8 - b1;
						int num12 = num9 - num10;
						int num13 = num11 - num10;
						if (num9 > -3 && num9 < 2 && num10 > -3 && num10 < 2 && num11 > -3 && num11 < 2) {
							++num5;
							array[num2++] = (byte)(64 | ((num9 + 2) << 4) | ((num10 + 2) << 2) | (num11 + 2));
						} else if (num12 > -9 && num12 < 8 && num10 > -33 && num10 < 32 && num13 > -9 && num13 < 8) {
							byte[] numArray2 = array;
							int index2 = num2;
							int num14 = index2 + 1;
							int num15 = (byte)(128 | (num10 + 32));
							numArray2[index2] = (byte)num15;
							byte[] numArray3 = array;
							int index3 = num14;
							num2 = index3 + 1;
							int num16 = (byte)(((num12 + 8) << 4) | (num13 + 8));
							numArray3[index3] = (byte)num16;
						} else {
							byte[] numArray4 = array;
							int index4 = num2;
							int num17 = index4 + 1;
							numArray4[index4] = 254;
							byte[] numArray5 = array;
							int index5 = num17;
							int num18 = index5 + 1;
							int num19 = num6;
							numArray5[index5] = (byte)num19;
							byte[] numArray6 = array;
							int index6 = num18;
							int num20 = index6 + 1;
							int num21 = num7;
							numArray6[index6] = (byte)num21;
							byte[] numArray7 = array;
							int index7 = num20;
							num2 = index7 + 1;
							int num22 = num8;
							numArray7[index7] = (byte)num22;
						}
					} else {
						byte[] numArray8 = array;
						int index8 = num2;
						int num23 = index8 + 1;
						numArray8[index8] = byte.MaxValue;
						byte[] numArray9 = array;
						int index9 = num23;
						int num24 = index9 + 1;
						int num25 = num6;
						numArray9[index9] = (byte)num25;
						byte[] numArray10 = array;
						int index10 = num24;
						int num26 = index10 + 1;
						int num27 = num7;
						numArray10[index10] = (byte)num27;
						byte[] numArray11 = array;
						int index11 = num26;
						int num28 = index11 + 1;
						int num29 = num8;
						numArray11[index11] = (byte)num29;
						byte[] numArray12 = array;
						int index12 = num28;
						num2 = index12 + 1;
						int num30 = maxValue;
						numArray12[index12] = (byte)num30;
					}
				}
			}

			r1 = num6;
			g1 = num7;
			b1 = num8;
			a1 = maxValue;
		}

		for (int index = 0; index < QoiCodec.Padding.Length; ++index)
			array[num2 + index] = QoiCodec.Padding[index];
		int end = num2 + QoiCodec.Padding.Length;
		return RuntimeHelpers.GetSubArray(array, Range.EndAt(end));
	}

	private static bool RgbaEquals(
		byte r1,
		byte g1,
		byte b1,
		byte a1,
		byte r2,
		byte g2,
		byte b2,
		byte a2
	) {
		return r1 == r2 && g1 == g2 && b1 == b2 && a1 == a2;
	}
}