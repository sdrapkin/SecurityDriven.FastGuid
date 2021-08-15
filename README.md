# **FastGuid** [![NuGet](https://img.shields.io/nuget/v/FastGuid.svg)](https://www.nuget.org/packages/FastGuid/)

### by [Stan Drapkin](https://github.com/sdrapkin/)

## 8 times faster than `Guid.NewGuid()`

Replace all calls to [`Guid.NewGuid()`](https://grep.app/search?q=Guid.NewGuid%28%29&filter[lang][0]=C%23) with `FastGuid.NewGuid()`

..from this:
```csharp
Guid guid = Guid.NewGuid(); // your current code
```

..to this:
```csharp
// using SecurityDriven;
Guid guid = FastGuid.NewGuid(); // 8x faster
```

* **Thread-safe**
* **128 bits of cryptographically-strong randomness**

## Benchmark:
```csharp
[SimpleJob]
public class Bench
{
	[Benchmark(Baseline = true)]
	public void FastGuid_NewGuid()
	{
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
		FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid(); FastGuid.NewGuid();
	}

	[Benchmark]
	public void Guid_NewGuid()
	{
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
		Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid(); Guid.NewGuid();
	}
}//class Bench
```

```csharp
SecurityDriven.FastGuid, Version=1.0.0.0, Culture=neutral, PublicKeyToken=e58a6d9408783005
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19042.1165 (20H2/October2020Update)
Intel Core i7-10510U CPU 1.80GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.7.21379.14
  [Host] : .NET 6.0.0 (6.0.21.37719), X64 RyuJIT
```
|           Method |      Mean |    Error |   StdDev | Ratio | RatioSD |
|----------------- |----------:|---------:|---------:|------:|--------:|
| FastGuid_NewGuid |  61.63 ns | 0.228 ns | 0.202 ns |  1.00 |    0.00 |
|     Guid_NewGuid | 503.33 ns | 1.853 ns | 1.643 ns |  8.17 |    0.03 |
