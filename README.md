# Whisper GUI for Windows

A modern, fast, and completely offline voice-to-text desktop application for Windows, powered by the [Whisper.net](https://github.com/sandrohanea/whisper.net) engine and built with **WinUI 3** and .NET 9.

## ✨ Features

- **Completely Offline**: Uses local AI models to ensure your data stays strictly on your device.
- **Microphone Recording**: Real-time transcription from your microphone.
- **Drag & Drop Support**: Drag any audio or video file directly into the app for fast transcription.
- **Fast & Lightweight**: Pre-packaged with the `ggml-base.bin` model, ready to use out of the box with maximum hardware compatibility.
- **Native Windows Experience**: Built using WinUI 3 for a modern, sleek Windows 11 design aesthetic.
- **Multi-Architecture**: Native builds for x86, x64, and ARM64.

## 🚀 Getting Started

### Prerequisites

- **Windows 10** (version 1809 or later) or **Windows 11**
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (for building from source)
- Visual Studio 2022 with the ".NET desktop development" and "Windows application development" workloads.

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/NickelSulphide101/Whisper-GUI-for-Windows.git
   cd Whisper-GUI-for-Windows
   ```

2. Open the solution in Visual Studio or use the .NET CLI:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run --project WhisperGUI
   ```

## 🛠️ Tech Stack

- **Framework**: .NET 9.0 + WinUI 3 (Windows App SDK)
- **AI Engine**: [Whisper.net](https://github.com/sandrohanea/whisper.net) & [Whisper.net.AllRuntimes](https://www.nuget.org/packages/Whisper.net.AllRuntimes)
- **Audio Processing**: [NAudio](https://github.com/naudio/NAudio) (Recording) & [FFmpegCore](https://github.com/rosenbjerg/FFmpegCore) (Media conversion)

## 📦 Packaging

The application is configured to be packaged as an MSIX bundle for easy distribution.
You can create a package in Visual Studio by right-clicking the project -> Publish -> Create App Packages.

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](../../issues).

## 📄 License

This project is open-source and available under the standard open-source licenses. Please refer to the repository for more details.
