using System.Collections.Generic;
using CaptainPinkTurd.Core.CustomDataStructure;

namespace CaptainPinkTurd.Core.Extensions
{
    public static class SerializeKeyValuePairExtensions
    {
        #region Array
        
        public static bool TryGetValue<TKey, TValue>(this SerializeKeyValuePair<TKey, TValue>[] dictionary, TKey key, out TValue value)
        {
            foreach (var element in dictionary)
            {
                if (EqualityComparer<TKey>.Default.Equals(element.Key, key))
                {
                    value = element.Value;
                    return true; 
                }
            }
            value = default;
            return false;
        }
        
        #endregion

        #region List

        public static List<SerializeKeyValuePair<TKey, TValue>> AddKeyValuePair<TKey, TValue>(
            this List<SerializeKeyValuePair<TKey, TValue>> dictionary, TKey key, TValue value)
        {
            var addedValue = new SerializeKeyValuePair<TKey, TValue>
            {
                Key = key,
                Value = value
            };
            dictionary.Add(addedValue);
            return dictionary;
        }
        public static bool TryGetValue<TKey, TValue>(this List<SerializeKeyValuePair<TKey, TValue>> dictionary, TKey key, out TValue value)
        {
            foreach (var element in dictionary)
            {
                if (EqualityComparer<TKey>.Default.Equals(element.Key, key))
                {
                    value = element.Value;
                    return true; 
                }
            }
            value = default;
            return false;
        }

        #endregion
    }
}