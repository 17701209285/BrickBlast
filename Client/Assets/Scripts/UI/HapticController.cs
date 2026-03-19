using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HapticController : MonoBehaviour
{
    public enum HapticImpactType
    {
        Selection = 0,
        LightImpact = 1,
        MediumImpact = 2,
        HeavyImpact = 3,
        SoftImpact = 4,
        RigidImpact = 5
    }

    private const HapticImpactType ExtraType = HapticImpactType.SoftImpact;
    private const int ExtraCount = 2;
    private const int ExtraDelayMs = 150;

    private const HapticImpactType MultiRowType = HapticImpactType.SoftImpact;
    private const int MultiRowCount = 4;
    private const int MultiRowDelayMs = 100;

    private bool initialized;
    private bool isSupported;
    private int lastPlayedFrame = -1;
    private Coroutine activePatternCoroutine;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
    private int androidSdkInt;
    private bool hasAmplitudeControl;
#endif

    public bool IsSupported
    {
        get
        {
            EnsureInitialized();
            return isSupported;
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        StopActivePattern();
    }

    public void Play(HapticImpactType hapticType)
    {
        if (!TryBeginPlayback())
        {
            return;
        }

        StopActivePattern();
        var profile = GetProfile(hapticType);
        PlayPulse(profile.DurationMs, profile.Amplitude);
    }

    public void Play(float intensity, float sharpness = 1f)
    {
        if (!TryBeginPlayback())
        {
            return;
        }

        StopActivePattern();

        var clampedIntensity = Mathf.Clamp01(intensity);
        var clampedSharpness = Mathf.Clamp01(sharpness);
        var durationMs = Mathf.RoundToInt(Mathf.Lerp(42f, 18f, clampedSharpness));
        var amplitude = Mathf.RoundToInt(Mathf.Lerp(72f, 255f, clampedIntensity));

        PlayPulse(durationMs, amplitude);
    }

    public void PlayExtraBalls()
    {
        PlayPattern(ExtraType, ExtraCount, ExtraDelayMs);
    }

    public void PlayMultiRow()
    {
        PlayPattern(MultiRowType, MultiRowCount, MultiRowDelayMs);
    }

    private void PlayPattern(HapticImpactType type, int count, int intervalMs)
    {
        if (!TryBeginPlayback())
        {
            return;
        }

        StopActivePattern();
        activePatternCoroutine = StartCoroutine(PlayPatternCoroutine(type, Mathf.Max(1, count), Mathf.Max(0, intervalMs)));
    }

    private IEnumerator PlayPatternCoroutine(HapticImpactType type, int count, int intervalMs)
    {
        var profile = GetProfile(type);
        var waitSeconds = intervalMs <= 0 ? 0f : intervalMs / 1000f;

        for (var i = 0; i < count; i++)
        {
            if (!CanPlay())
            {
                break;
            }

            PlayPulse(profile.DurationMs, profile.Amplitude);

            if (i < count - 1 && waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }
        }

        activePatternCoroutine = null;
    }

    private bool TryBeginPlayback()
    {
        if (!CanPlay())
        {
            return false;
        }

        if (lastPlayedFrame == Time.frameCount)
        {
            return false;
        }

        lastPlayedFrame = Time.frameCount;
        return true;
    }

    private bool CanPlay()
    {
        EnsureInitialized();
        return isSupported && FeedbackSettings.IsVibrationEnabled;
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        isSupported = Application.isMobilePlatform && SystemInfo.deviceType == DeviceType.Handheld;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isSupported)
        {
            return;
        }

        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var buildVersion = new AndroidJavaClass("android.os.Build$VERSION");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            androidSdkInt = buildVersion.GetStatic<int>("SDK_INT");

            if (activity != null)
            {
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator != null)
                {
                    isSupported = vibrator.Call<bool>("hasVibrator");
                    if (isSupported && androidSdkInt >= 26)
                    {
                        hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
                    }
                }
                else
                {
                    isSupported = false;
                }
            }
            else
            {
                isSupported = false;
            }
        }
        catch
        {
            isSupported = false;
        }
#endif
    }

    private void StopActivePattern()
    {
        if (activePatternCoroutine == null)
        {
            return;
        }

        StopCoroutine(activePatternCoroutine);
        activePatternCoroutine = null;
    }

    private void PlayPulse(int durationMs, int amplitude)
    {
        durationMs = Mathf.Max(10, durationMs);
        amplitude = Mathf.Clamp(amplitude, 1, 255);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator != null)
        {
            try
            {
                if (androidSdkInt >= 26)
                {
                    using var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
                    var appliedAmplitude = hasAmplitudeControl ? amplitude : -1;
                    using var oneShot = vibrationEffect.CallStatic<AndroidJavaObject>(
                        "createOneShot",
                        (long)durationMs,
                        appliedAmplitude);
                    vibrator.Call("vibrate", oneShot);
                    return;
                }

                vibrator.Call("vibrate", (long)durationMs);
                return;
            }
            catch
            {
            }
        }
#endif

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    private static HapticProfile GetProfile(HapticImpactType type)
    {
        switch (type)
        {
            case HapticImpactType.Selection:
                return new HapticProfile(18, 84);
            case HapticImpactType.LightImpact:
                return new HapticProfile(24, 124);
            case HapticImpactType.MediumImpact:
                return new HapticProfile(30, 176);
            case HapticImpactType.HeavyImpact:
                return new HapticProfile(38, 255);
            case HapticImpactType.RigidImpact:
                return new HapticProfile(20, 220);
            case HapticImpactType.SoftImpact:
            default:
                return new HapticProfile(26, 110);
        }
    }

    private readonly struct HapticProfile
    {
        public int DurationMs { get; }
        public int Amplitude { get; }

        public HapticProfile(int durationMs, int amplitude)
        {
            DurationMs = durationMs;
            Amplitude = amplitude;
        }
    }
}
