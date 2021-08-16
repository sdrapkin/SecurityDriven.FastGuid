using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SecurityDriven
{
	public static class FastGuid
	{
		// Copyright (c) 2021 Stan Drapkin
		// LICENSE: https://github.com/sdrapkin/SecurityDriven.FastGuid

		const int GUIDS_PER_THREAD = 512; //keep it power-of-2
		[ThreadStatic] static Container ts_data;

		static Container CreateContainer() => ts_data = new();
		static Container LocalContainer => ts_data ?? CreateContainer();

		sealed class Container
		{
			public Guid[] _guids = GC.AllocateUninitializedArray<Guid>(GUIDS_PER_THREAD);
			public int _idx = GUIDS_PER_THREAD;
			public Guid NewGuid()
			{
				int idx = _idx++ & (GUIDS_PER_THREAD - 1);
				if (idx == 0) RandomNumberGenerator.Fill(MemoryMarshal.Cast<Guid, byte>(_guids));

				Guid guid = _guids[idx];
				_guids[idx] = default; // prevents Guid leakage
				return guid;
			}//NextGuid()
		}//class Container

		/// <summary>
		/// Initializes a new instance of the <see cref="Guid"/> structure.
		/// </summary>
		/// <returns>A new <see cref="Guid"/> struct.</returns>
		/// <remarks>Faster alternative to <see cref="Guid.NewGuid"/>.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid NewGuid() => LocalContainer.NewGuid();
	}//class FastGuid
}//ns