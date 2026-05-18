# Third-party notices

SoLoudSharp redistributes the following third-party software as part of its
NuGet package or build process.

## SoLoud

- Source: https://github.com/jarikomppa/soloud
- Author: Jari Komppa
- License: zlib / libpng

```
SoLoud audio engine
Copyright (c) 2013-2020 Jari Komppa

This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

   1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.

   2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.

   3. This notice may not be removed or altered from any source
   distribution.
```

## miniaudio (statically linked via SoLoud's backend)

- Source: https://github.com/mackron/miniaudio (vendored into SoLoud at
  `src/backend/miniaudio/miniaudio.h`)
- Author: David Reid
- License: choice of MIT-0 or public domain (Unlicense)

## stb_vorbis, dr_wav, dr_flac, dr_mp3 (vendored into SoLoud)

- License: choice of MIT or public domain

Audio decoder libraries vendored into SoLoud's `src/audiosource/wav/` and
linked statically into the SoLoudSharp native binary.
