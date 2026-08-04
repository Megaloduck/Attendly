using System;
using System.Threading.Tasks;
using Attendly.Data;
using Attendly.Models;

namespace Attendly.Services;

public interface IThemeService
{
    ThemeMode CurrentMode { get; }
    event Action<ThemeMode>? ModeChanged;

    /// <summary>Loads the persisted preference. Call once at startup before reading CurrentMode.</summary>
    Task InitializeAsync();

    Task SetModeAsync(ThemeMode mode);
}

/// <summary>Persists the person's Light/Dark choice via the shared SQLite database - so
/// mobile and Desktop each remember their own preference on their own device.</summary>
public class ThemeService : IThemeService
{
    private readonly AttendanceRepository _repository;
    private ThemeMode _currentMode = ThemeMode.Light;

    public ThemeMode CurrentMode => _currentMode;
    public event Action<ThemeMode>? ModeChanged;

    public ThemeService(AttendanceRepository repository)
    {
        _repository = repository;
    }

    public async Task InitializeAsync()
    {
        var settings = await _repository.GetAppSettingsAsync();
        _currentMode = settings?.ThemeMode ?? ThemeMode.Light;
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        _currentMode = mode;
        await _repository.SaveAppSettingsAsync(new AppSettings { ThemeMode = mode });
        ModeChanged?.Invoke(mode);
    }
}