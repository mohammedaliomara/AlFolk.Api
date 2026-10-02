using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AlFolk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly string _settingsFilePath = "app_settings.json";

    private async Task<Dictionary<string, object>> LoadSettingsAsync()
    {
        if (!System.IO.File.Exists(_settingsFilePath))
        {
            var defaultSettings = new Dictionary<string, object>
            {
                { "appName", "AlFolk Platform" },
                { "contactEmail", "info@alfolk.org" },
                { "contactPhone", "01012345678" },
                { "termsAndConditions", "شروط وأحكام منصة مؤسسة الفلك..." },
                { "aboutUs", "مؤسسة الفلك لتعليم وتطوير مهارات الأطفال ذوي التوحد البسيط..." },
                { "features", new Dictionary<string, bool>
                    {
                        { "enableChat", true },
                        { "enableLeaderboard", true },
                        { "enableOfflineMode", false },
                        { "enableDailyTasks", true }
                    }
                }
            };
            var json = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(_settingsFilePath, json);
            return defaultSettings;
        }

        var savedJson = await System.IO.File.ReadAllTextAsync(_settingsFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(savedJson) ?? new Dictionary<string, object>();
    }

    private async Task SaveSettingsAsync(Dictionary<string, object> settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(_settingsFilePath, json);
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await LoadSettingsAsync();
        return Ok(new { success = true, data = settings });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, object> newSettings)
    {
        var settings = await LoadSettingsAsync();
        foreach (var key in newSettings.Keys)
        {
            settings[key] = newSettings[key];
        }
        await SaveSettingsAsync(settings);
        return Ok(new { success = true, message = "تم حفظ الإعدادات بنجاح", data = settings });
    }

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatureFlags()
    {
        var settings = await LoadSettingsAsync();
        if (settings.TryGetValue("features", out var features))
        {
            return Ok(new { success = true, data = features });
        }
        return Ok(new { success = true, data = new Dictionary<string, bool>() });
    }
}
