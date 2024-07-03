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

        public static string? Get(this Dictionary<string, object> extendedData, string key)
        {
            if (extendedData.TryGetValue(key, out var value))
            {
                return value.ToString();
            }
            return default;
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