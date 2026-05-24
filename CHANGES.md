## 🚀 What's New in FastGuid v1.4.2 (`2026-05-24`)

### 🧩 Full RFC 9562 UUIDv7 Support
- Added the `FastGuid.UUID` static subclass, providing high-performance,
  compliant Version 7 UUID generation (`CreateVersion7`) overloads for `DateTime`,
  `DateTimeOffset`, and standard parameterless calls.
- Added zero-allocation timestamp extraction utilities  (`ExtractDateTimeV7` and `TryExtractDateTimeV7`)
  which pull 1-millisecond UTC timestamps out of UUIDv7 identifiers.
- Since `FastGuid` lib targets `.NET 5.0`, all these new additions are not only
  much faster and more capable than `System.Guid`, but also work on older .NET frameworks without
  native `UUIDv7` support.

### 🏎️ Hot-Loop Optimizations (JIT Bounds-Check Stripping)
- Re-engineered the core string compilation loops in `FastGuid.StringGen`
  and the decoding routines in `GuidExtensions` with extra optimizations.

### 🛡️ Strict Unicode Boundary Isolation
- Hardened the `FromBase64Url` and `TryFromBase64Url` decoders
  against malformed data and high-Unicode validation bypasses.

### ☁️ Serverless & Cloud-Native Resiliency
- Added support for VM-Fork & [AWS Lambda SnapStart](https://docs.aws.amazon.com/lambda/latest/dg/snapstart.html)
  [mitigation](https://docs.aws.amazon.com/lambda/latest/dg/csharp-handler.html#csharp-handler-annotations) engine.
- `FastGuid.Reset()` invalidates stale thread-local arrays across cloned micro-VMs.
