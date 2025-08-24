namespace Dreamness.Ra3.Map.Parser.Util;

public static class DictionaryExtension
{
    /// <summary>
    /// Adds a key-value pair to the dictionary or updates the value if the key already exists.
    /// </summary>
    /// <param name="dictionary">The dictionary to operate on.</param>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to set.</param>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public static void Put<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.ContainsKey(key))
        {
            dictionary[key] = value; // Update the value if the key exists
        }
        else
        {
            dictionary.Add(key, value); // Add the key-value pair if the key does not exist
        }
    }
}