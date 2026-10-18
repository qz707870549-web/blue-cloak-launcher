using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Luma;

public static class AppCustomization
{
	public static string StoreIcon(string source, string directory)
	{
		BitmapSource bitmapSource = IconLoader.ReadImage(source) as BitmapSource;
		if (bitmapSource == null)
		{
			throw new InvalidDataException("请选择有效的 PNG、JPG、ICO、BMP 或 GIF 图片。");
		}
		if (bitmapSource.PixelWidth > 8192 || bitmapSource.PixelHeight > 8192)
		{
			throw new InvalidDataException("图片尺寸过大，请选择不超过 8192 像素的图片。");
		}
		double num = Math.Min(1.0, 256.0 / (double)Math.Max(bitmapSource.PixelWidth, bitmapSource.PixelHeight));
		if (num < 1.0)
		{
			bitmapSource = new TransformedBitmap(bitmapSource, new ScaleTransform(num, num));
			bitmapSource.Freeze();
		}
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
		byte[] array;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			pngBitmapEncoder.Save(memoryStream);
			array = memoryStream.ToArray();
		}
		string path;
		using (SHA256 sHA = SHA256.Create())
		{
			path = BitConverter.ToString(sHA.ComputeHash(array)).Replace("-", "") + ".png";
		}
		Directory.CreateDirectory(directory);
		string text = Path.Combine(directory, path);
		if (!File.Exists(text))
		{
			File.WriteAllBytes(text, array);
		}
		return text;
	}
}
