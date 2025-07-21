# Multi Saves Backup Tool

<p align="center">
   <img alt="MSBT Logo" src="msbt_logo.png">
</p>

<p align="center">
   <img alt="MSBT Count" src="https://count.lukiuwu.xyz/@MSBT?name=MSBT&theme=rule34&padding=7&offset=0&align=top&scale=1&pixelated=1&darkmode=auto">
</p>

[English](README.md) | [Русский](README.ru.md)

A tool for backing up game saves with support for multiple games and automatic scheduling.

## Features

- Monitor and backup saves for multiple games
- Automatic backup scheduling
- Support for mods and additional backup paths
- Configurable backup retention
- Localization support (English and Russian)
- System tray integration for easy access and management
- Automatic game detection to quickly add installed games
- Restore functionality to recover lost progress
- Statistics tab to track backup frequency and details
- Non-deletable backups for critical saves
- Parsing data from PCGamingWiki for enhanced game information
- And more!

## Requirements

- Windows 10 or later
- .NET 7.0 or later

## Installation

1. Download the latest release from the [Releases page](https://github.com/TheNightlyGod/MSBT/releases)
2. Run MultiSavesBackupSetup.exe

## Usage

### Adding a Game

1. Click "Add Game" in the Games tab
2. Fill in the required information:
    - Game Name: A unique identifier for your game
    - Game Executable: Path to the game's .exe file
    - Save Location: Path to the game's save files
3. Optional settings:
    - Alternative Executable: Secondary .exe file if needed
    - Mods Folder: Path to game mods
    - Additional Backup Path: Any other folders you want to backup
4. Configure backup settings:
    - Backup Interval: How often to backup (1-1440 minutes)
    - Days to Keep: How long to retain backups (0 = keep all)
    - Old Files Action: Choose between keeping all files, moving to archive, or deleting
    - Include Timestamp: Add date/time to backup names
    - Backup Mode: Choose between backing up all files or only changed files

### Monitoring

The Monitoring tab shows:

- Service Status: Current state of the backup service
- Last Update: When the status was last refreshed
- Game Status: Current state of each game's backup
- Last Backup: When the game was last backed up
- Next Backup: When the next backup is scheduled

### Settings

Global settings in the Settings tab:

- Backup Folder: Root directory for all backups
- Scan Interval: How often to check for needed backups
- Max Parallel Operations: Number of simultaneous backups
- Compression Level: Choose between Fast, Optimal, or Smallest Size
- Enable Logging: Toggle logging functionality

## Community and Feedback

Your feedback is valuable! Please share your suggestions or report issues via:

- [GitHub Issues](https://github.com/TheNightlyGod/MSBT/issues)
- [Feedback Form](https://feedback.lukiuwu.xyz/s/cmc8eps900015om012ayu33yh)

## Reporting Issues

If you encounter any issues or have suggestions for improvements, please report them on
the [GitHub Issues page](https://github.com/TheNightlyGod/MSBT/issues). Include the following information:

- **Description of the issue:**  
  e.g., "Clicking the tray icon causes the application to crash."
- **Steps to reproduce:**  
  e.g., "Click the tray icon."
- **Expected behavior:**  
  e.g., "The application should open."
- **Actual behavior:**  
  e.g., "The application crashes."
- **Attachments:**  
  Please attach the following files located in "C:\Program Files (x86)\Multi Saves Backup Tool":
    - backup_state.json
    - games.json
    - backup_serviceYYYYMMDD.log

## Roadmap

Check out our [public roadmap](https://plane.lukiuwu.xyz/spaces/issues/737ad55bb00441cf9219319ef0483a71) to see planned
features and track development progress.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is open source. Please report any issues on [GitHub Issues](https://github.com/TheNightlyGod/MSBT/issues).

## Acknowledgments

- Thank you to the community for 1000 views on GitHub!
- Special thanks to user Varset for their ideas and bug reports.

## Star History

<a href="https://www.star-history.com/?repos=journey-ad/Moe-Counter&type=Date#TheNightlyGod/MSBT&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=TheNightlyGod/MSBT&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=TheNightlyGod/MSBT&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=TheNightlyGod/MSBT&type=Date" />
 </picture>
</a>
