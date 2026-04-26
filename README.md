# 🚀 Crster Command

**Crster Command** is a powerful, AI-driven desktop utility designed to supercharge your workflow. Built with the modern **Avalonia UI** framework and integrated with **Google Gemini AI**, it's the ultimate command center for screen analysis, AI-powered macros, semantic notes, and workflow automation.

---

## ✨ Key Features

### 🧠 AI-Powered Macros
- **Macro Manager**: Create custom AI apps with a dedicated system prompt and send prompts with one click or a global hotkey.
- **File Attachments**: Attach files (images, documents, etc.) directly to a macro for multi-modal AI prompts.
- **Model Selection**: Choose any available Gemini model (e.g., `gemini-2.5-flash`) per macro for fine-grained cost and quality control.
- **Robot Mode**: Trigger macros automatically via a configurable global hotkey (`Ctrl+Alt+Shift+F12` by default) using the integrated `SharpHook` global input engine.
- **Copy & Export Responses**: Copy AI answers to clipboard or download response files in one click.

### 🖥️ Screen Capture & Analysis
- **Intelligent Screen Reader**: Capture specific screen regions and send them to Gemini's multi-modal API for instant analysis.
- **Precise Cropping Overlay**: A custom overlay window for pixel-perfect area selection.
- **Global Hotkey**: Trigger a capture from anywhere with a configurable shortcut (`PrintScreen` by default on Windows).
- **Multi-Format Export**: Save captures in PNG, JPEG, or BMP formats, or copy directly to the clipboard.

### 📽️ Screen Recording
- **High-Fidelity Recording**: Built-in screen recording with a dedicated recording overlay.
- **Quick Access**: Instantly archive or share recordings from the dashboard.

### 📝 Smart Notes
- **Rich Notes**: A dedicated space for quick notes, ideas, and files with pagination support.
- **Semantic Search**: AI-powered search using **local embeddings** (`ElBruno.LocalEmbeddings`) — finds notes by meaning, not just keywords.
- **Debounced Live Search**: Results update automatically as you type, with smart cancellation to avoid redundant lookups.

### 🔒 Security & Privacy
- **Encrypted API Key Storage**: Your Gemini API key is protected with **AES-256 encryption**, derived from a device-unique key.
- **Vault Password**: An optional vault password adds a second layer of protection to your stored secrets.
- **100% Local Storage**: All data (notes, macros, settings) is stored on-device using **LiteDB** — nothing leaves your machine except AI API calls.

### ⚙️ System Integration
- **Start on Startup**: Optionally launch Crster Command with Windows startup.
- **Start Hidden**: Launch silently to the system tray and stay out of the way until you need it.
- **Configurable Hotkeys**: All global shortcuts are user-defined from the Settings view.
- **Auto-Updates**: Seamless over-the-air updates powered by **Velopack**.

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| **Framework** | [Avalonia UI 12](https://avaloniaui.net/) — Cross-platform .NET desktop UI |
| **Language** | C# / .NET 10 |
| **MVVM** | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
| **AI Engine** | [Google Gemini](https://deepmind.google/technologies/gemini/) via `Google.GenAI` |
| **Local Embeddings** | [ElBruno.LocalEmbeddings](https://github.com/elbruno/LocalEmbeddings) — offline semantic search |
| **Database** | [LiteDB](https://www.litedb.org/) — Serverless, embedded NoSQL |
| **Global Hooks** | [SharpHook](https://github.com/TolikPylypchuk/SharpHook) — cross-platform keyboard/mouse hooks |
| **Icons** | [FluentIcons.Avalonia](https://github.com/davidxuang/FluentIcons) |
| **Updates** | [Velopack](https://velopack.io/) |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [Google AI Studio](https://aistudio.google.com/apikey) API Key (free tier available)

### Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/Crster/command.git
   cd command
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Run the application**:
   ```bash
   dotnet run --project CrsterCommand
   ```

### 🏗️ Publishing

To publish for multiple platforms as self-contained single-file executables:

1. **Run the publish script**:
   ```powershell
   ./CrsterCommand/publish.ps1
   ```

2. **Output** — find the binaries in the `publish` directory:
   | Folder | Platform |
   |---|---|
   | `win-x64` | Windows (x64) |
   | `osx-x64` | macOS (Intel) |
   | `linux-x64` | Linux (x64) |

### 🛠️ Configuration

Open the **Settings** view inside the app to configure:

| Setting | Description |
|---|---|
| **Gemini API Key** | Required for all AI features. Stored encrypted on-device. |
| **Vault Password** | Optional extra layer of encryption for stored secrets. |
| **AI Model** | Select the Gemini model to use (default: `gemini-2.5-flash`). |
| **Screen Capture Shortcut** | Global hotkey to trigger a screen capture (default: `PrintScreen`). |
| **Desktop Robot Shortcut** | Global hotkey to start/stop the AI Robot mode (default: `Ctrl+Alt+Shift+F12`). |
| **Start on Startup** | Launch automatically when Windows starts. |
| **Start Hidden** | Launch minimized to the system tray. |

---

## 🏗️ Project Structure

```
CrsterCommand/
├── Models/          # Data structures and entities (AiMacroApp, NoteModels, UserSettings, …)
├── ViewModels/      # MVVM logic and state management (CommunityToolkit.Mvvm)
├── Views/           # XAML UI components (Avalonia)
├── Windows/         # Overlay and dialog windows (CaptureOverlay, RecordingOverlay, Dialogs)
├── Services/        # Core business logic
│   ├── AIService              # Gemini API integration, chat history, file attachments
│   ├── EmbeddingService       # Local vector embeddings & cosine similarity for semantic search
│   ├── SecurityService        # AES-256 encryption / decryption of secrets
│   ├── StorageService         # LiteDB persistence layer
│   ├── ImageService           # Screen capture and image processing
│   ├── ScreenRecorderService  # Screen recording
│   ├── FileAttachmentService  # File attachment handling for AI macros
│   ├── GlobalHookManager      # SharpHook-based global keyboard/mouse hook manager
│   ├── ScreenCaptureHotkeyService  # Hotkey wiring for screen capture
│   ├── DesktopRobotHotkeyService   # Hotkey wiring for robot/macro automation
│   └── StartupService         # Windows startup registration
└── Converters/      # Value converters for Avalonia bindings
```

---

## 🤝 Contributing

Contributions are welcome! Whether it's adding new features, fixing bugs, or improving documentation, feel free to open a pull request.

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m "feat: add my feature"`
4. Push and open a PR against `main`

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

*Built with ❤️ by the Crster Team*
