# **FastGuid** [![NuGet](https://img.shields.io/nuget/v/FastGuid.svg)](https://www.nuget.org/packages/FastGuid/)

### by [Stan Drapkin](https://github.com/sdrapkin/)

## 10 times faster than `Guid.NewGuid()`

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

## Benchmark:
```csharp
[SimpleJob]
public class Bench
{
	[Benchmark(Baseline = true)]
	public void FastGuid_NewGuid() // 16 calls
	{
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
	}

	[Benchmark]
	public void Guid_NewGuid() // 16 calls
	{
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
	}
}//class Bench
```

```csharp
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19042.1165 (20H2/October2020Update)
Intel Core i7-10510U CPU 1.80GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.7.21379.14
  [Host] : .NET 6.0.0 (6.0.21.37719), X64 RyuJIT
```
|           Method |       Mean |    Error |   StdDev | Ratio | RatioSD |
|----------------- |-----------:|---------:|---------:|------:|--------:|
| FastGuid_NewGuid |   104.6 ns |  0.58 ns |  0.48 ns |  1.00 |    0.00 |
|     Guid_NewGuid | 1,135.4 ns | 16.34 ns | 18.16 ns | 10.80 |    0.17 |
