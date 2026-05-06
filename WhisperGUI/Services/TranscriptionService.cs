using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using FFmpegCore;

namespace WhisperGUI.Services
{
    public class TranscriptionService : IDisposable
    {
        private WhisperFactory _whisperFactory;
        private WhisperProcessor _processor;
        private readonly string _modelPath = "Assets/ggml-base.bin";

        public event EventHandler<string> TextRecognized;

        public async Task InitializeAsync()
        {
            // Check if model exists, if not, maybe download it
            if (!File.Exists(_modelPath))
            {
                if (!Directory.Exists("Assets")) Directory.CreateDirectory("Assets");
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
