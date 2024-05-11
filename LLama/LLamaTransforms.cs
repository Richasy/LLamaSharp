using LLama.Abstractions;
using LLama.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace LLama
{
    /// <summary>
    /// A class that contains all the transforms provided internally by LLama.
    /// </summary>
    public class LLamaTransforms
    {
        /// <summary>
        /// The default history transform.
        /// Uses plain text with the following format:
        /// [Author]: [Message]
        /// </summary>
        public class DefaultHistoryTransform : IHistoryTransform
        {
            private const string defaultUserName = "User";
            private const string defaultAssistantName = "Assistant";
            private const string defaultSystemName = "System";
            private const string defaultUnknownName = "??";

            public string UserName { get; }
            public string AssistantName { get; }
            public string SystemName { get; }
            public string UnknownName { get; }
            public bool IsInstructMode { get; }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="userName"></param>
            /// <param name="assistantName"></param>
            /// <param name="systemName"></param>
            /// <param name="unknownName"></param>
            /// <param name="isInstructMode"></param>
            public DefaultHistoryTransform(string? userName = null, string? assistantName = null, 
                string? systemName = null, string? unknownName = null, bool isInstructMode = false)
            {
                UserName = userName ?? defaultUserName;
                AssistantName = assistantName ?? defaultAssistantName;
                SystemName = systemName ?? defaultSystemName;
                UnknownName = unknownName ?? defaultUnknownName;
                IsInstructMode = isInstructMode;
            }

            /// <inheritdoc />
            public IHistoryTransform Clone()
            {
                return new DefaultHistoryTransform(UserName, AssistantName, SystemName, UnknownName, IsInstructMode);
            }

            /// <inheritdoc />
            public virtual string HistoryToText(ChatHistory history)
            {
                StringBuilder sb = new();
                foreach (var message in history.Messages)
                {
                    if (message.AuthorRole == AuthorRole.User)
                    {
                        sb.AppendLine($"{UserName}: {message.Content}");
                    }
                    else if (message.AuthorRole == AuthorRole.System)
                    {
                        sb.AppendLine($"{SystemName}: {message.Content}");
                    }
                    else if (message.AuthorRole == AuthorRole.Unknown)
                    {
                        sb.AppendLine($"{UnknownName}: {message.Content}");
                    }
                    else if (message.AuthorRole == AuthorRole.Assistant)
                    {
                        sb.AppendLine($"{AssistantName}: {message.Content}");
                    }
                }
                return sb.ToString();
            }

            /// <inheritdoc />
            public virtual ChatHistory TextToHistory(AuthorRole role, string text)
            {
                ChatHistory history = new ChatHistory();
                history.AddMessage(role, TrimNamesFromText(text, role));
                return history;
            }

            /// <summary>
            /// Drop the name at the beginning and the end of the text.
            /// </summary>
            /// <param name="text"></param>
            /// <param name="role"></param>
            /// <returns></returns>
            public virtual string TrimNamesFromText(string text, AuthorRole role)
            {
                if (role == AuthorRole.User && text.StartsWith($"{UserName}:"))
                {
                    text = text.Substring($"{UserName}:".Length).TrimStart();
                }
                else if (role == AuthorRole.Assistant && text.EndsWith($"{AssistantName}:"))
                {
                    text = text.Substring(0, text.Length - $"{AssistantName}:".Length).TrimEnd();
                }
                if (IsInstructMode && role == AuthorRole.Assistant && text.EndsWith("\n> "))
                {
                    text = text.Substring(0, text.Length - "\n> ".Length).TrimEnd();
                }
                return text;
            }
        }

        /// <summary>
        /// A text input transform that only trims the text.
        /// </summary>
        public class NaiveTextInputTransform
            : ITextTransform
        {
            /// <inheritdoc />
            public string Transform(string text)
            {
                return text.Trim();
            }

            /// <inheritdoc />
            public ITextTransform Clone()
            {
                return new NaiveTextInputTransform();
            }
        }

        /// <summary>
        /// A no-op text input transform.
        /// </summary>
        public class EmptyTextOutputStreamTransform
            : ITextStreamTransform
        {
            /// <inheritdoc />
            public IAsyncEnumerable<string> TransformAsync(IAsyncEnumerable<string> tokens)
            {
                return tokens;
            }

            /// <inheritdoc />
            public ITextStreamTransform Clone()
            {
                return new EmptyTextOutputStreamTransform();
            }
        }

        /// <summary>
        /// A text output transform that removes the keywords from the response.
        /// </summary>
        public class KeywordTextOutputStreamTransform : ITextStreamTransform
        {
            /// <summary>
            /// Keywords that you want to remove from the response.
            /// This property is used for JSON serialization.
            /// </summary>
            [JsonPropertyName("keywords")]
            public HashSet<string> Keywords { get; }

            /// <summary>
            /// Maximum length of the keywords.
            /// This property is used for JSON serialization.
            /// </summary>
            [JsonPropertyName("maxKeywordLength")]
            public int MaxKeywordLength { get; }

            /// <summary>
            /// If set to true, when getting a matched keyword, all the related tokens will be removed. 
            /// Otherwise only the part of keyword will be removed.
            /// This property is used for JSON serialization.
            /// </summary>
            [JsonPropertyName("removeAllMatchedTokens")]
            public bool RemoveAllMatchedTokens { get; }

            /// <summary>
            /// JSON constructor.
            /// </summary>
            [JsonConstructor]
            public KeywordTextOutputStreamTransform(
                HashSet<string> keywords,
                int maxKeywordLength,
                bool removeAllMatchedTokens)
            {
                Keywords = new(keywords);
                MaxKeywordLength = maxKeywordLength;
                RemoveAllMatchedTokens = removeAllMatchedTokens;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="keywords">Keywords that you want to remove from the response.</param>
            /// <param name="redundancyLength">The extra length when searching for the keyword. For example, if your only keyword is "highlight", 
            /// maybe the token you get is "\r\nhighligt". In this condition, if redundancyLength=0, the token cannot be successfully matched because the length of "\r\nhighligt" (10)
            /// has already exceeded the maximum length of the keywords (8). On the contrary, setting redundancyLengyh &gt;= 2 leads to successful match.
            /// The larger the redundancyLength is, the lower the processing speed. But as an experience, it won't introduce too much performance impact when redundancyLength &lt;= 5 </param>
            /// <param name="removeAllMatchedTokens">If set to true, when getting a matched keyword, all the related tokens will be removed. Otherwise only the part of keyword will be removed.</param>
            public KeywordTextOutputStreamTransform(IEnumerable<string> keywords, int redundancyLength = 3, bool removeAllMatchedTokens = false)
            {
                Keywords = new(keywords);
                MaxKeywordLength = Keywords.Max(x => x.Length) + redundancyLength;
                MaxKeywordLength = Keywords.Select(x => x.Length).Max() + redundancyLength;
                RemoveAllMatchedTokens = removeAllMatchedTokens;
            }

            /// <inheritdoc />
            public ITextStreamTransform Clone()
            {
                return new KeywordTextOutputStreamTransform(Keywords, MaxKeywordLength, RemoveAllMatchedTokens);
            }

            /// <inheritdoc />
            public async IAsyncEnumerable<string> TransformAsync(IAsyncEnumerable<string> tokens)
            {
                var window = new Queue<string>();

                await foreach (var s in tokens)
                {
                    window.Enqueue(s);
                    var current = string.Join("", window);
                    if (Keywords.Any(x => current.Contains(x)))
                    {
                        var matchedKeywords = Keywords.Where(x => current.Contains(x));
                        int total = window.Count;
                        for (int i = 0; i < total; i++)
                        {
                            window.Dequeue();
                        }
                        if (!RemoveAllMatchedTokens)
                        {
                            foreach(var keyword in matchedKeywords)
                            {
                                current = current.Replace(keyword, "");
                            }
                            yield return current;
                        }
                    }
                    if (current.Length >= MaxKeywordLength)
                    {
                        int total = window.Count;
                        for (int i = 0; i < total; i++)
                        {
                            yield return window.Dequeue();
                        }
                    }
                }
                int totalCount = window.Count;
                for (int i = 0; i < totalCount; i++)
                {
                    yield return window.Dequeue();
                }
            }
        }
    }
}
