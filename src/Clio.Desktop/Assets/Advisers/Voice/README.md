# First Adviser narration

`opening.wav` and `influence.wav` are prerecorded synthetic narration of the corresponding First Adviser reports. They use a calm Northern British male voice at a slightly slower pace. No actor or fictional character's voice was cloned. These are neural speech recordings, not a human performance.

The original game dialogue is included below. The generated audio assets are provided under [Creative Commons Attribution-ShareAlike 4.0 International](https://creativecommons.org/licenses/by-sa/4.0/), with the model and source-data credits preserved here. This notice concerns the voice assets, not unrelated Clio source files.

## Credits and sources

- Speech engine: [Piper 2023.11.14-2](https://github.com/rhasspy/piper/releases/tag/2023.11.14-2), Michael Hansen, MIT license. This is the older standalone Windows release, not the later GPL Python package.
- Voice: [rhasspy/piper-voices, en_GB-northern_english_male-medium](https://huggingface.co/rhasspy/piper-voices/tree/1162a9173d0ce503555aed757976b7a9912eae4c/en/en_GB/northern_english_male/medium). The model card identifies its source data as CC BY-SA 4.0. It was fine-tuned from the US English Lessac medium voice.
- Source data: [OpenSLR SLR83, UK and Ireland English dialect speech](https://www.openslr.org/83/), copyright 2018, 2019 Google, Inc.; recordings contributed by volunteers. Authors: Isin Demirsahin, Oddur Kjartansson, Alexander Gutkin and Clara Rivera, *Open-source Multi-speaker Corpora of the English Accents in the British Isles*, 2020.
- Changes: new Clio dialogue synthesized using that model with the settings below. Audio is 22,050 Hz, mono PCM WAV.

License texts and runtime source links are in `tools/voice-licenses`. The runtime is a separate local executable; Clio does not send narration text to a service. Run `tools/prepare-adviser-voice.ps1` once to obtain the optional offline runtime, then build. The two committed recordings work without that runtime; live reports can use the installed Windows speech fallback.

## Generation

Use the text below on Piper's standard input. Arguments:

```text
--model en_GB-northern_english_male-medium.onnx --output_file <clip>.wav --length_scale 1.14 --noise_scale 0.55 --noise_w 0.7 --sentence_silence 0.32 --quiet
```

No pitch shifting, voice conversion or added reverberation was used. Live narration should use the same settings to keep the First Adviser's delivery consistent.

### Opening

Let morning find us ready. Fifty people have placed their trust in you. Tonight, that is enough. The first matters of the tribe begin in the morning, after rest. Bed early; rise with the light. A rested chief is more likely to meet the day's troubles with a clear head. Even wisdom benefits from a full night's sleep.

### Influence

Your word carries weight. You are chief of the tribe. That gives you one influence each turn: the power to direct our common effort. Unspent influence carries forward. For now, choose between two uses. Each costs one influence. We need not make government any more complicated before breakfast.
