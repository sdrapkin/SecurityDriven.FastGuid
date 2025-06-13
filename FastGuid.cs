using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SecurityDriven
{
	/// <summary>
	/// Provides high-performance, thread-safe methods for generating cryptographically strong GUIDs,
	/// database-optimized GUIDs (SQL Server and PostgreSQL formats for clustered keys and index performance),
	/// random byte filling, and random string generation using various alphabets (Base16, Base32, Base64, Base64Url).
	/// </summary>
	public static partial class FastGuid
	{
		// Copyright (c) 2025 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		const int GUIDS_PER_THREAD = 1 << 8; // 256 (keep it power-of-2)
		const int GUID_SIZE_IN_BYTES = 16;

		[StructLayout(LayoutKind.Explicit, Size = GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES, Pack = 1)]
		struct Guids
		{
			[FieldOffset(0)]
			Guid guid0;

			[FieldOffset(0)]
			byte byte0;

			public Span<Guid> AsSpanGuid() => MemoryMarshal.CreateSpan(ref guid0, GUIDS_PER_THREAD);
			public Span<byte> AsSpanByte() => MemoryMarshal.CreateSpan(ref byte0, GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES);
		}//Guids

		struct Container
		{
			public Guids _guids; // do not move, should be 1st
			public byte _idx; // wraps around on 256 (GUIDS_PER_THREAD)
		}//Container

		[ThreadStatic] static Container ts_container; // ts stands for "ThreadStatic"

		/// <summary>Initializes a new instance of the <see cref="Guid"/> structure.</summary>
		/// <returns>A new <see cref="Guid"/> struct.</returns>
		/// <remarks>Faster alternative to <see cref="Guid.NewGuid"/>.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewGuid()
		{
			ref Container container = ref ts_container;
			byte idx = container._idx++;
			if (idx == 0)
			{
				FillContainer(ref container);
			}//if
			Span<Guid> span = container._guids.AsSpanGuid();
			Guid guid = span[idx];
			span[idx] = default;
			return guid;
		}//NewGuid()

		const int MAX_BYTES_TO_FILL_VIA_GUIDS = 512;

		/// <summary>
		/// Fills a span with cryptographically strong random bytes.
		/// </summary>
		/// <param name="data">The span to fill with cryptographically strong random bytes.</param>
		/// <remarks>If <paramref name="data"/> is larger than 512 bytes, RandomNumberGenerator.Fill(data) is used instead.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Fill(Span<byte> data)
		{
			int dataLength = data.Length;
			if (dataLength == 0) return;
			if (dataLength > MAX_BYTES_TO_FILL_VIA_GUIDS)
			{
				RandomNumberGenerator.Fill(data); return;
			}//if

			int lengthInGuids = dataLength >>> 4; // faster assembly than "dataLength / GUID_SIZE_IN_BYTES"

			ref Container container = ref ts_container;
			byte idx = container._idx;
			Span<Guid> guidsAsSpan = container._guids.AsSpanGuid();

			if (lengthInGuids > 0)
			{
				Span<Guid> dataAsGuids = MemoryMarshal.CreateSpan<Guid>(ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data)), lengthInGuids);
				for (int i = 0; i < lengthInGuids; ++i)
				{
					if (idx == 0) FillContainer(ref container);

					dataAsGuids[i] = guidsAsSpan[idx];
					guidsAsSpan[idx++] = default;
				}//for
			}//if

			lengthInGuids *= GUID_SIZE_IN_BYTES; // same assembly as "lengthInGuids << 4"
			int remainingBytes = dataLength - lengthInGuids;
			if (remainingBytes > 0)
			{
				if (idx == 0) FillContainer(ref container);

				Span<byte> guidAsByteSpan = MemoryMarshal.CreateSpan(ref Unsafe.As<Guid, byte>(ref guidsAsSpan[idx]), remainingBytes);
				guidAsByteSpan.TryCopyTo(data.Slice(lengthInGuids));
				guidsAsSpan[idx++] = default;
			}//if
			container._idx = idx;
		}//Fill()

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void FillContainer(ref Container container) => RandomNumberGenerator.Fill(container._guids.AsSpanByte());
	}//class FastGuid
}//ns
