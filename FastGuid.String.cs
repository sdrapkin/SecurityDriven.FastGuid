using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SecurityDriven
{
	public static partial class FastGuid
	{
		// Copyright (c) 2026 Stan Drapkin  
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid  

		/// <summary>Generates random text strings using Base16/Base32/Base64/Base64Url alphabets.</summary>  
		public static class StringGen
		{
			// RFC 4648 alphabets  
			const string Base16 = "0123456789ABCDEF";
			const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
			const string Base32c = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford Base32
			const string Base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			const string Base64Url = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

			// Repeat the alphabets to 256 bytes  
			const string Base16_256 =
				Base16 + Base16 + Base16 + Base16 +
				Base16 + Base16 + Base16 + Base16 +
				Base16 + Base16 + Base16 + Base16 +
				Base16 + Base16 + Base16 + Base16;

			const string Base32_256 =
				Base32 + Base32 + Base32 + Base32 +
				Base32 + Base32 + Base32 + Base32;

			const string Base32c_256 =
				Base32c + Base32c + Base32c + Base32c +
				Base32c + Base32c + Base32c + Base32c;

			const string Base64_256 = Base64 + Base64 + Base64 + Base64;
			const string Base64Url_256 = Base64Url + Base64Url + Base64Url + Base64Url;

			static string TextAlphabet256(int length, string alphabet256) =>
				string.Create(length, alphabet256,
					static (charSpan, _alphabet256) =>
					{
						Span<byte> byteSpan = MemoryMarshal.AsBytes(charSpan).Slice(charSpan.Length);
						FastGuid.Fill(byteSpan);

						// Establish direct zero-bounds references
						ref char charSpan0Ref = ref MemoryMarshal.GetReference(charSpan);
						ref byte byteSpan0Ref = ref MemoryMarshal.GetReference(byteSpan);
						ref char alphabet256Span0Ref = ref MemoryMarshal.GetReference(_alphabet256.AsSpan());

						for (int i = 0; i < charSpan.Length; ++i)
						{
							// charSpan[i] = _alphabet256[byteSpan[i]];
							Unsafe.Add(ref charSpan0Ref, i) = Unsafe.Add(ref alphabet256Span0Ref, (nint)Unsafe.Add(ref byteSpan0Ref, i)); // strips out JIT-inserted bound-checks

						}
					});

			/// <summary>Generates a random text string using Base16 alphabet.</summary>  
			public static string Text16(int length) => TextAlphabet256(length, Base16_256);

			/// <summary>Generates a random text string using Base32 alphabet.</summary>  
			public static string Text32(int length) => TextAlphabet256(length, Base32_256);

			/// <summary>Generates a random text string using Base32 Crockford alphabet.</summary>  
			public static string Text32c(int length) => TextAlphabet256(length, Base32c_256);

			/// <summary>Generates a random text string using Base64 alphabet.</summary>  
			public static string Text64(int length) => TextAlphabet256(length, Base64_256);

			/// <summary>Generates a random text string using Base64Url alphabet.</summary>  
			public static string Text64Url(int length) => TextAlphabet256(length, Base64Url_256);

			/* TextDigits is implemented separately since 250 
			 * is the largest multiple of 10 that fits in a byte,
			 * ensuring unbiased generation by rejecting only
			 * 6 out of 256 byte values (2.3% rejection rate). */

			const string Digits10 = "0123456789";
			const string Digits10_250 =
				Digits10 + Digits10 + Digits10 + Digits10 + Digits10 +
				Digits10 + Digits10 + Digits10 + Digits10 + Digits10 +
				Digits10 + Digits10 + Digits10 + Digits10 + Digits10 +
				Digits10 + Digits10 + Digits10 + Digits10 + Digits10 +
				Digits10 + Digits10 + Digits10 + Digits10 + Digits10; // 250 chars

			/// <summary>Generates an unbiased random decimal digit string.</summary>
			public static string TextDigits(int length) =>
				string.Create(length, Digits10_250, static (charSpan, _digits10_250) =>
				{
					const int BYTESPAN_LEN = 64;
					Span<byte> byteSpan = stackalloc byte[BYTESPAN_LEN];
					FastGuid.Fill(byteSpan);

					ref char charSpan0Ref = ref MemoryMarshal.GetReference(charSpan);
					ref byte byteSpan0Ref = ref MemoryMarshal.GetReference(byteSpan);
					ref char digits10_250Span0Ref = ref MemoryMarshal.GetReference(_digits10_250.AsSpan());

					int byteIdx = 0, charIdx = 0;
					while (charIdx < charSpan.Length)
					{
						if (byteIdx == BYTESPAN_LEN)
						{
							FastGuid.Fill(byteSpan);
							byteIdx = 0;
						}
						byte b = Unsafe.Add(ref byteSpan0Ref, byteIdx++);
						if (b < 250)
							Unsafe.Add(ref charSpan0Ref, charIdx++) = Unsafe.Add(ref digits10_250Span0Ref, (nint)b);
					}
				});
		}// static class StringGen  
	}//class FastGuid  
}//ns
