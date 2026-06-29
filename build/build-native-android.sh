#!/usr/bin/env bash
# Cross-compiles libsoloud.so for Android using the NDK CMake toolchain + miniaudio backend.
#
# Usage: build-native-android.sh <rid>
# Where <rid> is one of: android-arm64, android-arm, android-x64
#
# Requires the Android NDK; resolved from ANDROID_NDK_ROOT or ANDROID_NDK_LATEST_HOME.
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <rid>" >&2
    echo "  rid: android-arm64 | android-arm | android-x64" >&2
    exit 1
fi

RID="$1"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
SOLOUD="$REPO/external/soloud"

# Minimum API level supported by .NET for Android.
ANDROID_PLATFORM="android-21"

case "$RID" in
    android-arm64)
        ANDROID_ABI="arm64-v8a"
        ;;
    android-arm)
        ANDROID_ABI="armeabi-v7a"
        ;;
    android-x64)
        ANDROID_ABI="x86_64"
        ;;
    *)
        echo "Unsupported RID: $RID" >&2
        exit 1
        ;;
esac

NDK="${ANDROID_NDK_ROOT:-${ANDROID_NDK_LATEST_HOME:-}}"
if [ -z "$NDK" ] || [ ! -d "$NDK" ]; then
    echo "Android NDK not found. Set ANDROID_NDK_ROOT or ANDROID_NDK_LATEST_HOME." >&2
    exit 1
fi

TOOLCHAIN="$NDK/build/cmake/android.toolchain.cmake"
if [ ! -f "$TOOLCHAIN" ]; then
    echo "NDK toolchain file missing: $TOOLCHAIN" >&2
    exit 1
fi

# Host-tagged LLVM binutils that understand cross-arch ELF.
HOST_TAG="linux-x86_64"
LLVM_BIN="$NDK/toolchains/llvm/prebuilt/$HOST_TAG/bin"
NM="$LLVM_BIN/llvm-nm"
STRIP="$LLVM_BIN/llvm-strip"
OBJCOPY="$LLVM_BIN/llvm-objcopy"

if [ ! -f "$SOLOUD/include/soloud.h" ]; then
    echo "SoLoud submodule missing at $SOLOUD. Run bootstrap.ps1 first." >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)"
BUILD_DIR="$REPO/build/build-$RID"
rm -rf "$BUILD_DIR"

echo "[build-native-android] cmake -S build -B $BUILD_DIR (abi=$ANDROID_ABI, platform=$ANDROID_PLATFORM)"
cmake -S "$REPO/build" -B "$BUILD_DIR" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE="$TOOLCHAIN" \
    -DANDROID_ABI="$ANDROID_ABI" \
    -DANDROID_PLATFORM="$ANDROID_PLATFORM"

echo "[build-native-android] cmake --build $BUILD_DIR --parallel $NPROC"
cmake --build "$BUILD_DIR" --parallel "$NPROC"

LIB="$BUILD_DIR/libsoloud.so"
if [ ! -f "$LIB" ]; then
    LIB="$(find "$BUILD_DIR" -maxdepth 3 -name 'libsoloud.so' | head -n 1)"
fi
if [ ! -f "$LIB" ]; then
    echo "Expected output missing: libsoloud.so under $BUILD_DIR" >&2
    find "$BUILD_DIR" -name '*.so' >&2 || true
    exit 1
fi

NATIVE_OUT="$REPO/artifacts/native/$RID"
mkdir -p "$NATIVE_OUT"
SO="$NATIVE_OUT/libsoloud.so"
cp -f "$LIB" "$SO"

# Strip and produce a separate symbol file using NDK binutils (cross-arch aware).
"$OBJCOPY" --only-keep-debug "$SO" "${SO}.dbg" || true
"$STRIP" --strip-unneeded "$SO" || true
"$OBJCOPY" --add-gnu-debuglink="${SO}.dbg" "$SO" || true

# Symbol export check. Capture nm output first; piping into `grep -q` would
# trip `set -o pipefail` (grep exits on first match, nm gets SIGPIPE).
syms="$("$NM" -D --defined-only "$SO" 2>/dev/null)"
if ! grep -qE '\bSoloud_create$' <<<"$syms"; then
    echo "libsoloud.so does not export Soloud_create" >&2
    exit 1
fi

echo "[build-native-android] symbol check OK - $RID artifacts staged."
ls -la "$NATIVE_OUT"
