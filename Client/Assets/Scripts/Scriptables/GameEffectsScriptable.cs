using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "GameEffectsScriptable", menuName = "Scriptable Objects/GameEffectsScriptable")]
public class GameEffectsScriptable : ScriptableObject, ISerializationCallbackReceiver
{
    [Serializable]
    private struct EffectEntry
    {
        public string key;
        public GameObject effect;
    }

    [SerializeField]
    private List<EffectEntry> effects = new List<EffectEntry>();

    [NonSerialized]
    private Dictionary<string, GameObject> m_effectDictionary;


    public bool GetEffect(string key,out GameObject effect)
    {
        EnsureDictionary();
        if (string.IsNullOrEmpty(key) || m_effectDictionary == null)
        {
            effect = null;
            return false;
        }

        return m_effectDictionary.TryGetValue(key, out effect);
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        m_effectDictionary = null;
    }

    private void OnEnable()
    {
        m_effectDictionary = null;
    }

    private void EnsureDictionary()
    {
        if (m_effectDictionary != null)
        {
            return;
        }

        m_effectDictionary = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        for (int i = 0; i < effects.Count; i++)
        {
            var entry = effects[i];
            if (string.IsNullOrEmpty(entry.key) || entry.effect == null)
            {
                continue;
            }

            m_effectDictionary[entry.key] = entry.effect;
        }
    }
}
