#!/usr/bin/env bash
# Builds libsoloud.{so,dylib} on Linux or macOS using CMake + miniaudio backend.
#
# Usage: build-native-unix.sh <rid>
# Where <rid> is one of: linux-x64, osx-x64
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <rid>" >&2
    echo "  rid: linux-x64 | osx-x64" >&2
    exit 1
fi

RID="$1"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
SOLOUD="$REPO/external/soloud"

case "$RID" in
    linux-x64)
        LIB_EXT="so"
        EXTRA_CMAKE_ARGS=()
        ;;
    osx-x64)
        LIB_EXT="dylib"
        # Bake in a deployment target compatible with current macOS runners.
        EXTRA_CMAKE_ARGS=(-DCMAKE_OSX_DEPLOYMENT_TARGET=11.0 -DCMAKE_OSX_ARCHITECTURES=x86_64)
        ;;
    *)
        echo "Unsupported RID: $RID" >&2
        exit 1
        ;;
esac

if [ ! -f "$SOLOUD/include/soloud.h" ]; then
    echo "SoLoud submodule missing at $SOLOUD. Run bootstrap.ps1 first." >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || sysctl -n hw.physicalcpu 2>/dev/null || echo 4)"
BUILD_DIR="$REPO/build/build-$RID"
rm -rf "$BUILD_DIR"

echo "[build-native-unix] cmake -S build -B $BUILD_DIR -DCMAKE_BUILD_TYPE=Release ${EXTRA_CMAKE_ARGS[*]:-}"
cmake -S "$REPO/build" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE=Release "${EXTRA_CMAKE_ARGS[@]}"

echo "[build-native-unix] cmake --build $BUILD_DIR --parallel $NPROC"
cmake --build "$BUILD_DIR" --parallel "$NPROC"

LIB="$BUILD_DIR/libsoloud.$LIB_EXT"
if [ ! -f "$LIB" ]; then
    # CMake may emit into a subdir on some generators; search.
    LIB="$(find "$BUILD_DIR" -maxdepth 3 -name "libsoloud.$LIB_EXT" | head -n 1)"
fi
if [ ! -f "$LIB" ]; then
    echo "Expected output missing: libsoloud.$LIB_EXT under $BUILD_DIR" >&2
    find "$BUILD_DIR" -name '*.so' -o -name '*.dylib' >&2 || true
    exit 1
fi

NATIVE_OUT="$REPO/artifacts/native/$RID"
mkdir -p "$NATIVE_OUT"
cp -f "$LIB" "$NATIVE_OUT/libsoloud.$LIB_EXT"

# Strip and produce separate symbol files.
if [ "$LIB_EXT" = "so" ]; then
    SO="$NATIVE_OUT/libsoloud.so"
    objcopy --only-keep-debug "$SO" "${SO}.dbg" || true
    strip --strip-unneeded "$SO" || true
    objcopy --add-gnu-debuglink="${SO}.dbg" "$SO" || true

    if command -v patchelf >/dev/null 2>&1; then
        patchelf --set-soname libsoloud.so "$SO" || true
    fi
elif [ "$LIB_EXT" = "dylib" ]; then
    DY="$NATIVE_OUT/libsoloud.dylib"
    if command -v dsymutil >/dev/null 2>&1; then
        dsymutil "$DY" -o "${DY}.dSYM" || true
    fi
    strip -S "$DY" || true
fi

# Symbol export check. Capture nm output first; piping into `grep -q` would
# trip `set -o pipefail` because grep exits on the first match and the upstream
# nm gets SIGPIPE (exit 141).
if [ "$LIB_EXT" = "so" ]; then
    syms="$(nm -D --defined-only "$NATIVE_OUT/libsoloud.so" 2>/dev/null)"
    if ! grep -qE '\bSoloud_create$' <<<"$syms"; then
        echo "libsoloud.so does not export Soloud_create" >&2
        exit 1
    fi
elif [ "$LIB_EXT" = "dylib" ]; then
    syms="$(nm -gU "$NATIVE_OUT/libsoloud.dylib" 2>/dev/null)"
    if ! grep -qE '_Soloud_create$' <<<"$syms"; then
        echo "libsoloud.dylib does not export Soloud_create" >&2
        exit 1
    fi
fi

echo "[build-native-unix] symbol check OK - $RID artifacts staged."
ls -la "$NATIVE_OUT"
