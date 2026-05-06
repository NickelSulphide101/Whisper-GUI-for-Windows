using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using FFMpegCore;

namespace WhisperGUI.Services
{
    public class TranscriptionService : IDisposable
    {
        private WhisperFactory _whisperFactory;
        private WhisperProcessor _processor;
        // Bug 6 fix: resolve model path relative to app base directory, not working directory
        private readonly string _modelPath;

        public event EventHandler<string> TextRecognized;

        public TranscriptionService()
        {
            _modelPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ggml-base.bin");
        }

        public async Task InitializeAsync()
        {
            // Check if model exists, if not, maybe download it
            if (!File.Exists(_modelPath))
            {
                var assetsDir = Path.GetDirectoryName(_modelPath)!;
                if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                using var fileWriter = File.OpenWrite(_modelPath);
                await modelStream.CopyToAsync(fileWriter);
            }

            _whisperFactory = WhisperFactory.FromPath(_modelPath);
            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
        }

        /// <summary>
        /// Process a chunk of raw PCM bytes from the microphone.
        /// </summary>
        public void ProcessAudioChunk(byte[] pcmData)
        {
            // For real-time streaming, you might want to buffer enough audio before processing,
            // or use the processor.ProcessAsync(stream) in a continuous loop.
            // Note: Whisper expects float32 samples between -1 and 1
            float[] floatArray = new float[pcmData.Length / 2];
            for (int i = 0; i < floatArray.Length; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * 2);
                floatArray[i] = sample / 32768.0f;
            }

            // In a real application, you'd feed this into a continuous stream or VAD buffer.
            // This is simplified to show integration.
        }

        /// <summary>
        /// Transcribes raw PCM audio data (16kHz, 16-bit, mono).
        /// Bug 2 fix: new method to support microphone recording transcription.
        /// </summary>
        public async Task TranscribePcmAsync(byte[] pcmData, CancellationToken cancellationToken)
        {
            // Convert 16-bit PCM to float32 samples that Whisper expects
            float[] floatSamples = new float[pcmData.Length / 2];
            for (int i = 0; i < floatSamples.Length; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * 2);
                floatSamples[i] = sample / 32768.0f;
            }

            // Write as a proper WAV file to a MemoryStream for Whisper processing
            using var memStream = new MemoryStream();
            using (var writer = new BinaryWriter(memStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // WAV header for 16kHz, 16-bit, mono PCM
                int sampleRate = 16000;
                short bitsPerSample = 16;
                short channels = 1;
                int dataSize = pcmData.Length;
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                short blockAlign = (short)(channels * bitsPerSample / 8);

                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // chunk size
                writer.Write((short)1); // PCM format
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(pcmData);
            }

            memStream.Position = 0;
            await foreach (var result in _processor.ProcessAsync(memStream, cancellationToken))
            {
                TextRecognized?.Invoke(this, $"{result.Start}->{result.End}: {result.Text}");
            }
        }

        /// <summary>
        /// Transcribes an entire audio or video file.
        /// </summary>
        public async Task TranscribeFileAsync(string filePath, CancellationToken cancellationToken)
        {
            string audioFilePath = filePath;
            bool isTempFile = false;

            try
            {
                // If it's a video file, extract audio using FFmpeg
                var extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".mp4" || extension == ".mkv" || extension == ".avi" || extension == ".mov")
                {
                    audioFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
                    isTempFile = true;
                    
                    // Convert to 16kHz, mono wav which Whisper expects
                    await FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(audioFilePath, true, options => options
                            .WithCustomArgument("-ar 16000 -ac 1 -c:a pcm_s16le"))
                        .ProcessAsynchronously(true);
                }

                // Process the audio file
                using var fileStream = File.OpenRead(audioFilePath);
                await foreach (var result in _processor.ProcessAsync(fileStream, cancellationToken))
                {
                    TextRecognized?.Invoke(this, $"{result.Start}->{result.End}: {result.Text}");
                }
            }
            finally
            {
                if (isTempFile && File.Exists(audioFilePath))
                {
                    File.Delete(audioFilePath);
                }
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _whisperFactory?.Dispose();
        }
    }
}
