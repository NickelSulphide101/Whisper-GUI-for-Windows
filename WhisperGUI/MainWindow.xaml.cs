using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WhisperGUI.Services;

namespace WhisperGUI
{
    public sealed partial class MainWindow : Window
    {
        private AudioService _audioService;
        private TranscriptionService _transcriptionService;
        private bool _isRecording = false;
        private CancellationTokenSource _cts;
        // Bug 2 fix: accumulate audio chunks during recording
        private List<byte[]> _recordedChunks = new();

        public MainWindow()
        {
            this.InitializeComponent();
            
            // UI Thread setup
            ExtendsContentIntoTitleBar = true;

            _audioService = new AudioService();
            _transcriptionService = new TranscriptionService();

            _transcriptionService.TextRecognized += OnTextRecognized;
            // Bug 2 fix: subscribe to audio data events
            _audioService.AudioDataAvailable += OnAudioDataAvailable;
            // Bug 1 fix: register window closed handler
            this.Closed += Window_Closed;
            
            InitializeServicesAsync();
        }

        private async void InitializeServicesAsync()
        {
            try
            {
                await _transcriptionService.InitializeAsync();
                DispatcherQueue.TryEnqueue(() =>
                {
                    ModelStatusText.Text = "就绪 (Base)";
                    ModelStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ModelStatusText.Text = "模型加载失败";
                    ModelStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    OutputTextBox.Text += $"Error: {ex.Message}\n";
                });
            }
        }

        private void OnTextRecognized(object sender, string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputTextBox.Text += text + "\n";
                // Scroll to bottom
                OutputTextBox.SelectionStart = OutputTextBox.Text.Length;
                OutputTextBox.SelectionLength = 0;
            });
        }

        // Bug 2 fix: collect audio data during recording
        private void OnAudioDataAvailable(object sender, byte[] data)
        {
            _recordedChunks.Add(data);
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                _isRecording = true;
                _recordedChunks.Clear();
                RecordText.Text = "停止录音";
                RecordIcon.Glyph = "\xE71A"; // Stop icon
                _audioService.StartRecording();
            }
            else
            {
                _isRecording = false;
                RecordText.Text = "开始录音";
                RecordIcon.Glyph = "\xE720"; // Mic icon
                _audioService.StopRecording();

                // Bug 2 fix: feed recorded audio to transcription service
                if (_recordedChunks.Count > 0)
                {
                    OutputTextBox.Text += "正在转录录音...\n";
                    try
                    {
                        // Combine all chunks into a single PCM byte array
                        int totalLength = 0;
                        foreach (var chunk in _recordedChunks)
                            totalLength += chunk.Length;

                        byte[] allPcm = new byte[totalLength];
                        int offset = 0;
                        foreach (var chunk in _recordedChunks)
                        {
                            Array.Copy(chunk, 0, allPcm, offset, chunk.Length);
                            offset += chunk.Length;
                        }
                        _recordedChunks.Clear();

                        _cts?.Dispose(); // Bug 3 fix: dispose previous CTS
                        _cts = new CancellationTokenSource();
                        await _transcriptionService.TranscribePcmAsync(allPcm, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        OutputTextBox.Text += $"[转录失败] {ex.Message}\n";
                    }
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            // Bug 4 fix: guard against empty/null text
            if (string.IsNullOrEmpty(OutputTextBox.Text))
                return;

            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(OutputTextBox.Text);
            Clipboard.SetContent(dataPackage);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Text = string.Empty;
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                DropHighlight.Visibility = Visibility.Visible;
            }
        }

        // Bug 5 fix: hide drop overlay when drag leaves
        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            DropHighlight.Visibility = Visibility.Collapsed;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            DropHighlight.Visibility = Visibility.Collapsed;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var file = items[0];
                    string filePath = file.Path;
                    
                    OutputTextBox.Text += $"开始转录: {file.Name}...\n";
                    
                    // Bug 3 fix: dispose the previous CTS before creating a new one
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                    try
                    {
                        await Task.Run(() => _transcriptionService.TranscribeFileAsync(filePath, _cts.Token));
                    }
                    catch (Exception ex)
                    {
                        OutputTextBox.Text += $"[转录失败] {ex.Message}\n";
                    }
                }
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Bug 1 fix: cancel running work, stop recording, dispose resources
            _cts?.Cancel();
            _cts?.Dispose();
            _audioService?.StopRecording();
            _transcriptionService?.Dispose();
        }
    }
}
