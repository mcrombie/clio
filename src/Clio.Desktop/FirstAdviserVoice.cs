using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;

namespace Clio.Desktop
{
    // Narration is entirely local. A single worker owns synthesis and playback;
    // generation numbers prevent interrupted or muted reports from starting later.
    internal sealed class FirstAdviserVoice : IDisposable
    {
        private sealed class Request
        {
            internal int Generation;
            internal string Text, ClipKey;
        }

        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly string preferencesPath;
        private readonly Thread worker;
        private bool muted, disposed;
        private int generation;
        private string problem = "";
        private Request pending;
        private Process activeProcess;
        private SoundPlayer activePlayer;
        private SpeechSynthesizer activeSpeech;

        [DllImport("winmm.dll")]
        private static extern uint waveOutGetNumDevs();

        internal FirstAdviserVoice(string preferencesPath)
        {
            this.preferencesPath = preferencesPath;
            if (!String.IsNullOrEmpty(preferencesPath))
            {
                try
                {
                    if (File.Exists(preferencesPath))
                    {
                        string saved = File.ReadAllText(preferencesPath).Trim();
                        muted = String.Equals(saved, "muted", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(saved, "true", StringComparison.OrdinalIgnoreCase) || saved == "1";
                    }
                }
                catch (IOException) { problem = "The saved voice preference could not be read."; }
                catch (UnauthorizedAccessException) { problem = "The saved voice preference could not be read."; }
            }
            worker = new Thread(Work) { IsBackground = true, Name = "Clio First Adviser narration" };
            worker.Start();
        }

        internal bool Muted { get { lock (gate) return muted; } }
        internal string Problem { get { lock (gate) return problem; } }

        internal void SetMuted(bool value)
        {
            lock (gate)
            {
                if (disposed) return;
                muted = value;
                if (value) { generation++; pending = null; }
            }
            if (value) CancelActive();
            if (!String.IsNullOrEmpty(preferencesPath))
            {
                try
                {
                    string directory = Path.GetDirectoryName(preferencesPath);
                    if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(preferencesPath, value ? "muted" : "audible", new UTF8Encoding(false));
                }
                catch (IOException) { SetProblem("Voice changed, but the mute preference could not be saved."); }
                catch (UnauthorizedAccessException) { SetProblem("Voice changed, but the mute preference could not be saved."); }
            }
            Wake();
        }

        internal void Speak(string text, string clipKey = null)
        {
            if (String.IsNullOrWhiteSpace(text)) { Stop(); return; }
            lock (gate)
            {
                if (disposed || muted) return;
                generation++; problem = "";
                // Cancel the old owner before the worker can pick up this report.
                // Otherwise a late cancellation could stop the replacement voice.
                CancelActive();
                pending = new Request { Generation = generation, Text = text.Length > 8000 ? text.Substring(0, 8000) : text, ClipKey = clipKey };
            }
            Wake();
        }

        internal void Stop()
        {
            lock (gate) { if (disposed) return; generation++; pending = null; }
            CancelActive(); Wake();
        }

        private void Wake()
        { try { wake.Set(); } catch (ObjectDisposedException) { } }

        private bool Current(Request request)
        { lock (gate) return !disposed && !muted && generation == request.Generation; }

        private void SetProblem(string text)
        { lock (gate) { if (!disposed) problem = text; } }

        private void SetProblem(Request request, string text)
        { lock (gate) { if (!disposed && generation == request.Generation) problem = text; } }

        private void CancelActive()
        {
            Process process; SoundPlayer player; SpeechSynthesizer speech;
            lock (gate) { process = activeProcess; player = activePlayer; speech = activeSpeech; }
            // Never wait for synthesis or audio completion on the UI thread.
            try { if (player != null) player.Stop(); } catch (Exception) { }
            try { if (speech != null) speech.SpeakAsyncCancelAll(); } catch (Exception) { }
            try { if (process != null && !process.HasExited) process.Kill(); } catch (Exception) { }
        }

        private void Work()
        {
            try
            {
                while (true)
                {
                    wake.WaitOne();
                    Request request;
                    lock (gate)
                    {
                        if (disposed) return;
                        request = pending; pending = null;
                    }
                    if (request == null || !Current(request)) continue;
                    try { Narrate(request); }
                    catch (Exception)
                    { SetProblem(request, "The adviser could not speak. The written report remains available."); }
                    lock (gate) { if (pending != null) Wake(); }
                }
            }
            finally { wake.Dispose(); }
        }

        private void Narrate(Request request)
        {
            try
            {
                if (waveOutGetNumDevs() == 0)
                { SetProblem(request, "No audio output is available. Connect speakers or headphones; the report remains readable."); return; }
            }
            catch (DllNotFoundException)
            { SetProblem(request, "Windows audio is unavailable. The report remains readable."); return; }
            if (!Current(request)) return;
            if (request.ClipKey == "opening" || request.ClipKey == "influence")
            {
                using (Stream recording = Assembly.GetExecutingAssembly().GetManifestResourceStream("Clio.Voice." + request.ClipKey + ".wav"))
                {
                    if (recording != null)
                    {
                        using (MemoryStream copy = new MemoryStream())
                        { recording.CopyTo(copy); copy.Position = 0; PlayWave(request, copy); }
                        return;
                    }
                }
            }
            string voiceDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice");
            string executable = Path.Combine(voiceDirectory, "piper.exe");
            string model = Path.Combine(voiceDirectory, "en_GB-northern_english_male-medium.onnx");
            if (File.Exists(executable) && File.Exists(model) && File.Exists(model + ".json"))
            {
                if (SpeakWithPiper(request, executable, model, voiceDirectory)) return;
                if (!Current(request)) return;
                SetProblem(request, "The recorded voice is unavailable for this report; using an installed Windows voice.");
            }
            else SetProblem(request, "Using an installed Windows voice. The local adviser voice files are unavailable.");
            SpeakWithWindows(request);
        }

        private bool SpeakWithPiper(Request request, string executable, string model, string directory)
        {
            string wavePath = Path.Combine(Path.GetTempPath(), "clio-adviser-" + Guid.NewGuid().ToString("N") + ".wav");
            Process process = null;
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(executable)
                {
                    WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    Arguments = "--model " + QuoteArgument(model) + " --output_file " + QuoteArgument(wavePath) +
                        " --length_scale 1.14 --noise_scale 0.55 --noise_w 0.7 --sentence_silence 0.32 --quiet"
                };
                process = new Process { StartInfo = start };
                // Drain both pipes; no console or unbounded error buffer is left behind.
                process.OutputDataReceived += delegate { };
                process.ErrorDataReceived += delegate { };
                lock (gate)
                {
                    if (!Current(request)) return true;
                    activeProcess = process; process.Start();
                }
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                using (StreamWriter input = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                { input.WriteLine(request.Text.Replace("\r", " ").Replace("\n", " ")); }
                while (!process.WaitForExit(100))
                {
                    if (!Current(request)) { try { process.Kill(); } catch (Exception) { } return true; }
                }
                if (!Current(request)) return true;
                if (process.ExitCode != 0 || !File.Exists(wavePath)) return false;
                using (MemoryStream wave = new MemoryStream(File.ReadAllBytes(wavePath))) PlayWave(request, wave);
                return true;
            }
            catch (Exception) { return !Current(request); }
            finally
            {
                lock (gate) { if (activeProcess == process) activeProcess = null; }
                if (process != null)
                {
                    try { if (!process.HasExited) { process.Kill(); process.WaitForExit(1500); } } catch (Exception) { }
                    process.Dispose();
                }
                // Only the exact file created by this request is ever removed.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try { if (File.Exists(wavePath)) File.Delete(wavePath); break; }
                    catch (IOException) { if (attempt < 2) Thread.Sleep(50); }
                    catch (UnauthorizedAccessException) { break; }
                }
            }
        }

