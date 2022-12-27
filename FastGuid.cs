using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SecurityDriven
{
	/// <summary>Represents a globally unique identifier (GUID).</summary>
	public static class FastGuid
	{
		// Copyright (c) 2022 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		const int GUIDS_PER_THREAD = 256; //keep it power-of-2
		const int GUID_SIZE_IN_BYTES = 16;
		const int DATETIME_SIZE_IN_BYTES = 8;

		struct Container
		{
			public Guid[] _guids;
			public int _idx;
		}

		[ThreadStatic] static Container ts_data;

		/// <summary>
		/// Initializes a new instance of the <see cref="Guid"/> structure.
		/// </summary>
		/// <returns>A new <see cref="Guid"/> struct.</returns>
		/// <remarks>Faster alternative to <see cref="Guid.NewGuid"/>.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewGuid()
		{
			ref var guid0 = ref MemoryMarshal.GetArrayDataReference(ts_data._guids ??= GC.AllocateUninitializedArray<Guid>(GUIDS_PER_THREAD));
			int idx = ts_data._idx++ & (GUIDS_PER_THREAD - 1);
			if (idx == 0)
			{
				RandomNumberGenerator.Fill(
					MemoryMarshal.CreateSpan<byte>(ref Unsafe.As<Guid, byte>(ref guid0), GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES));
			}

			var guid = Unsafe.Add(ref guid0, idx);
			Unsafe.Add(ref guid0, idx) = default; // prevents Guid leakage
			return guid;
		}//NewGuid()

		/// <summary>
		/// Returns new Guid well-suited to be used as a SQL-Server clustered key.
		/// Guid structure is [8 random bytes][8 bytes of SQL-Server-ordered DateTime.UtcNow].
		/// Each Guid should be sequential accross 100-nanosecond UtcNow precision limits.
		/// 64-bit cryptographic randomness provides reasonable unguessability and protection against online brute-force attacks.
		/// </summary>
		/// <returns>Guid for SQL-Server clustered key.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewSqlServerGuid()
		{
			Guid guid = FastGuid.NewGuid();
			Span<byte> guidSpan = MemoryMarshal.CreateSpan(ref Unsafe.As<Guid, byte>(ref guid), GUID_SIZE_IN_BYTES);

			DateTime utcNow = DateTime.UtcNow;
			Span<byte> ticksSpan = MemoryMarshal.CreateSpan(ref Unsafe.As<DateTime, byte>(ref utcNow), DATETIME_SIZE_IN_BYTES);

			// based on Microsoft SqlGuid.cs
			// https://github.com/microsoft/referencesource/blob/5697c29004a34d80acdaf5742d7e699022c64ecd/System.Data/System/Data/SQLTypes/SQLGuid.cs

			guidSpan[10] = ticksSpan[7];
			guidSpan[11] = ticksSpan[6];
			guidSpan[12] = ticksSpan[5];
			guidSpan[13] = ticksSpan[4];
			guidSpan[14] = ticksSpan[3];
			guidSpan[15] = ticksSpan[2];

			guidSpan[08] = ticksSpan[1];
			guidSpan[09] = ticksSpan[0];

			return guid;
		}// NewSqlServerGuid()
	}//class FastGuid
}//ns