# 🚀 Crster Command

**Crster Command** is a powerful, AI-driven desktop utility designed to supercharge your workflow. Built with the modern **Avalonia UI** framework and integrated with advanced **Google Gemini AI**, it's the ultimate command center for screen analysis, automation, and project management.

---

## ✨ Key Features

### 🧠 AI Screen Intelligence
- **Intelligent Reader**: Capture specific screen regions and leverage Gemini's multi-modal analysis.
- **Structured Data Extraction**: Automatically categorize screen content into Text, Forms, or Images.
- **Precise Cropping**: A custom overlay window for pixel-perfect area selection.

### 🖌️ Professional Annotations
- **Screen Capture Overlay**: Annotate screenshots with a sophisticated red-dashed underline for inputs.
- **Multi-Format Export**: Save your captures in PNG, JPEG, or BMP formats, or copy them directly to your clipboard.

### 🤖 Workflow Automation (Macros)
- **Macro Manager**: Record and reply custom automation scripts.
- **Robot Interactions**: Automate mouse and keyboard tasks effortlessly using the integrated `Desktop.Robot` engine.

### 📽️ Screen Recording & Capture
- **High-Fidelity Captures**: Standard screen capture and recording features built right into the dashboard.
- **Quick Access**: Instantly archive or share your recordings.

### 📝 Productivity Suite
- **Notes & Ideas**: A dedicated space for quick notes and organization.
- **Interactive Dashboard**: A central hub to monitor your tasks and active services.
- **Local persistence**: Your data is yours. Everything is stored locally using **LiteDB**.

---

## 🛠️ Tech Stack

- **Framework**: [Avalonia UI](https://avaloniaui.net/) (Cross-platform .NET UI)
- **AI Engine**: [Google Gemini Pro](https://deepmind.google/technologies/gemini/) (via `Mscc.GenerativeAI`)
- **Language**: C#/.NET 9
- **Database**: [LiteDB](https://www.litedb.org/) (Serverless NoSQL)
- **Automation**: [Desktop.Robot](https://github.com/vrcatala/Desktop.Robot)
- **Updates**: [Velopack](https://velopack.io/)

---

## 🚀 Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Google AI (Gemini) API Key

### Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/yourusername/crster-command.git
   cd crster-command
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
To publish the application for multiple platforms (Windows, macOS, and Linux) as self-contained single-file executables:

1. **Run the publish script**:
   ```powershell
   ./CrsterCommand/publish.ps1
   ```

2. **Output**:
   Find the binaries in the `publish` directory:
   - `win-x64`: Windows (x64)
   - `osx-x64`: macOS (Intel)
   - `linux-x64`: Linux (x64)

### 🛠️ Configuration
Go to the **Settings** view within the app to configure:
- **Gemini API Key**: Essential for AI features.
- **Recording Hub**: Adjust screen recording quality and frame rate.
- **Macro Settings**: Customize keyboard and mouse interaction settings.

---

## 🏗️ Project Structure

- `CrsterCommand/Views`: XAML-based UI components.
- `CrsterCommand/ViewModels`: MVVM logic and state management.
- `CrsterCommand/Services`: Core business logic (AI, Macros, Storage, Recording).
- `CrsterCommand/Models`: Data structures and entities.

---

## 🤝 Contribution

Contributions are welcome! Whether it's adding new features, fixing bugs, or improving documentation, feel free to open a PR.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

*Built with ❤️ by the Crster Team*
