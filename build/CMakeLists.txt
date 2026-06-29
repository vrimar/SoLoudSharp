cmake_minimum_required(VERSION 3.15)
project(soloud CXX C)

# Single-purpose project: build SoLoud as a shared library with the miniaudio
# backend (zero external runtime dependencies) and the C API exposed for
# P/Invoke. All other backends are intentionally disabled.

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT DEFINED SOLOUD_ROOT)
    set(SOLOUD_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../external/soloud")
endif()

if(NOT EXISTS "${SOLOUD_ROOT}/include/soloud.h")
    message(FATAL_ERROR "SoLoud sources not found at ${SOLOUD_ROOT}. Run build/bootstrap.ps1 to initialize the submodule.")
endif()

set(SOLOUD_SRC "${SOLOUD_ROOT}/src")

set(SOLOUD_SOURCES
    # Core
    ${SOLOUD_SRC}/core/soloud.cpp
    ${SOLOUD_SRC}/core/soloud_audiosource.cpp
    ${SOLOUD_SRC}/core/soloud_bus.cpp
    ${SOLOUD_SRC}/core/soloud_core_3d.cpp
    ${SOLOUD_SRC}/core/soloud_core_basicops.cpp
    ${SOLOUD_SRC}/core/soloud_core_faderops.cpp
    ${SOLOUD_SRC}/core/soloud_core_filterops.cpp
    ${SOLOUD_SRC}/core/soloud_core_getters.cpp
    ${SOLOUD_SRC}/core/soloud_core_setters.cpp
    ${SOLOUD_SRC}/core/soloud_core_voicegroup.cpp
    ${SOLOUD_SRC}/core/soloud_core_voiceops.cpp
    ${SOLOUD_SRC}/core/soloud_fader.cpp
    ${SOLOUD_SRC}/core/soloud_fft.cpp
    ${SOLOUD_SRC}/core/soloud_fft_lut.cpp
    ${SOLOUD_SRC}/core/soloud_file.cpp
    ${SOLOUD_SRC}/core/soloud_filter.cpp
    ${SOLOUD_SRC}/core/soloud_misc.cpp
    ${SOLOUD_SRC}/core/soloud_queue.cpp
    ${SOLOUD_SRC}/core/soloud_thread.cpp

    # Audio sources
    ${SOLOUD_SRC}/audiosource/ay/chipplayer.cpp
    ${SOLOUD_SRC}/audiosource/ay/sndbuffer.cpp
    ${SOLOUD_SRC}/audiosource/ay/sndchip.cpp
    ${SOLOUD_SRC}/audiosource/ay/sndrender.cpp
    ${SOLOUD_SRC}/audiosource/ay/soloud_ay.cpp
    ${SOLOUD_SRC}/audiosource/monotone/soloud_monotone.cpp
    ${SOLOUD_SRC}/audiosource/noise/soloud_noise.cpp
    ${SOLOUD_SRC}/audiosource/openmpt/soloud_openmpt.cpp
    ${SOLOUD_SRC}/audiosource/openmpt/soloud_openmpt_dll.c
    ${SOLOUD_SRC}/audiosource/sfxr/soloud_sfxr.cpp
    ${SOLOUD_SRC}/audiosource/speech/darray.cpp
    ${SOLOUD_SRC}/audiosource/speech/klatt.cpp
    ${SOLOUD_SRC}/audiosource/speech/resonator.cpp
    ${SOLOUD_SRC}/audiosource/speech/soloud_speech.cpp
    ${SOLOUD_SRC}/audiosource/speech/tts.cpp
    ${SOLOUD_SRC}/audiosource/tedsid/sid.cpp
    ${SOLOUD_SRC}/audiosource/tedsid/soloud_tedsid.cpp
    ${SOLOUD_SRC}/audiosource/tedsid/ted.cpp
    ${SOLOUD_SRC}/audiosource/vic/soloud_vic.cpp
    ${SOLOUD_SRC}/audiosource/vizsn/soloud_vizsn.cpp
    ${SOLOUD_SRC}/audiosource/wav/dr_impl.cpp
    ${SOLOUD_SRC}/audiosource/wav/soloud_wav.cpp
    ${SOLOUD_SRC}/audiosource/wav/soloud_wavstream.cpp
    ${SOLOUD_SRC}/audiosource/wav/stb_vorbis.c

    # Filters
    ${SOLOUD_SRC}/filter/soloud_bassboostfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_biquadresonantfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_dcremovalfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_duckfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_echofilter.cpp
    ${SOLOUD_SRC}/filter/soloud_eqfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_fftfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_flangerfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_freeverbfilter.cpp
    ${SOLOUD_SRC}/filter/soloud_lofifilter.cpp
    ${SOLOUD_SRC}/filter/soloud_robotizefilter.cpp
    ${SOLOUD_SRC}/filter/soloud_waveshaperfilter.cpp

    # Backend (miniaudio only)
    ${SOLOUD_SRC}/backend/miniaudio/soloud_miniaudio.cpp

    # C API
    ${SOLOUD_SRC}/c_api/soloud_c.cpp
)

if(WIN32)
    list(APPEND SOLOUD_SOURCES ${SOLOUD_SRC}/c_api/soloud.def)
endif()

add_library(soloud SHARED ${SOLOUD_SOURCES})

target_include_directories(soloud PRIVATE
    ${SOLOUD_ROOT}/include
    ${SOLOUD_SRC}
)

target_compile_definitions(soloud PRIVATE WITH_MINIAUDIO)

if(MSVC)
    target_compile_options(soloud PRIVATE /W3 /wd4244 /wd4267 /wd4996)
    # On MSVC, soloud.def is consumed by the linker; setting it as a source
    # above already does the right thing. Suppress noisy CRT warnings.
    target_compile_definitions(soloud PRIVATE _CRT_SECURE_NO_WARNINGS)
else()
    target_compile_options(soloud PRIVATE -Wall -Wno-unused-parameter -Wno-sign-compare)
    find_package(Threads REQUIRED)
    target_link_libraries(soloud PRIVATE Threads::Threads ${CMAKE_DL_LIBS})
endif()

if(APPLE)
    target_link_libraries(soloud PRIVATE
        "-framework CoreFoundation"
        "-framework CoreAudio"
        "-framework AudioToolbox"
    )
endif()

if(ANDROID)
    # AAudio/OpenSL ES are dlopen'd at runtime by miniaudio; only liblog is linked.
    target_link_libraries(soloud PRIVATE log)
    # Required for Google Play (apps targeting Android 15+): 16 KB page alignment.
    target_link_options(soloud PRIVATE "-Wl,-z,max-page-size=16384")
endif()

# Produce predictable output names: soloud.dll / libsoloud.so / libsoloud.dylib
set_target_properties(soloud PROPERTIES
    OUTPUT_NAME soloud
    PREFIX "${CMAKE_SHARED_LIBRARY_PREFIX}"
)
