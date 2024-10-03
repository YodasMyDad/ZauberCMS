using System.Text.Json;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

    public static class DictionaryExtensions
    {
        /// <summary>
        /// Converts object to dictionary
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Dictionary<string, string>? ToDictionary(this object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }

        /// <summary>
        /// Gets and converts data stored in Dictionary<string, object>, used mainly for extended data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extendedData">The dictionary</param>
        /// <param name="key">The key for the data</param>
        /// <returns>T</returns>
        public static T? Get<T>(this Dictionary<string, object> extendedData, string key)
        {
            if (extendedData.TryGetValue(key, out var value))
            {
                return JsonSerializer.Deserialize<T>(value.ToString() ?? string.Empty);
            }

            return default;
        }

        /// <summary>
        /// Retrieves a value from the dictionary by its key.
        /// </summary>
        /// <param name="extendedData">The dictionary to search in.</param>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <returns>The value associated with the specified key if found; otherwise, default.</returns>
        public static string? Get(this Dictionary<string, object> extendedData, string key)
        {
            if (extendedData.TryGetValue(key, out var value))
            {
                return value.ToString();
            }
            return default;
        }

        /// <summary>
        /// Retrieves the value associated with the specified key from the dictionary.
        /// </summary>
        /// <param name="data">The dictionary from which to retrieve the value.</param>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <returns>The value associated with the specified key, or an empty string if the key is not present.</returns>
        public static string? Get(this Dictionary<string, string> data, string key)
        {
            if (data.TryGetValue(key, out var value))
            {
                return value;
            }
            return string.Empty;
        }
        
        /// <summary>
        /// Retrieves the value associated with the specified key from the dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary from which to retrieve the value.</param>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <typeparam name="T">The type of the values in the dictionary.</typeparam>
        /// <returns>The value associated with the specified key, or the default value if the key is not present.</returns>
        public static T? GetValue<T>(this Dictionary<Guid, T> dictionary, Guid key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        /// <summary>
        /// Attempts to add a value to the dictionary if the specified key is present.
        /// </summary>
        /// <param name="dictionary">The dictionary to which the value should be added.</param>
        /// <param name="key">The key with which the value will be associated.</param>
        /// <param name="value">The value to add to the dictionary.</param>
        /// <typeparam name="T">The type of the values in the dictionary.</typeparam>
        public static void TryAdd<T>(this Dictionary<Guid, T> dictionary, Guid? key, T? value)
        {
            if (key.HasValue && value != null)
            {
                dictionary.TryAdd(key.Value, value);
            }
        }

        /// <summary>
        /// Gets a temp UI messages in the extended data
        /// </summary>
        /// <param name="extendedData"></param>
        /// <returns></returns>
        public static IEnumerable<ResultMessage>? GetTempUiMessages(this Dictionary<string, object> extendedData)
        {
            if (extendedData.ContainsKey(Constants.ExtendedDataKeys.TempUiMessages))
            {
                return extendedData.Get<IEnumerable<ResultMessage>>(Constants.ExtendedDataKeys.TempUiMessages);
            }

            return Enumerable.Empty<ResultMessage>();
        }

        /// <summary>
        /// Removes the temp messages
        /// </summary>
        /// <param name="extendedData"></param>
        public static void RemoveTempUiMessages(this Dictionary<string, object> extendedData)
        {
            if (extendedData.ContainsKey(Constants.ExtendedDataKeys.TempUiMessages))
            {
                extendedData.Remove(Constants.ExtendedDataKeys.TempUiMessages);
            }
        }

        /// <summary>
        /// Sets the Temp UI message on the entity
        /// </summary>
        /// <param name="extendedData"></param>
        /// <param name="message"></param>
        public static void SetTempUiMessage(this Dictionary<string, object> extendedData, ResultMessage message)
        {
            var list = GetCurrentTempUiMessagesAsList(extendedData);
            list.Add(message);
            extendedData.Add(Constants.ExtendedDataKeys.TempUiMessages, list);
        }

        /// <summary>
        /// Sets the Temp UI message on the entity
        /// </summary>
        /// <param name="extendedData"></param>
        /// <param name="messages"></param>
        public static void SetTempUiMessage(this Dictionary<string, object> extendedData, List<ResultMessage> messages)
        {
            var list = GetCurrentTempUiMessagesAsList(extendedData);
            list.AddRange(messages);
            extendedData.Add(Constants.ExtendedDataKeys.TempUiMessages, list);
        }

        /// <summary>
        /// Gets current Temp UI messages
        /// </summary>
        /// <param name="extendedData"></param>
        /// <returns></returns>
        private static List<ResultMessage> GetCurrentTempUiMessagesAsList(Dictionary<string, object> extendedData)
        {
            var list = new List<ResultMessage>();
            var currentMessages = extendedData.GetTempUiMessages();
            if (currentMessages != null)
            {
                var resultMessages = currentMessages as ResultMessage[] ?? currentMessages.ToArray();
                if (resultMessages.Any())
                {
                    list.AddRange(resultMessages);
                }
            }

            return list;
        }
    }