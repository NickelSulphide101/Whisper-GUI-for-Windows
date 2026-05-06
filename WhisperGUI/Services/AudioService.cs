using System;
using System.IO;
using NAudio.Wave;

namespace WhisperGUI.Services
{
    public class AudioService
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioStream;
        private WaveFileWriter? _waveWriter;

        public event EventHandler<byte[]>? AudioDataAvailable;

        public void StartRecording()
        {
            if (_waveIn != null) return;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0, // Default microphone
                // Whisper requires 16kHz, 16-bit, Mono PCM
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50
            };

            _audioStream = new MemoryStream();
            // Optional: Write to stream if you want to save it, or just pass bytes.
            // We can just pass the raw bytes directly to Whisper.net stream.

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            // Make a copy of the buffer to pass out
            byte[] buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
            
            AudioDataAvailable?.Invoke(this, buffer);
        }

        public void StopRecording()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
                _waveIn = null!;
            }

            if (_audioStream != null)
            {
                _audioStream.Dispose();
                _audioStream = null!;
            }
        }
    }
}
