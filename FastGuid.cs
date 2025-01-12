using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SecurityDriven
{
	/// <summary>Represents a globally unique identifier (GUID).</summary>
	public static class FastGuid
	{
		// Copyright (c) 2024 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		const int GUIDS_PER_THREAD = 1 << 8; // 256 (keep it power-of-2)
		const int GUID_SIZE_IN_BYTES = 16;

		[StructLayout(LayoutKind.Sequential, Size = GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES, Pack = 1)]
		struct Guids
		{
			Guid guid0;

			public Span<Guid> AsSpanGuid() => MemoryMarshal.CreateSpan(ref guid0, GUIDS_PER_THREAD);
		}//Guids

		struct Container
		{
			public Guids _guids; // do not move, should be 1st
			public byte _idx; // wraps around on 256 (GUIDS_PER_THREAD)
		}//Container

		[ThreadStatic] static Container ts_container; //ts stands for "ThreadStatic"

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

			int lengthInGuids = dataLength >> 4;

			ref Container container = ref ts_container;
			byte idx = container._idx;
			Span<Guid> guidsAsSpan = container._guids.AsSpanGuid();
			Span<Guid> dataAsGuids = MemoryMarshal.CreateSpan<Guid>(ref Unsafe.As<byte, Guid>(ref data[0]), lengthInGuids);

			for (int i = 0; i < lengthInGuids; ++i)
			{
				if (idx == 0) FillContainer(ref container);

				dataAsGuids[i] = guidsAsSpan[idx];
				guidsAsSpan[idx++] = default;
			}//for

			int remainingBytes = dataLength - (lengthInGuids << 4);
			if (remainingBytes > 0)
			{
				if (idx == 0) FillContainer(ref container);

				Span<byte> byteSpan = MemoryMarshal.CreateSpan(ref Unsafe.As<Guid, byte>(ref guidsAsSpan[idx]), GUID_SIZE_IN_BYTES).Slice(0, remainingBytes);
				byteSpan.CopyTo(data.Slice(dataLength - remainingBytes));
				guidsAsSpan[idx++] = default;
			}//if
			container._idx = idx;
		}//Fill()

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void FillContainer(ref Container container)
		{
			RandomNumberGenerator.Fill(
						MemoryMarshal.CreateSpan<byte>(ref Unsafe.As<Container, byte>(ref container), GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES));
		}//FillContainer()

		/// <summary>
		/// Returns new Guid optimized for use as a SQL-Server clustered key.
		/// Guid structure is [8 random bytes][8 bytes of SQL-Server-ordered DateTime.UtcNow].
		/// Each Guid should be sequential across 100-nanosecond UtcNow precision limits.
		/// 64-bit cryptographic randomness adds uniqueness for timestamp collisions and provides reasonable unguessability and protection against online brute-force attacks.
		/// </summary>
		/// <returns>Guid for SQL-Server clustered key.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewSqlServerGuid() => NewSqlServerGuid(DateTime.UtcNow);

		/// <summary>
		/// Returns new Guid optimized for use as a SQL-Server clustered key.
		/// Guid structure is [8 random bytes][8 bytes of SQL-Server-ordered timestampUtc].
		/// 64-bit cryptographic randomness adds uniqueness for timestamp collisions and provides reasonable unguessability and protection against online brute-force attacks.
		/// </summary>
		/// <param name="timestampUtc">UTC timestamp.</param>
		/// <returns>Guid for SQL-Server clustered key.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewSqlServerGuid(DateTime timestampUtc)
		{
			Guid guid = FastGuid.NewGuid();
			ref var guidStruct = ref Unsafe.As<Guid, (LongStruct LONG0, LongStruct LONG1)>(ref guid);

			ref var ticksStruct = ref Unsafe.As<DateTime, LongStruct>(ref timestampUtc);

			// based on Microsoft SqlGuid.cs
			// https://github.com/microsoft/referencesource/blob/5697c29004a34d80acdaf5742d7e699022c64ecd/System.Data/System/Data/SQLTypes/SQLGuid.cs

			guidStruct.LONG1.B2 = ticksStruct.B7;
			guidStruct.LONG1.B3 = ticksStruct.B6;
			guidStruct.LONG1.B4 = ticksStruct.B5;
			guidStruct.LONG1.B5 = ticksStruct.B4;
			guidStruct.LONG1.B6 = ticksStruct.B3;
			guidStruct.LONG1.B7 = ticksStruct.B2;

			guidStruct.LONG1.B0 = ticksStruct.B1;
			guidStruct.LONG1.B1 = ticksStruct.B0;

			return guid;
		}// NewSqlServerGuid(DateTime)

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(long))]
		struct LongStruct
		{
			public byte B0;
			public byte B1;
			public byte B2;
			public byte B3;
			public byte B4;
			public byte B5;
			public byte B6;
			public byte B7;
		}//struct LongStruct

		/// <summary>Helper methods for Guids generated by <see cref="NewSqlServerGuid"/>.</summary>
		public static class SqlServer
		{
			/// <summary>Extracts SqlServer guid creation timestamp (UTC). Full <see cref="DateTime.UtcNow"/> precision.</summary>
			/// <param name="guid">Guid generated by <see cref="NewSqlServerGuid"/>.</param>
			/// <returns>SqlServer Guid creation timestamp.</returns>
			public static DateTime GetTimestamp(Guid guid)
			{
				ref (LongStruct, LongStruct) longStructs = ref Unsafe.As<Guid, (LongStruct, LongStruct)>(ref guid);
				longStructs.Item1.B0 = longStructs.Item2.B1;
				longStructs.Item1.B1 = longStructs.Item2.B0;
				longStructs.Item1.B2 = longStructs.Item2.B7;
				longStructs.Item1.B3 = longStructs.Item2.B6;
				longStructs.Item1.B4 = longStructs.Item2.B5;
				longStructs.Item1.B5 = longStructs.Item2.B4;
				longStructs.Item1.B6 = longStructs.Item2.B3;
				longStructs.Item1.B7 = longStructs.Item2.B2;

				return Unsafe.As<LongStruct, DateTime>(ref longStructs.Item1);
			}// GetTimestamp()

			/// <summary>Returns the *smallest* Guid for a given timestamp (useful for time-based database range searches).</summary>
			public static Guid MinGuidForTimestamp(DateTime timestampUtc)
			{
				Guid guid = default;
				ref (ulong, LongStruct) guidLSRef = ref Unsafe.As<Guid, (ulong, LongStruct)>(ref guid);
				ref LongStruct tsLSRef = ref Unsafe.As<DateTime, LongStruct>(ref timestampUtc);

				guidLSRef.Item2.B2 = tsLSRef.B7;
				guidLSRef.Item2.B3 = tsLSRef.B6;
				guidLSRef.Item2.B4 = tsLSRef.B5;
				guidLSRef.Item2.B5 = tsLSRef.B4;
				guidLSRef.Item2.B6 = tsLSRef.B3;
				guidLSRef.Item2.B7 = tsLSRef.B2;
				guidLSRef.Item2.B0 = tsLSRef.B1;
				guidLSRef.Item2.B1 = tsLSRef.B0;

				return guid;
			}// MinGuidForTimestamp()

			/// <summary>Returns the *largest* Guid for a given timestamp (useful for time-based database range searches).</summary>
			public static Guid MaxGuidForTimestamp(DateTime timestampUtc)
			{
				Guid guid = default;
				ref (ulong, LongStruct) guidLSRef = ref Unsafe.As<Guid, (ulong, LongStruct)>(ref guid);
				ref LongStruct tsLSRef = ref Unsafe.As<DateTime, LongStruct>(ref timestampUtc);

				guidLSRef.Item1 = ulong.MaxValue;

				guidLSRef.Item2.B2 = tsLSRef.B7;
				guidLSRef.Item2.B3 = tsLSRef.B6;
				guidLSRef.Item2.B4 = tsLSRef.B5;
				guidLSRef.Item2.B5 = tsLSRef.B4;
				guidLSRef.Item2.B6 = tsLSRef.B3;
				guidLSRef.Item2.B7 = tsLSRef.B2;
				guidLSRef.Item2.B0 = tsLSRef.B1;
				guidLSRef.Item2.B1 = tsLSRef.B0;

				return guid;
			}// MaxGuidForTimestamp()
		}// inner class SqlServer
	}//class FastGuid
}//ns