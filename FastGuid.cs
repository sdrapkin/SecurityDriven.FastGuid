using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace SecurityDriven
{
	/// <summary>
	/// Provides high-performance, thread-safe methods for generating cryptographically strong GUIDs,
	/// database-optimized GUIDs (SQL Server and PostgreSQL formats for clustered keys and index performance),
	/// random byte filling, and random string generation using various alphabets (Base16, Base32, Base64, Base64Url).
	/// </summary>
	public static partial class FastGuid
	{
		// Copyright (c) 2026 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		const int GUIDS_PER_THREAD = 1 << 8; // 256 (keep it power-of-2)
		const int GUID_SIZE_IN_BYTES = 16;

		[StructLayout(LayoutKind.Explicit, Size = GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES, Pack = 1)]
		struct Guids
		{
			[FieldOffset(0)] Guid guid0;
			[FieldOffset(0)] byte byte0;

			public Span<Guid> AsSpanGuid() => MemoryMarshal.CreateSpan(ref guid0, GUIDS_PER_THREAD);
			public Span<byte> AsSpanByte() => MemoryMarshal.CreateSpan(ref byte0, GUIDS_PER_THREAD * GUID_SIZE_IN_BYTES);
		}//Guids

		struct Container
		{
			public Guids _guids; // do not move, should be 1st
			public int _epoch; // thread-local copy of s_epoch; zero-initialized
			public byte _idx; // wraps around on 256 (GUIDS_PER_THREAD)
		}//Container

		[ThreadStatic] static Container ts_container; // ts stands for "ThreadStatic"

		/*
			Global epoch. Written only by Reset() via Interlocked.Increment (full memory barrier).
			Read by every thread on every NewGuid() and Fill() call, but:
			 -	In steady state (no Reset in flight) the cache line is in Shared state
				on every core simultaneously → L1 hit, zero coherency traffic, zero contention.
			 -	After Reset(), one invalidation wave, then back to Shared (negligible cost).
			Declared volatile so the JIT cannot hoist/cache the read in a register across calls.
		*/
		static volatile int s_epoch; // = 0; matches all threads' _epoch = 0 on first use

		// -----------------------------------------------------------------
		// Public Reset() — call this in your RegisterAfterRestore hook (AWS SnapStart).
		//
		// Effect: the next NewGuid() or Fill() call on ANY thread — including
		// threads already in the pool with stale snapshot buffers - will
		// detect the epoch mismatch, discard its buffer, and refill from
		// RandomNumberGenerator before returning a single byte of output.
		// -----------------------------------------------------------------
		/// <summary>
		/// Invalidates all thread-local GUID buffers. Must be called after every
		/// AWS Lambda SnapStart restore (via RegisterAfterRestore) to guarantee
		/// that no pre-snapshot buffered randomness is ever returned to callers.
		/// Safe to call at any time; idempotent with respect to correctness.
		/// </summary>
		public static void Reset()
		{
			// Interlocked.Increment provides a full memory barrier (StoreLoad fence),
			// ensuring the incremented value is visible to all cores before any
			// subsequent NewGuid() / Fill() calls can execute on restored threads.
			Interlocked.Increment(ref s_epoch);

			// Eagerly reset the *current* thread's container for extra safety.
			// This handles the case where the AfterRestore hook and the first
			// invocation run on the same thread (the epoch check in NewGuid covers this
			// too, but the eager clear avoids even a single stale GUID reaching the
			// epoch-mismatch branch).
			ts_container._idx = 0;
			// _epoch intentionally left stale: FillContainer will sync it.
		}//Reset()

		/// <summary>Initializes a new instance of the <see cref="Guid"/> structure.</summary>
		/// <returns>A new <see cref="Guid"/> struct (cryptographically strong 128-bit random value).</returns>
		/// <remarks>A high-performance, thread-safe, lock-free alternative to <see cref="Guid.NewGuid"/>.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewGuid()
		{
			ref Container container = ref ts_container;
			byte idx = container._idx++; // natural byte overflow (255 -> 0).

			// Two conditions that both require FillContainer:
			//   (a) idx == 0: normal buffer exhaustion (happens every GUIDS_PER_THREAD calls)
			//   (b) epoch mismatch: SnapStart restore detected (happens ~never in steady state)
			//
			// We use "||" so the epoch read is skipped entirely for condition (a).
			// For the steady-state "idx != 0" calls, the s_epoch read is a cheap L1 hit (~1 clock cycle),
			//	  and the branch should be perfectly predicted as not-taken.
			// In the rare case of (b), the epoch mismatch will be detected immediately,
			//    and the buffer will be refilled prior to returning a fresh Guid.
			if (idx == 0 || container._epoch != s_epoch)
			{
				// If epoch-mismatch (idx != 0), we reset "idx" to 0 and "container._idx" (next slot) to 1
				if (idx != 0)
				{
					idx = 0;
					container._idx = 1;
				}
				FillContainer(ref container);
			}//if
			Span<Guid> span = container._guids.AsSpanGuid();
			Guid guid = span[idx];
			span[idx] = default; // wipe the consumed Guid from the thread-local buffer
			return guid;
		}//NewGuid()

		// 512 bytes = 32 GUIDs. Limits one bulk call from consuming more than 1/8th of 
		// the thread-local buffer, preventing cache thrashing for larger sequences.
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

			ref Container container = ref ts_container;

			// Epoch check at the entry point of Fill: if a SnapStart restore happened,
			// force _idx to 0 so the first FillContainer call inside the loops below
			// will pull fresh bytes. We don't call FillContainer here directly to
			// avoid changing the loop structure (and to preserve the slot-zeroing logic).
			if (container._epoch != s_epoch)
				container._idx = 0;
			// _epoch is updated inside FillContainer when _idx == 0 triggers it.


			int lengthInGuids = dataLength >>> 4; // faster assembly than "dataLength / GUID_SIZE_IN_BYTES"

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
		static void FillContainer(ref Container container)
		{
			RandomNumberGenerator.Fill(container._guids.AsSpanByte());

			// Capture epoch AFTER the fill, so if Reset() races during fill
			// (theoretically possible but not expected in Lambda's single-threaded
			// hook model), the mismatch is detected again on the *next* call.
			container._epoch = s_epoch;
		}//FillContainer()
	}//class FastGuid
}//ns
