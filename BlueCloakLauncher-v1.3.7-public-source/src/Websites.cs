using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace Luma;

public static class Websites
{
	public static bool TryUrl(string input, out Uri address)
	{
		address = null;
		string text = (input ?? "").Trim();
		if (text == "" || text.Any(char.IsWhiteSpace))
		{
			return false;
		}
		if (!text.Contains("://"))
		{
			text = "https://" + text;
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out var result) || (result.Scheme != "http" && result.Scheme != "https") || string.IsNullOrEmpty(result.Host) || !string.IsNullOrEmpty(result.UserInfo))
		{
			return false;
		}
		address = result;
		return true;
	}

	private static byte[] Download(Uri address, int limit)
	{
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(address);
		obj.Timeout = 6000;
		obj.ReadWriteTimeout = 6000;
		obj.UserAgent = "Luma/1.0";
		obj.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
		obj.MaximumAutomaticRedirections = 4;
		using HttpWebResponse httpWebResponse = (HttpWebResponse)obj.GetResponse();
		if (httpWebResponse.ContentLength > limit)
		{
			throw new IOException("内容过大");
		}
		using Stream stream = httpWebResponse.GetResponseStream();
		using MemoryStream memoryStream = new MemoryStream();
		byte[] array = new byte[8192];
		int num;
		while ((num = stream.Read(array, 0, array.Length)) > 0)
		{
			if (memoryStream.Length + num > limit)
			{
				throw new IOException("内容过大");
			}
			memoryStream.Write(array, 0, num);
		}
		return memoryStream.ToArray();
	}

	public static WebsiteInfo Fetch(Uri address)
	{
		WebsiteInfo websiteInfo = new WebsiteInfo
		{
			Name = address.Host
		};
		Uri uri = new Uri(address.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
		try
		{
			string input = Encoding.UTF8.GetString(Download(address, 1048576));
			Match match = Regex.Match(input, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			if (match.Success)
			{
				websiteInfo.Name = WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "\\s+", " ")).Trim();
			}
			foreach (Match item in Regex.Matches(input, "<link\\b[^>]*>", RegexOptions.IgnoreCase))
			{
				string value = item.Value;
				if (Regex.IsMatch(value, "\\brel\\s*=\\s*[\"'][^\"']*\\bicon\\b", RegexOptions.IgnoreCase))
				{
					Match match2 = Regex.Match(value, "\\bhref\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
					if (match2.Success && Uri.TryCreate(address, WebUtility.HtmlDecode(match2.Groups[1].Value), out var result) && (result.Scheme == "http" || result.Scheme == "https"))
					{
						uri = result;
						break;
					}
				}
			}
		}
		catch
		{
		}
		foreach (Uri item2 in new Uri[2]
		{
			uri,
			new Uri(address.GetLeftPart(UriPartial.Authority) + "/favicon.ico")
		}.Distinct())
		{
			try
			{
				using MemoryStream bitmapStream = new MemoryStream(Download(item2, 2097152));
				BitmapFrame bitmapFrame = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.OrderByDescending((BitmapFrame image) => image.PixelWidth).First();
				if (bitmapFrame.PixelWidth > 2048 || bitmapFrame.PixelHeight > 2048)
				{
					continue;
				}
				string text = Path.Combine(Program.DataDir, "website-icons");
				Directory.CreateDirectory(text);
				string text2;
				using (SHA256 sHA = SHA256.Create())
				{
					text2 = BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(address.AbsoluteUri))).Replace("-", "");
				}
				string text3 = Path.Combine(text, text2 + ".png");
				PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
				pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapFrame));
				using (FileStream stream = File.Create(text3))
				{
					pngBitmapEncoder.Save(stream);
				}
				websiteInfo.IconPath = text3;
				break;
			}
			catch
			{
			}
		}
		if (string.IsNullOrWhiteSpace(websiteInfo.Name))
		{
			websiteInfo.Name = address.Host;
		}
		if (websiteInfo.Name.Length > 100)
		{
			websiteInfo.Name = websiteInfo.Name.Substring(0, 100);
		}
		return websiteInfo;
	}
}
