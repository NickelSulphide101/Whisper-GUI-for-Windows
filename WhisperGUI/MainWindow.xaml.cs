using System;
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

        public MainWindow()
        {
            this.InitializeComponent();
            
            // UI Thread setup
            ExtendsContentIntoTitleBar = true;

            _audioService = new AudioService();
            _transcriptionService = new TranscriptionService();

            _transcriptionService.TextRecognized += OnTextRecognized;
            
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

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                _isRecording = true;
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
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
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
            _audioService?.StopRecording();
            _transcriptionService?.Dispose();
        }
    }
}
