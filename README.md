# **FastGuid** [![NuGet](https://img.shields.io/nuget/v/FastGuid.svg)](https://www.nuget.org/packages/FastGuid/)

### by [Stan Drapkin](https://github.com/sdrapkin/)

## 10 times faster than `Guid.NewGuid()`

## Static APIs
* **`Guid FastGuid.NewGuid()`**
	- returns a cryptographically random GUID.
	- ~10x faster than Guid.NewGuid().
* **`FastGuid.Fill(Span<byte> data)`**
	- Fills a span with cryptographically strong random bytes.
	- ~5x faster for <512 bytes, otherwise calls RandomNumberGenerator.Fill()

## Usage
Replace all calls to [`Guid.NewGuid()`](https://grep.app/search?q=Guid.NewGuid%28%29&filter[lang][0]=C%23) with `FastGuid.NewGuid()`

..from this:
```csharp
Guid guid = Guid.NewGuid(); // your current code
```

..to this:
```csharp
// using SecurityDriven;
Guid guid = FastGuid.NewGuid(); // 10x faster
```

* **Thread-safe**
* **128 bits of cryptographically-strong randomness**

Switch from this:
```csharp
Span<byte> key = stackalloc byte[32];
RandomNumberGenerator.Fill(key); // 145 nanoseconds
```

..to this:
```csharp
Span<byte> key = stackalloc byte[32];
FastGuid.Fill(key); // 25 nanoseconds
```

## Benchmark:
```csharp
public class Bench
{
	[Benchmark(Baseline = true)]
	public void FastGuid_NewGuid() // 12 calls
	{
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
	}

	[Benchmark]
	public void Guid_NewGuid() // 12 calls
	{
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
	}
}//class Bench
```

```csharp
BenchmarkDotNet=v0.13.2, OS=Windows 10 (10.0.19045.2364)
Intel Core i7-10510U CPU 1.80GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.101
  [Host] : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
```
|           Method |       Mean |    Error |   StdDev | Ratio |
|----------------- |-----------:|---------:|---------:|------:|
| FastGuid_NewGuid |   116.6 ns |  2.26 ns |  5.19 ns |  1.00 |
|     Guid_NewGuid | 1,215.9 ns | 23.85 ns | 45.96 ns | 10.39 |