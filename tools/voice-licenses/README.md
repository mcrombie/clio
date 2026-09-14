# Optional offline speech runtime notices

This directory accompanies the separately downloaded, unmodified Piper Windows runtime. The model is `en_GB-northern_english_male-medium`. The bootstrap script pins the engine release, voice repository commit and SHA-256 checksums. Binary downloads live in ignored `build/narration`, not in the source repository.

| Component | License / copyright | Source |
| --- | --- | --- |
| Piper 2023.11.14-2 | MIT, Michael Hansen, 2022 | https://github.com/rhasspy/piper/tree/2023.11.14-2 |
| piper-phonemize | MIT, Michael Hansen, 2023 | https://github.com/rhasspy/piper-phonemize |
| ONNX Runtime | MIT, Microsoft Corporation | https://github.com/microsoft/onnxruntime |
| eSpeak NG | GPL version 3 or later | https://github.com/rhasspy/espeak-ng and https://github.com/espeak-ng/espeak-ng |
| Northern English male voice data | CC BY-SA 4.0, Google, Inc., 2018/2019; volunteer contributors | https://www.openslr.org/83/ |

Full license notices are alongside this file. See `MODEL_CARD.txt` for voice provenance. eSpeak is a dependency of the external synthesis executable and remains under its own license. Its corresponding source and build instructions are in the linked repositories. When publishing a binary runtime bundle, preserve these notices and provide the corresponding source as required by that component's license. The source-only Clio repository instead supplies a downloader for the upstream runtime.

Generated First Adviser recordings and their exact dialogue/settings are documented in `src/Clio.Desktop/Assets/Advisers/Voice/README.md`.