        private static string QuoteArgument(string value)
        {
            // These are local file paths, never narration text or shell code.
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void PlayWave(Request request, MemoryStream wave)
        {
            if (!Current(request)) return;
            double seconds = WaveDuration(wave); wave.Position = 0;
            using (SoundPlayer player = new SoundPlayer(wave) { LoadTimeout = 5000 })
            {
                try
                {
                    lock (gate) { if (!Current(request)) return; activePlayer = player; }
                    player.Load();
                    lock (gate)
                    {
                        if (!Current(request)) return;
                        // Play returns immediately. Starting under this lock means
                        // a concurrent mute can always cancel an already-started clip.
                        player.Play();
                    }
                    Stopwatch elapsed = Stopwatch.StartNew();
                    while (Current(request) && elapsed.Elapsed.TotalSeconds < seconds + .25) Thread.Sleep(60);
                }
                finally
                {
                    try { player.Stop(); } catch (Exception) { }
                    lock (gate) { if (activePlayer == player) activePlayer = null; }
                }
            }
        }

        private static double WaveDuration(MemoryStream stream)
        {
            stream.Position = 0;
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Invalid narration audio.");
                reader.ReadUInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Invalid narration audio.");
                uint bytesPerSecond = 0, dataLength = 0;
                while (stream.Position + 8 <= stream.Length)
                {
                    string chunk = new string(reader.ReadChars(4)); uint size = reader.ReadUInt32();
                    long next = stream.Position + size + (size & 1);
                    if (next > stream.Length + 1) break;
                    if (chunk == "fmt " && size >= 16)
                    { reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32(); bytesPerSecond = reader.ReadUInt32(); }
                    else if (chunk == "data") dataLength = size;
                    stream.Position = Math.Min(next, stream.Length);
                    if (bytesPerSecond > 0 && dataLength > 0) break;
                }
                if (bytesPerSecond == 0 || dataLength == 0) throw new InvalidDataException("Empty narration audio.");
                return dataLength / (double)bytesPerSecond;
            }
        }

