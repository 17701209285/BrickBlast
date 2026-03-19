using UnityEngine;

public static class FeedbackSettings
{
    private const string ScreenShakeEnabledKey = "feedback.screen_shake";
    private const string VibrationEnabledKey = "feedback.vibration";

    private static bool? cachedScreenShakeEnabled;
    private static bool? cachedVibrationEnabled;

    public static bool IsScreenShakeEnabled
    {
        get => GetCachedValue(ref cachedScreenShakeEnabled, ScreenShakeEnabledKey, true);
        set => SetCachedValue(ref cachedScreenShakeEnabled, ScreenShakeEnabledKey, value);
    }

    public static bool IsVibrationEnabled
    {
        get => GetCachedValue(ref cachedVibrationEnabled, VibrationEnabledKey, true);
        set => SetCachedValue(ref cachedVibrationEnabled, VibrationEnabledKey, value);
    }

    private static bool GetCachedValue(ref bool? cachedValue, string key, bool defaultValue)
    {
        if (!cachedValue.HasValue)
        {
            cachedValue = PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        return cachedValue.Value;
    }

    private static void SetCachedValue(ref bool? cachedValue, string key, bool value)
    {
        if (cachedValue.HasValue && cachedValue.Value == value)
        {
            return;
        }

        cachedValue = value;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
