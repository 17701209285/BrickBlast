using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine;

internal static class GameLog
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message)
    {
        Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Warning(object message)
    {
        Debug.LogWarning(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void InfoFormat(string format, params object[] args)
    {
        Debug.LogFormat(format, args);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void WarningFormat(string format, params object[] args)
    {
        Debug.LogWarningFormat(format, args);
    }
}
