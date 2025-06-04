# Multi Saves Backup Tool

A tool for automatic game save backup with flexible settings and real-time monitoring.

## Features

- 🎮 Support for multiple games
- 🔄 Automatic background backup
- ⚙️ Customizable backup intervals for each game
- 📂 Ability to backup multiple folders for a single game:
  - Saves
  - Mods
  - Additional files
- ⏱️ Configurable backup retention period
- 💾 Backup compression to save space
- 🖥️ Modern user interface using Avalonia UI

## System Requirements

- Windows
- .NET 9.0 or higher

## Installation

1. Download the latest version of the installer (`MultiSavesBackupSetup.exe`)
2. Run the installer and follow the instructions
3. After installation, the program will start automatically

## Usage
[Multi Saves Backup Tool.csproj](Multi%20Saves%20Backup%20Tool/Multi%20Saves%20Backup%20Tool.csproj)
### Adding a Game

1. Go to the "Games" tab
2. Click the add new game button
3. Specify:
   - Game name
   - Path to game executable
   - Path to saves folder
   - (Optional) Path to mods folder
   - (Optional) Additional backup folder
   - Backup interval
   - Backup retention period

### Monitoring

In the "Monitoring" tab, you can track:
- Backup status for each game
- Time of the last backup
- Number of saved copies

### Settings

In the settings section, you can configure:
- Backup compression level
- General application parameters

## Technical Support

If you encounter any issues or have questions, create an issue in the project repository.
