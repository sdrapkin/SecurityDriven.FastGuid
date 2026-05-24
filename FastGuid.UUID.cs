using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SecurityDriven
{
	public static partial class FastGuid
	{
		// Copyright (c) 2026 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		/// <summary>
		/// Provides factory and utility methods for working with Universally Unique Identifiers (UUIDs), 
		/// specifically focusing on RFC-9562 compliant Version-7 formats.
		/// </summary>
		public static class UUID
		{
			struct GuidStruct
			{
				public int a;
				public short b;
				public short c;
				public byte d;
				public byte e;
				public byte f;
				public byte g;
				public byte h;
				public byte i;
				public byte j;
				public byte k;
			}//GuidStruct

			const ushort Version7Mask = 0xF000;
			const ushort Version7Data = 0x7000;

			const byte Variant10xxMask = 0xC0;
			const byte Variant10xxData = 0x80;

			const int DaysTo1970 = 719_162; // (new DateTime(1970, 1, 1) - default(DateTime)).TotalDays
			const long UnixEpochTicks = DaysTo1970 * TimeSpan.TicksPerDay;
			const long UnixEpochMilliseconds = UnixEpochTicks / TimeSpan.TicksPerMillisecond;

			/// <summary>Creates a new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</summary>
			/// <returns>A new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</returns>
			/// <remarks>
			///     <para>Faster alternative to System.Guid.CreateVersion7().</para>
			///     <para>This uses <see cref="DateTimeOffset.UtcNow" /> to determine the Unix Epoch timestamp source.</para>
			///     <para>This seeds the rand_a and rand_b sub-fields with random data.</para>
			/// </remarks>
			public static Guid CreateVersion7() => CreateVersion7(DateTime.UtcNow.Ticks);

			/// <summary>Creates a new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</summary>
			/// <param name="timestamp">The date time offset used to determine the Unix Epoch timestamp.</param>
			/// <returns>A new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</returns>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="timestamp" /> represents an offset prior to <see cref="DateTimeOffset.UnixEpoch" />.</exception>
			/// <remarks>
			///     <para>Faster alternative to System.Guid.CreateVersion7(DateTimeOffset).</para>
			///     <para>This seeds the rand_a and rand_b sub-fields with random data.</para>
			/// </remarks>
			public static Guid CreateVersion7(DateTimeOffset timestamp) => CreateVersion7(ticks: timestamp.UtcTicks);


			/// <summary>Creates a new <see cref="Guid"/> according to RFC 9562, following the Version 7 format.</summary>
			/// <param name="utcTimestamp">The timestamp used to determine the Unix Epoch source.</param>
			/// <returns>A new <see cref="Guid"/> according to RFC 9562, following the Version 7 format.</returns>
			/// <remarks>
			///     <para>Convenience method.</para>
			///     <para>This method seeds the rand_a and rand_b sub-fields with random data.</para>
			/// </remarks>
			public static Guid CreateVersion7(DateTime utcTimestamp) => CreateVersion7(ticks: utcTimestamp.Ticks);

			static Guid CreateVersion7(long ticks)
			{
				Guid guidv7 = FastGuid.NewGuid();
				long unix_ts_ms = (long)((ulong)ticks / TimeSpan.TicksPerMillisecond) - UnixEpochMilliseconds;
				if (unix_ts_ms < 0) ThrowNegative(nameof(ticks), unix_ts_ms);

				ref var guidv7Ref = ref Unsafe.As<Guid, GuidStruct>(ref guidv7);

				guidv7Ref.a = (int)(unix_ts_ms >> 16);
				guidv7Ref.b = (short)(unix_ts_ms);

				guidv7Ref.c = (short)((guidv7Ref.c & ~Version7Mask) | Version7Data);
				guidv7Ref.d = (byte)((guidv7Ref.d & ~Variant10xxMask) | Variant10xxData);

				return guidv7;

				[DoesNotReturn]
				static void ThrowNegative(string paramName, long val) =>
					throw new ArgumentOutOfRangeException(paramName: paramName, actualValue: val, message: $"{paramName} ('{val}') must be a non-negative value.");
			}//CreateVersion7()


			/// <summary>Extracts the UTC DateTime from a Version-7 Guid.</summary>
			/// <param name="guidv7">The Version 7 Guid from which to extract the timestamp.</param>
			/// <returns>DateTime timestamp with 1 millisecond precision.</returns>
			/// <exception cref="ArgumentException">Thrown when the provided <paramref name="guidv7"/> is not a valid Version 7 format.</exception>
			/// <remarks>A zero-allocation, high-performance alternative to parsing raw bytes or string representations manually.</remarks>
			public static DateTime ExtractDateTimeV7(Guid guidv7)
			{
				var ok = TryExtractTicksV7(guidv7, out var ticks);
				if (!ok)
				{
					throw new ArgumentException("Guid is not a valid Version-7 Guid.", nameof(guidv7));
				}
				return new DateTime(ticks, DateTimeKind.Utc);
			}//ExtractDateTimeV7()

			/// <summary>Tries to extract a UTC timestamp from a Version 7 Guid.</summary>
			/// <param name="guidv7">The Version 7 Guid.</param>
			/// <param name="timestamp">The extracted UTC timestamp if the operation succeeds.</param>
			/// <returns>true if the timestamp was successfully extracted; otherwise, false.</returns>
			/// <remarks>A non-throwing, zero-allocation alternative to manual sub-array extraction techniques.</remarks>			
			public static bool TryExtractDateTimeV7(Guid guidv7, out DateTime timestamp)
			{
				var ok = TryExtractTicksV7(guidv7, out var ticks);
				if (ok)
				{
					timestamp = new DateTime(ticks, DateTimeKind.Utc);
					return true;
				}

				timestamp = default;
				return false;
			}//TryExtractDateTimeV7()

			/// <summary>Tries to extract the UTC ticks from a Version-7 Guid. Does not throw.</summary>
			/// <returns>True if extraction is successful, false otherwise.</returns>
			static bool TryExtractTicksV7(Guid guidv7, out long ticks)
			{
				ref readonly GuidStruct guidRef = ref Unsafe.As<Guid, GuidStruct>(ref guidv7);

				long unix_ts_ms = ((long)(uint)guidRef.a << 16) | (ushort)guidRef.b;
				long total_ms = unix_ts_ms + UnixEpochMilliseconds;
				ticks = total_ms * TimeSpan.TicksPerMillisecond;

				// Validate the version and that ticks are >= 0
				if ((guidRef.c & Version7Mask) != Version7Data || ticks < 0)
				{
					ticks = default;
					return false;
				}

				return true;
			}//TryExtractTicksV7()

		}//static class UUID
	}//class FastGuid
}//ns
