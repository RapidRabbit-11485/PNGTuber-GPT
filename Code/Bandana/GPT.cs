﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class CPHInline
{
    private string responseVariable; // Variable to store the response value
    public bool Execute()
    {
        try
        {
            string projectFilePath = args["PROJECT_FILE_PATH"].ToString();
            string contextFilePath = projectFilePath + "\\Context.txt";
            string keywordContextFilePath = projectFilePath + "\\keyword_contexts.json";
            string twitchChatFilePath = projectFilePath + "\\message_log.json";
            string userName = args["userName"].ToString();
            string rawInput = args["rawInput"].ToString();
            bool stripEmojis = args.ContainsKey("stripEmojis") && bool.TryParse(args["stripEmojis"].ToString(), out bool result) ? result : false;
            Dictionary<string, string> keywordContexts = new Dictionary<string, string>();
            // Read keywordContexts from the JSON file
            if (File.Exists(keywordContextFilePath))
            {
                string jsonContent = File.ReadAllText(keywordContextFilePath);
                keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            }

            // Parse the Twitch Chat JSON and extract displayName and message for each line
            string[] twitchChatLines = File.ReadAllLines(twitchChatFilePath);
            StringBuilder twitchChatBuilder = new StringBuilder();
            foreach (string line in twitchChatLines)
            {
                ChatMessage chatMessage = JsonConvert.DeserializeObject<ChatMessage>(line);
                string displayName = chatMessage.displayName;
                string message = chatMessage.message;
                twitchChatBuilder.AppendLine($"{displayName}: {message}");
            }

            // Combine the parsed Twitch Chat with the context
            string parsedTwitchChat = twitchChatBuilder.ToString();
            string context = File.ReadAllText(contextFilePath);
            string broadcaster = args["broadcaster"].ToString();
            string currentTitle = args["currentTitle"].ToString();
            string currentGame = args["currentGame"].ToString();
            string combinedPrompt = $"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}";
            string chatContext = "Here is the current chat log of people that have been chatting. You can interact with them and ask them questions sometimes.\n";
            chatContext += parsedTwitchChat;
            string prompt = userName + " asks " + rawInput;
            var mentionedKeywords = keywordContexts.Keys;
            bool keywordMatch = false;
            foreach (var keyword in mentionedKeywords)
            {
                if (prompt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    keywordMatch = true;
                    string keywordPhrase = $"something you know about {keyword} is";
                    string keywordValue = keywordContexts[keyword];
                    combinedPrompt += $"\n{keywordPhrase} {keywordValue}\n";
                    break;
                }
            }

            if (keywordContexts.ContainsKey(userName))
            {
                string usernamePhrase = $"something you know about {userName} is";
                string usernameValue = keywordContexts[userName];
                combinedPrompt += $"\n{usernamePhrase} {usernameValue}\n";
            }

            string[] excludedCategories =
            {
                "sexual",
                "violence"
            };
            List<string> flaggedCategories = CallModerationEndpoint(prompt, excludedCategories);
            if (flaggedCategories.Except(excludedCategories).Any())
            {
                string flaggedCategoriesString = string.Join(", ", flaggedCategories);
                responseVariable = $"{userName} your request flagged for harmful or hateful content. Repeated attempts at abuse will result in a permanent ban.";
                CPH.SetArgument("response", responseVariable);
                CPH.LogInfo("Response: " + responseVariable);
                return true;
            }

            GenerateChatCompletion(prompt, combinedPrompt, chatContext).Wait();
            if (string.IsNullOrEmpty(responseVariable))
            {
                responseVariable = "ChatGPT did not return a response.";
            }

            // Remove emojis if stripEmojis is true
            if (stripEmojis)
            {
                responseVariable = RemoveEmojis(responseVariable);
            }

            CPH.SetArgument("response", responseVariable);
            CPH.LogInfo("Response: " + responseVariable);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogInfo("Exception: " + ex.ToString());
            return false;
        }
    }

    public async Task GenerateChatCompletion(string prompt, string combinedPrompt, string chatContext)
    {
        string coreDirectives = "You must respond in less than 500 total characters for each message or part of your response will be cut off.\n";
        coreDirectives += "Do not preface your responses with your name and a colon in any of your messages.\n";
        coreDirectives += "Any time your response has the word uwu in it replace it with oowoo.\n";
        coreDirectives += "Any time your response has the word owo in it replace it with ohwoe.\n";
        coreDirectives += "Do not say your character's name in your responses\n";
        coreDirectives += "Always respond in the first person.\n";
        coreDirectives += "You should use swear words in your responses sporadically";
        string apiKey = args["OPENAI_API_KEY"].ToString();
        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";
        string initialResponse = "I am ready to receive messages, and will comply with your instructions.";
        string chatResponse = "I have received the chat log and will reference users or questions from there that are relevant to the current prompt";
        var completionsRequest = new
        {
            model = "gpt-3.5-turbo",
            messages = new List<Message>
            {
                new Message
                {
                    role = "system",
                    content = combinedPrompt
                },
                new Message
                {
                    role = "user",
                    content = coreDirectives
                },
                new Message
                {
                    role = "assistant",
                    content = initialResponse
                },
                new Message
                {
                    role = "user",
                    content = chatContext
                },
                new Message
                {
                    role = "assistant",
                    content = chatResponse
                },
                new Message
                {
                    role = "user",
                    content = prompt
                }
            }
        };
        string completionsRequestJSON = JsonConvert.SerializeObject(completionsRequest, Formatting.Indented);
        CPH.LogInfo("Request Body: " + completionsRequestJSON);
        var completionsJsonPayload = JsonConvert.SerializeObject(completionsRequest);
        var completionsContentBytes = Encoding.UTF8.GetBytes(completionsJsonPayload);
        WebRequest completionsWebRequest = WebRequest.Create(completionsEndpoint);
        completionsWebRequest.Method = "POST";
        completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        completionsWebRequest.ContentType = "application/json";
        completionsWebRequest.ContentLength = completionsContentBytes.Length;
        using (Stream requestStream = completionsWebRequest.GetRequestStream())
        {
            requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
        }

        using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
        {
            using (Stream responseStream = completionsWebResponse.GetResponseStream())
            {
                using (StreamReader responseReader = new StreamReader(responseStream))
                {
                    string completionsResponseContent = responseReader.ReadToEnd();
                    CPH.LogInfo("Completions Response: " + completionsResponseContent);
                    var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                    string generatedText = completionsJsonResponse?.Choices?[0]?.Message?.content ?? string.Empty;
                    if (string.IsNullOrEmpty(generatedText))
                    {
                        responseVariable = "ChatGPT did not return a response.";
                        CPH.LogInfo(responseVariable);
                        return;
                    }

                    // Replace line breaks with spaces in the generated text
                    generatedText = generatedText.Replace("\r\n", " ").Replace("\n", " ");
                    responseVariable = generatedText;
                }
            }
        }
    }

    public List<string> CallModerationEndpoint(string prompt, string[] excludedCategories)
    {
        string apiKey = args["OPENAI_API_KEY"].ToString();
        string moderationEndpoint = "https://api.openai.com/v1/moderations";
        var moderationRequestBody = new
        {
            input = prompt
        };
        var moderationJsonPayload = JsonConvert.SerializeObject(moderationRequestBody);
        var moderationContentBytes = Encoding.UTF8.GetBytes(moderationJsonPayload);
        WebRequest moderationWebRequest = WebRequest.Create(moderationEndpoint);
        moderationWebRequest.Method = "POST";
        moderationWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        moderationWebRequest.ContentType = "application/json";
        moderationWebRequest.ContentLength = moderationContentBytes.Length;
        using (Stream requestStream = moderationWebRequest.GetRequestStream())
        {
            requestStream.Write(moderationContentBytes, 0, moderationContentBytes.Length);
        }

        using (WebResponse moderationWebResponse = moderationWebRequest.GetResponse())
        {
            using (Stream responseStream = moderationWebResponse.GetResponseStream())
            {
                using (StreamReader responseReader = new StreamReader(responseStream))
                {
                    string moderationResponseContent = responseReader.ReadToEnd();
                    CPH.LogInfo("Prompt: " + prompt);
                    CPH.LogInfo("Moderation Response: " + moderationResponseContent);
                    var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);
                    List<string> flaggedCategories = moderationJsonResponse?.Results?[0]?.Categories?.Where(category => category.Value && !excludedCategories.Contains(category.Key)).Select(category => category.Key).ToList() ?? new List<string>();
                    CPH.LogInfo("Flagged Categories: " + string.Join(", ", flaggedCategories));
                    return flaggedCategories;
                }
            }
        }
    }

    private string RemoveEmojis(string text)
    {
        // Regular expression pattern to match non-ASCII characters (emojis)
        string nonAsciiPattern = @"[^\u0000-\u007F]+";
        // Regular expression to match and remove emojis (non-ASCII characters) from the 'text' string
        text = Regex.Replace(text, nonAsciiPattern, "");
        // Remove any extra spaces left after removing the emojis
        text = text.Trim();
        return text;
    }

    public class ChatMessage
    {
        public string displayName { get; set; }
        public string message { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class ChatCompletionsResponse
    {
        public List<Choice> Choices { get; set; }
    }

    public class Choice
    {
        public string finish_reason { get; set; }
        public Message Message { get; set; }
    }

    public class ModerationResponse
    {
        public List<Result> Results { get; set; }
    }

    public class Result
    {
        public bool Flagged { get; set; }
        public Dictionary<string, bool> Categories { get; set; }
        public Dictionary<string, double> Category_scores { get; set; }
    }
}