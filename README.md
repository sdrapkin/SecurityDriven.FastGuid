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