        private void SpeakWithWindows(Request request)
        {
            if (!Current(request)) return;
            using (ManualResetEvent finished = new ManualResetEvent(false))
            using (SpeechSynthesizer speech = new SpeechSynthesizer())
            {
                Exception audioError = null;
                speech.SpeakCompleted += delegate(object sender, SpeakCompletedEventArgs args)
                { audioError = args.Error; try { finished.Set(); } catch (ObjectDisposedException) { } };
                try
                {
                    speech.SetOutputToDefaultAudioDevice(); speech.Rate = -1; speech.Volume = 95;
                    InstalledVoice voice = speech.GetInstalledVoices().Where(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "en")
                        .OrderByDescending(v => v.VoiceInfo.Gender == VoiceGender.Male)
                        .ThenByDescending(v => v.VoiceInfo.Culture.Name == "en-GB")
                        .ThenByDescending(v => v.VoiceInfo.Age == VoiceAge.Senior).FirstOrDefault();
                    if (voice != null) speech.SelectVoice(voice.VoiceInfo.Name);
                    string language = speech.Voice.Culture.Name;
                    string ssml = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"" + language +
                        "\"><prosody pitch=\"-12%\" rate=\"-5%\">" + SecurityElement.Escape(request.Text) + "</prosody></speak>";
                    lock (gate)
                    {
                        if (!Current(request)) return;
                        activeSpeech = speech; speech.SpeakSsmlAsync(ssml);
                    }
                    while (!finished.WaitOne(80) && Current(request)) { }
                    if (audioError != null && Current(request))
                        SetProblem(request, "Windows could not play adviser narration. Check the audio output; the report remains readable.");
                }
                catch (Exception)
                { SetProblem(request, "No working adviser voice is available. The written report remains available."); }
                finally
                {
                    try { speech.SpeakAsyncCancelAll(); } catch (Exception) { }
                    lock (gate) { if (activeSpeech == speech) activeSpeech = null; }
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true; generation++; pending = null;
            }
            CancelActive(); Wake();
            // The background worker releases its current exact temp file, audio
            // objects and event. Closing the game never waits for model inference.
        }
    }
}
