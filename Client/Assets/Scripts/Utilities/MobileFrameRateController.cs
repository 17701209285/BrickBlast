using UnityEngine;

internal static class MobileFrameRateController
{
    private const int LowEndTargetFrameRate = 30;
    private const int HighEndTargetFrameRate = 60;
    private const int IOSHighEndMemoryMb = 4096;
    private const int AndroidHighEndMemoryMb = 6144;
    private const int HighEndCoreCount = 6;
    private const int AndroidHighEndCoreCount = 8;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyDefaultFrameRate()
    {
        if (!Application.isMobilePlatform || SystemInfo.deviceType != DeviceType.Handheld)
        {
            return;
        }

        QualitySettings.vSyncCount = 0;

        int targetFrameRate = ResolveTargetFrameRate();
        Application.targetFrameRate = targetFrameRate;

        Debug.LogFormat(
            "[FrameRate] Applied mobile target frame rate: {0}. DeviceModel={1} MemoryMB={2} Cores={3}",
            targetFrameRate,
            SystemInfo.deviceModel,
            SystemInfo.systemMemorySize,
            SystemInfo.processorCount);
    }

    private static int ResolveTargetFrameRate()
    {
#if UNITY_IOS
        return IsHighEndIOSDevice() ? HighEndTargetFrameRate : LowEndTargetFrameRate;
#elif UNITY_ANDROID
        return IsHighEndAndroidDevice() ? HighEndTargetFrameRate : LowEndTargetFrameRate;
#else
        return HighEndTargetFrameRate;
#endif
    }

    private static bool IsHighEndIOSDevice()
    {
        return SystemInfo.systemMemorySize >= IOSHighEndMemoryMb
            || SystemInfo.processorCount >= HighEndCoreCount;
    }

    private static bool IsHighEndAndroidDevice()
    {
        return SystemInfo.systemMemorySize >= AndroidHighEndMemoryMb
            || (SystemInfo.systemMemorySize >= IOSHighEndMemoryMb && SystemInfo.processorCount >= AndroidHighEndCoreCount);
    }
}
