namespace Multi_Saves_Backup_Tool.Services;

public interface ITrayService
{
    bool IsVisible { get; set; }
    void Initialize();
    void UpdateTooltip(string tooltip);
}