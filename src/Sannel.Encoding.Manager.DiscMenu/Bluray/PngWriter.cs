using System.Buffers.Binary;
using System.IO.Compression;

namespace Sannel.Encoding.Manager.DiscMenu.Bluray;

/// <summary>Minimal PNG encoder (8-bit RGBA, no filtering), so the crawler can save overlay planes without an imaging library.</summary>
internal static class PngWriter
{
	/// <summary>Writes ARGB pixels (0xAARRGGBB, row-major) as an RGBA PNG.</summary>
	public static void WriteArgb(string path, ReadOnlySpan<uint> argb, int width, int height)
	{
		// One filter byte (0 = none) per row, then RGBA.
		var rowBytes = (width * 4) + 1;
		var raw = new byte[height * rowBytes];
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var index = (y * width) + x;
				var pixel = index < argb.Length ? argb[index] : 0;
				var offset = (y * rowBytes) + 1 + (x * 4);
				raw[offset] = (byte)(pixel >> 16);
				raw[offset + 1] = (byte)(pixel >> 8);
				raw[offset + 2] = (byte)pixel;
				raw[offset + 3] = (byte)(pixel >> 24);
			}
		}

		File.WriteAllBytes(path, Encode(width, height, 6, raw));
	}

	/// <summary>Encodes already-filtered scanlines (<paramref name="colorType"/> 2 = RGB, 6 = RGBA).</summary>
	public static byte[] Encode(int width, int height, byte colorType, byte[] raw)
	{
		using var output = new MemoryStream();
		output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
		var header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
		header[8] = 8; // bit depth
		header[9] = colorType;
		WriteChunk(output, "IHDR", header);
		using (var compressed = new MemoryStream())
		{
			using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
			{
				zlib.Write(raw);
			}

			WriteChunk(output, "IDAT", compressed.ToArray());
		}

		WriteChunk(output, "IEND", []);
		return output.ToArray();
	}

	private static void WriteChunk(Stream output, string type, byte[] data)
	{
		var length = new byte[4];
		BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
		output.Write(length);
		var typeAndData = new byte[4 + data.Length];
		System.Text.Encoding.ASCII.GetBytes(type, typeAndData);
		data.CopyTo(typeAndData, 4);
		output.Write(typeAndData);
		var crc = 0xFFFFFFFFu;
		foreach (var b in typeAndData)
		{
			crc ^= b;
			for (var k = 0; k < 8; k++)
			{
				crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
			}
		}

		var checksum = new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(checksum, ~crc);
		output.Write(checksum);
	}
}
