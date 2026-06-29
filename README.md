# SoLoudSharp

Cross-platform .NET bindings for the [SoLoud](https://github.com/jarikomppa/soloud) audio engine, with native binaries bundled for `win-x64`, `linux-x64`, `osx-x64`, and Android (`android-arm64`, `android-arm`, `android-x64`). The native build uses SoLoud's built-in [miniaudio](https://github.com/mackron/miniaudio) backend, so the package has no external runtime dependencies.

## Packages

| Package | Contents |
|---|---|
| `SoLoudSharp` | Managed bindings (`net8.0`/`net10.0`) plus `soloud.dll` / `libsoloud.so` / `libsoloud.dylib` (incl. Android `.so` for arm64-v8a, armeabi-v7a, x86_64) under `runtimes/{rid}/native/`. AOT-compatible. |

## Quick start

```csharp
using SoLoudSharp;

var soloud = Native.Soloud_create();
Native.Soloud_initEx(soloud,
    aFlags: (uint)SoloudInitFlags.ClipRoundoff,
    aBackend: (uint)SoloudBackend.MiniAudio,
    aSamplerate: 0, aBufferSize: 0, aChannels: 2);

var wav = Native.Wav_create();
Native.Wav_load(wav, "hello.wav");
Native.Soloud_play(soloud, wav);

// ... wait for playback ...

Native.Wav_destroy(wav);
Native.Soloud_deinit(soloud);
Native.Soloud_destroy(soloud);
```

The `Native` class mirrors `soloud_c.h` one-to-one. Idiomatic C# wrappers (`Soloud : IDisposable`, etc.) can be layered on top as the binding surface grows.

## Building from source

Prerequisites:

- .NET SDK 10 (see [`global.json`](global.json))
- CMake 3.15+
- A C++ toolchain (MSVC on Windows, gcc/clang on Linux, Apple clang on macOS)
- PowerShell 7+ (for the Windows native build script)
- Android NDK r27+ for the Android build (set `ANDROID_NDK_ROOT` or `ANDROID_NDK_LATEST_HOME`)

Steps:

```bash
# 1. Initialize the SoLoud submodule.
pwsh build/bootstrap.ps1

# 2. Build the native shared library for your platform.
pwsh build/build-native-win.ps1                  # Windows
bash build/build-native-unix.sh linux-x64        # Linux
bash build/build-native-unix.sh osx-x64          # macOS
bash build/build-native-android.sh android-arm64 # Android (also: android-arm, android-x64)

# 3. Build the managed solution.
dotnet build SoLoudSharp.sln
```

Output is staged into `artifacts/native/{rid}/` and packed into the NuGet package via the `runtimes/{rid}/native/` convention.

When working only on the managed side (no native binaries available), suppress the pack-time warning with:

```bash
dotnet build SoLoudSharp.sln -p:SkipNativeWarning=true
```

## Layout

```
external/soloud/    # git submodule, pinned to a specific upstream SHA
build/              # CMakeLists.txt + per-platform build scripts
src/SoLoudSharp/    # managed wrapper library (multi-targets net8.0;net10.0)
tests/              # xunit smoke tests + AOT sample
artifacts/native/   # staging for native binaries before packing
.github/workflows/  # ci-pr, build-native, package
```

## License

[Zlib](LICENSE) — same terms as SoLoud itself. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the bundled SoLoud and miniaudio attributions.
