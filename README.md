[English](README.md) | [Русский](README.ru.md)

# Multi Saves Backup Tool

A tool for automatic backup of game saves with support for multiple games and customizable backup settings.

## Features

- Monitor multiple games simultaneously
- Automatic backup scheduling
- Customizable backup intervals
- Compression options for backup files
- Support for mod folders backup
- Cleanup of old backups
- Service-based architecture for reliable operation

## Requirements

- Windows 10 or later
- .NET 7.0 or later
- Administrator privileges for service installation

## Installation

1. Download the latest release from the releases page
2. Extract the archive to a desired location
3. Run the application as administrator
4. The application will install itself as a Windows service

## Usage

1. Launch the application
2. Add games using the "Games" tab:
   - Specify the game executable
   - Set the save files location
   - Configure backup settings
3. Monitor backup status in the "Monitoring" tab
4. Configure global settings in the "Settings" tab

## Configuration

### Game Settings

- **Game Name**: Display name for the game
- **Game Executable**: Path to the game's executable file
- **Save Location**: Path to the game's save files
- **Backup Interval**: How often to create backups
- **Backup Settings**:
  - Days to keep backups
  - Compression level
  - Include timestamps
  - Backup all files or only changed ones

### Global Settings

- **Backup Root Folder**: Where to store all backups
- **Scan Interval**: How often to check for new saves
- **Max Parallel Operations**: Number of simultaneous backups
- **Compression Level**: Global compression setting
- **Logging**: Enable/disable logging

## License

This project is licensed under the MIT License - see the LICENSE file for details. 