using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SecurityDriven
{
	/// <summary>Represents a globally unique identifier (GUID).</summary>
	public static class FastGuid
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

		/// <summary>
		/// Returns new Guid optimized for use as a SQL-Server clustered key.
		/// Guid structure is [8 random bytes][8 bytes of SQL-Server-ordered DateTime.UtcNow].
		/// Each Guid should be sequential within 100-nanosecond UtcNow precision limits.
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

			// based on Microsoft SqlGuid.cs
			// https://github.com/microsoft/referencesource/blob/5697c29004a34d80acdaf5742d7e699022c64ecd/System.Data/System/Data/SQLTypes/SQLGuid.cs

			ref var guidStruct = ref Unsafe.As<Guid, (long, ulong)>(ref guid);
			ref var ticksStruct = ref Unsafe.As<DateTime, ulong>(ref timestampUtc);

			guidStruct.Item2 = BinaryPrimitives.ReverseEndianness(BitOperations.RotateRight(ticksStruct, 16));

			return guid;
		}//NewSqlServerGuid(DateTime)

		/// <summary>
		/// Returns new Guid optimized for use as a PostgreSQL index key.
		/// Guid structure is [8 bytes of PostgreSQL-ordered timestampUtc][8 random bytes].
		/// Each Guid should be sequential within 100-nanosecond UtcNow precision limits.
		/// 64-bit cryptographic randomness adds uniqueness for timestamp collisions and provides reasonable unguessability and protection against online brute-force attacks.
		/// Designed for <see href="https://www.npgsql.org">Npgsql</see>, which auto-converts to big-endian wire format, resulting in correct PG::uuid value.
		/// Explicit <see href="https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-7">UUID-v7-like</see> big-endian byte conversion of returned Guid should use
		/// .ToByteArray(bigEndian:true) or .TryWriteBytes(destinationSpan, bigEndian:true).
		/// </summary>
		/// <returns>Guid for PostgreSQL index key.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewPostgreSqlGuid() => NewPostgreSqlGuid(DateTime.UtcNow);

		/// <summary>
		/// Returns new Guid optimized for use as a PostgreSQL index key.
		/// Guid structure is [8 bytes of PostgreSQL-ordered timestampUtc][8 random bytes].
		/// 64-bit cryptographic randomness adds uniqueness for timestamp collisions and provides reasonable unguessability and protection against online brute-force attacks.
		/// Designed for <see href="https://www.npgsql.org">Npgsql</see>, which auto-converts to big-endian wire format, resulting in correct PG::uuid value.
		/// Explicit <see href="https://www.rfc-editor.org/rfc/rfc9562.html#name-uuid-version-7">UUID-v7-like</see> big-endian byte conversion of returned Guid should use
		/// .ToByteArray(bigEndian:true) or .TryWriteBytes(destinationSpan, bigEndian:true).
		/// </summary>
		/// <param name="timestampUtc">UTC timestamp.</param>
		/// <returns>Guid for PostgreSQL index key.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewPostgreSqlGuid(DateTime timestampUtc)
		{
			Guid guid = FastGuid.NewGuid();

			// PostgreSQL compares GUIDs as byte arrays, using memcmp
			// https://doxygen.postgresql.org/uuid_8c.html#aae2aef5e86c79c563f02a5cee13d1708

			ref var guidStruct = ref Unsafe.As<Guid, (int, short, short)>(ref guid);
			ref var ticksStruct = ref Unsafe.As<DateTime, (int, int)>(ref timestampUtc);
			ref var shortStruct = ref Unsafe.As<int, (short, short)>(ref ticksStruct.Item1);

			guidStruct.Item1 = ticksStruct.Item2;
			guidStruct.Item2 = shortStruct.Item2;
			guidStruct.Item3 = shortStruct.Item1;

			return guid;
		}//NewPostgreSqlGuid()

		static readonly Guid AllBitsSet = new Guid(uint.MaxValue, ushort.MaxValue, ushort.MaxValue,
			byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

		/// <summary>Helper methods for Guids generated by <see cref="NewSqlServerGuid()"/>.</summary>
		public static class SqlServer
		{
			/// <summary>Extracts SqlServer Guid creation timestamp (UTC). Full <see cref="DateTime.UtcNow"/> precision.</summary>
			/// <param name="guid">Guid generated by <see cref="NewSqlServerGuid()"/>.</param>
			/// <returns>SqlServer Guid creation timestamp.</returns>
			public static DateTime GetTimestamp(Guid guid)
			{
				ref var guidStruct = ref Unsafe.As<Guid, (long, ulong)>(ref guid);
				var ulongStruct = BinaryPrimitives.ReverseEndianness(BitOperations.RotateRight(guidStruct.Item2, 16));
				return Unsafe.As<ulong, DateTime>(ref ulongStruct);
			}// GetTimestamp()

			/// <summary>Returns the *smallest* Guid for a given timestamp (useful for time-based database range searches).</summary>
			public static Guid MinGuidForTimestamp(DateTime timestampUtc)
			{
				Guid guid = default;
				ref var guidStruct = ref Unsafe.As<Guid, (long, ulong)>(ref guid);
				ref var ticksStruct = ref Unsafe.As<DateTime, ulong>(ref timestampUtc);

				guidStruct.Item2 = BinaryPrimitives.ReverseEndianness(BitOperations.RotateRight(ticksStruct, 16));

				return guid;
			}// MinGuidForTimestamp()

			/// <summary>Returns the *largest* Guid for a given timestamp (useful for time-based database range searches).</summary>
			public static Guid MaxGuidForTimestamp(DateTime timestampUtc)
			{
				Guid guid = AllBitsSet;
				ref var guidStruct = ref Unsafe.As<Guid, (long, ulong)>(ref guid);
				ref var ticksStruct = ref Unsafe.As<DateTime, ulong>(ref timestampUtc);

				guidStruct.Item2 = BinaryPrimitives.ReverseEndianness(BitOperations.RotateRight(ticksStruct, 16));

				return guid;
			}// MaxGuidForTimestamp()
		}// static class SqlServer

		/// <summary>Helper methods for Guids generated by <see cref="NewPostgreSqlGuid()"/>.</summary>
		public static class PostgreSql
		{
			/// <summary>Extracts PostgreSql Guid creation timestamp (UTC). Full <see cref="DateTime.UtcNow"/> precision.</summary>
			/// <param name="guid">Guid generated by <see cref="NewPostgreSqlGuid()"/>.</param>
			/// <returns>PostgreSql Guid creation timestamp.</returns>
			public static DateTime GetTimestamp(Guid guid)
			{
				ref var guidTimestamp = ref Unsafe.As<Guid, (uint, ushort, ushort)>(ref guid);
				ulong ticksStruct = ((ulong)guidTimestamp.Item1 << 32) | ((uint)guidTimestamp.Item2 << 16) | guidTimestamp.Item3;

				return Unsafe.As<ulong, DateTime>(ref ticksStruct);
			}// GetTimestamp()

			/// <summary>Returns the *smallest* Guid for a given timestamp (useful for time-based database range searches).</summary>
			public static Guid MinGuidForTimestamp(DateTime timestampUtc)
			{
				Guid guid = default;
				ref var guidStruct = ref Unsafe.As<Guid, (int, short, short)>(ref guid);
				ref var ticksStruct = ref Unsafe.As<DateTime, (int, int)>(ref timestampUtc);
				ref var shortStruct = ref Unsafe.As<int, (short, short)>(ref ticksStruct.Item1);

				guidStruct.Item1 = ticksStruct.Item2;
				guidStruct.Item2 = shortStruct.Item2;
				guidStruct.Item3 = shortStruct.Item1;

				return guid;
			}// MinGuidForTimestamp()

			/// <summary>Returns the *largest* Guid for a given timestamp (useful for time-based database range searches).</summary>
			public static Guid MaxGuidForTimestamp(DateTime timestampUtc)
			{
				Guid guid = AllBitsSet;
				ref var guidStruct = ref Unsafe.As<Guid, (int, short, short)>(ref guid);
				ref var ticksStruct = ref Unsafe.As<DateTime, (int, int)>(ref timestampUtc);
				ref var shortStruct = ref Unsafe.As<int, (short, short)>(ref ticksStruct.Item1);

				guidStruct.Item1 = ticksStruct.Item2;
				guidStruct.Item2 = shortStruct.Item2;
				guidStruct.Item3 = shortStruct.Item1;

				return guid;
			}// MaxGuidForTimestamp()
		}// static class PostgreSql

	}//class FastGuid
}//ns