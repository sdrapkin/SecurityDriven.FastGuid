using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SecurityDriven
{
	/// <summary>Provides extension methods for converting <see cref="Guid"/> instances to and from Base64Url strings.</summary>
	/// <remarks>These methods allow efficient conversion between <see cref="Guid"/> and its compact Base64Url representation.</remarks>
	public static class GuidExtensions
	{
		// Copyright (c) 2025 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		const int BASE64URL_LENGTH = 22; // 16 bytes of Guid encoded in Base64Url is 22 characters long
		const string BASE64URL_LENGTH_STRING = "22"; // for error messages
		const int GUID_LENGTH = 16; // Length of a Guid in bytes
		const string BASE64URL_ALPHABET_STRING = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

		/// <summary>Converts a Guid to a Base64Url string.</summary>
		/// <param name="guid">The Guid to convert.</param>
		/// <returns>A Base64Url string representation of the Guid (22 chars).</returns>
		public static string ToBase64Url(this Guid guid)
		{
			return string.Create(BASE64URL_LENGTH, guid, static (outCharSpan, guid) =>
			{
				var inByteSpan = MemoryMarshal.CreateReadOnlySpan<byte>(ref Unsafe.As<Guid, byte>(ref guid), GUID_LENGTH);
				ReadOnlySpan<char> BASE64URL_ALPHABET = BASE64URL_ALPHABET_STRING;

				const int LENGTH_MOD_3 = 1; // 16 % 3 = 1;
				const int LIMIT = GUID_LENGTH - LENGTH_MOD_3;

				int i, j = 0;
				byte b0, b1, b2;

				for (i = 0; i < LIMIT; i += 3) // takes 3 bytes from inArray and inserts 4 bytes into output
				{
					b0 = inByteSpan[i];
					b1 = inByteSpan[i + 1];
					b2 = inByteSpan[i + 2];

					outCharSpan[j] = BASE64URL_ALPHABET[b0 >> 2];
					outCharSpan[j + 1] = BASE64URL_ALPHABET[((b0 & 0x03) << 4) | (b1 >> 4)];
					outCharSpan[j + 2] = BASE64URL_ALPHABET[((b1 & 0x0f) << 2) | (b2 >> 6)];
					outCharSpan[j + 3] = BASE64URL_ALPHABET[b2 & 0x3f];
					j += 4;
				}//for

				b0 = inByteSpan[LIMIT];

				outCharSpan[j] = BASE64URL_ALPHABET[b0 >> 2];
				outCharSpan[j + 1] = BASE64URL_ALPHABET[(b0 & 0x03) << 4];
			});//string.Create()
		}//ToBase64Url()

		/********************************************************
			c# code to generate the DecodeLookup table:
			Span<byte> decodeLookup = stackalloc byte[byte.MaxValue+1];
			decodeLookup.Fill(0xFF); // Initialize with invalid value

			for (var i = 0; i < BASE64URL_ALPHABET_STRING.Length; i++)
			{ decodeLookup[BASE64URL_ALPHABET_STRING[i]] = (byte)i;	}

			"[".Dump();
			for (var i = 0; i < decodeLookup.Length; ++i)
				{ Console.Write($"0x{decodeLookup[i]:X2},"); if ((i + 1) % 8 == 0) "".Dump(); }
			"]".Dump();
		*********************************************************/

		// Lookup table for decoding Base64Url characters to their byte values.
		static ReadOnlySpan<byte> DecodeLookup => [
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0x3E,0xFF,0xFF,
			0x34,0x35,0x36,0x37,0x38,0x39,0x3A,0x3B,
			0x3C,0x3D,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0x00,0x01,0x02,0x03,0x04,0x05,0x06,
			0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,
			0x0F,0x10,0x11,0x12,0x13,0x14,0x15,0x16,
			0x17,0x18,0x19,0xFF,0xFF,0xFF,0xFF,0x3F,
			0xFF,0x1A,0x1B,0x1C,0x1D,0x1E,0x1F,0x20,
			0x21,0x22,0x23,0x24,0x25,0x26,0x27,0x28,
			0x29,0x2A,0x2B,0x2C,0x2D,0x2E,0x2F,0x30,
			0x31,0x32,0x33,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
			0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF];

		/// <summary>Converts a Base64Url string back to a Guid.</summary>
		/// <param name="base64Url">The Base64Url string to convert.</param>
		/// <returns>The Guid represented by the Base64Url string (22 chars). Throws ArgumentException on invalid input.</returns>
		public static Guid FromBase64Url(this string base64Url)
		{
			[DoesNotReturn]
			static void Throw_ArgumentException(string message) => throw new ArgumentException(message, nameof(base64Url));

			if (base64Url.Length != BASE64URL_LENGTH)
				Throw_ArgumentException($"{nameof(base64Url)} must be exactly {BASE64URL_LENGTH_STRING} characters long");

			var decodeLookup = DecodeLookup;
			Guid guid = default;
			Span<byte> guidBytes = MemoryMarshal.CreateSpan<byte>(ref Unsafe.As<Guid, byte>(ref guid), GUID_LENGTH);

			ReadOnlySpan<char> input = base64Url.AsSpan();

			const int LENGTH_MOD_3 = 1; // 16 % 3 = 1;
			const int LIMIT = GUID_LENGTH - LENGTH_MOD_3; // 15

			int j = 0;
			byte b0, b1, b2, b3;

			// Decode groups of 4 characters to 3 bytes
			for (int i = 0; i < LIMIT; i += 3)
			{
				b0 = decodeLookup[(byte)input[j]];
				b1 = decodeLookup[(byte)input[j + 1]];
				b2 = decodeLookup[(byte)input[j + 2]];
				b3 = decodeLookup[(byte)input[j + 3]];

				if ((b0 | b1 | b2 | b3) >= 64) Throw_ArgumentException("Invalid Base64Url character");

				guidBytes[i] = (byte)((b0 << 2) | (b1 >> 4));
				guidBytes[i + 1] = (byte)((b1 << 4) | (b2 >> 2));
				guidBytes[i + 2] = (byte)((b2 << 6) | b3);
				j += 4;
			}//for

			// Handle the remaining byte (padding case)
			b0 = decodeLookup[(byte)input[j]];
			b1 = decodeLookup[(byte)input[j + 1]];

			if ((b0 | b1) >= 64) Throw_ArgumentException("Invalid Base64Url character");

			guidBytes[LIMIT] = (byte)((b0 << 2) | (b1 >> 4));
			return guid;
		}//FromBase64Url()

		/// <summary>
		/// Tries to convert a Base64Url string to a Guid.
		/// </summary>
		/// <param name="base64Url">The Base64Url char span to convert.</param>
		/// <param name="guid">When this method returns, contains the Guid value equivalent to the Base64Url string, if the conversion succeeded, or <see cref="Guid.Empty"/> if it failed.</param>
		/// <returns><c>true</c> if the string was converted successfully; otherwise, <c>false</c>. Does not throw.</returns>
		public static bool TryFromBase64Url(ReadOnlySpan<char> base64Url, out Guid guid)
		{
			guid = default;

			if (base64Url.Length != BASE64URL_LENGTH)
				return false;

			var decodeLookup = DecodeLookup;

			Span<byte> guidBytes = MemoryMarshal.CreateSpan<byte>(ref Unsafe.As<Guid, byte>(ref guid), GUID_LENGTH);

			const int LENGTH_MOD_3 = 1; // 16 % 3 = 1;
			const int LIMIT = GUID_LENGTH - LENGTH_MOD_3; // 15

			int j = 0;
			byte b0, b1, b2, b3;

			// Decode groups of 4 characters to 3 bytes
			for (int i = 0; i < LIMIT; i += 3)
			{
				b0 = decodeLookup[(byte)base64Url[j]];
				b1 = decodeLookup[(byte)base64Url[j + 1]];
				b2 = decodeLookup[(byte)base64Url[j + 2]];
				b3 = decodeLookup[(byte)base64Url[j + 3]];

				if ((b0 | b1 | b2 | b3) >= 64) return false;

				guidBytes[i] = (byte)((b0 << 2) | (b1 >> 4));
				guidBytes[i + 1] = (byte)((b1 << 4) | (b2 >> 2));
				guidBytes[i + 2] = (byte)((b2 << 6) | b3);
				j += 4;
			}//for

			// Handle the remaining byte (padding case)
			b0 = decodeLookup[(byte)base64Url[j]];
			b1 = decodeLookup[(byte)base64Url[j + 1]];

			if ((b0 | b1) >= 64) return false;

			guidBytes[LIMIT] = (byte)((b0 << 2) | (b1 >> 4));
			return true;
		}//TryFromBase64Url()

	}//class GuidExtensions
}//ns
