using System;
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
    public Queue<chatMessage> PromptResponseLog { get; set; } = new Queue<chatMessage>(); // Store previous prompts and responses in a queue
    public Queue<chatMessage> ChatGPTLog { get; set; } = new Queue<chatMessage>(); // Store the chat log in a queue

    public bool Execute()
    {
        if (ChatGPTLog == null)
        {
            CPH.LogInfo("Warning: ChatGPTLog is null.");
        }
        else if (!ChatGPTLog.Any())
        {
            CPH.LogInfo("Warning: ChatGPTLog is empty.");
        }
        else
        {
            string chatLogAsString = JsonConvert.SerializeObject(ChatGPTLog, Formatting.Indented);
            CPH.LogInfo("ChatGPTLog Content: " + chatLogAsString);
        }

        try
        {
            string projectFilePath = args["PROJECT_FILE_PATH"].ToString();
            string contextFilePath = projectFilePath + "\\Context.txt";
            string keywordContextFilePath = projectFilePath + "\\keyword_contexts.json";
            string userName = args["userName"].ToString();
            string rawInput = args["rawInput"].ToString();
            bool stripEmojis = args.ContainsKey("stripEmojis") && bool.TryParse(args["stripEmojis"].ToString(), out bool result) ? result : false;
            Dictionary<string, string> keywordContexts = new Dictionary<string, string>();
            if (File.Exists(keywordContextFilePath))
            {
                string jsonContent = File.ReadAllText(keywordContextFilePath);
                keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            }

            string context = File.ReadAllText(contextFilePath);
            string broadcaster = args["broadcaster"].ToString();
            string currentTitle = args["currentTitle"].ToString();
            string currentGame = args["currentGame"].ToString();
            string combinedPrompt = $"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}";
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

            string excludedCategoriesString = args["excludedModCategories"].ToString();
            string[] excludedCategories = excludedCategoriesString.Split(',');
            List<string> flaggedCategories = CallModerationEndpoint(prompt, excludedCategories);
            if (flaggedCategories.Except(excludedCategories).Any())
            {
                responseVariable = $"{userName} your request flagged for harmful or hateful content. Repeated attempts at abuse will result in a permanent ban.";
                CPH.SetArgument("response", responseVariable);
                CPH.LogInfo("Response: " + responseVariable);
                return true;
            }

            GenerateChatCompletion(prompt, combinedPrompt).Wait();
            if (string.IsNullOrEmpty(responseVariable))
            {
                responseVariable = "ChatGPT did not return a response.";
            }

            if (stripEmojis)
            {
                responseVariable = RemoveEmojis(responseVariable);
            }

            CPH.SetGlobalVar("response", responseVariable, false);
            CPH.LogInfo("Response: " + responseVariable);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogInfo("Exception: " + ex.ToString());
            return false;
        }
    }

    public async Task GenerateChatCompletion(string prompt, string combinedPrompt)
    {
        string initialResponse = "I am ready to receive messages, and will comply with your instructions.";
        string chatResponse = "I have received the chat log and will reference users or questions from there that are relevant to the current prompt";
        string apiKey = args["OPENAI_API_KEY"].ToString();
        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";
        var messages = new List<chatMessage>
        {
            new chatMessage
            {
                role = "system",
                content = combinedPrompt
            },
            new chatMessage
            {
                role = "assistant",
                content = initialResponse
            }
        };
        // Iterate over the ChatGPTLog variable to add each message to the messages list
        foreach (var chatMessage in ChatGPTLog)
        {
            messages.Add(new chatMessage { role = chatMessage.role, content = chatMessage.content });
        }

        // Add previous prompts and responses to the messages list from PromptResponseLog
        foreach (var chatMessage in PromptResponseLog)
        {
            messages.Add(new chatMessage { role = chatMessage.role, content = chatMessage.content });
        }

        messages.Add(new chatMessage { role = "assistant", content = chatResponse });
        prompt = "You must respond in less than 500 characters. " + prompt;
        messages.Add(new chatMessage { role = "user", content = prompt });
        var completionsRequest = new
        {
            model = "gpt-3.5-turbo",
            messages = messages
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

        // Save the current prompt and response to the PromptResponseLog queue
        PromptResponseLog.Enqueue(new chatMessage { role = "user", content = prompt });
        PromptResponseLog.Enqueue(new chatMessage { role = "assistant", content = responseVariable });
        // Limit the queue size, for example to 5 responses, to keep the memory usage in check
        while (PromptResponseLog.Count > 5)
        {
            PromptResponseLog.Dequeue();
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

    public class ChatCompletionsResponse
    {
        public List<Choice> Choices { get; set; }
    }

    public class Choice
    {
        public string finish_reason { get; set; }
        public chatMessage Message { get; set; }
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

    public bool SaveMessage()
    {
        // Retrieving the 'ignoreBotNames' value from args
        string ignoreNamesString = args["ignoreBotNames"].ToString();
        if (string.IsNullOrWhiteSpace(ignoreNamesString))
        {
            CPH.LogInfo("'ignoreBotNames' value is either not found or not a valid string.");
            return false;
        }

        List<string> ignoreNamesList = new List<string>(ignoreNamesString.Split(','));
        // Trim any spaces from userNames
        for (int i = 0; i < ignoreNamesList.Count; i++)
        {
            ignoreNamesList[i] = ignoreNamesList[i].Trim();
        }

        // Retrieving the 'userName' value from args
        string currentuserName = args["userName"].ToString();
        if (string.IsNullOrWhiteSpace(currentuserName))
        {
            CPH.LogInfo("'userName' value is either not found or not a valid string.");
            return false;
        }

        // Check if the current userName exists in the ignore list, and if so, return
        if (ignoreNamesList.Contains(currentuserName))
        {
            return false; // This means we are not processing this message as we are ignoring it
        }

        //ChatLog is null, so we need to create it
        if (ChatGPTLog == null)
        {
            ChatGPTLog = new Queue<chatMessage>();
        }

        string msg;
        if (args.ContainsKey("messageStripped"))
        {
            msg = args["messageStripped"].ToString();
        }
        else
        {
            CPH.LogInfo("Key 'messageStripped' not found in args.");
            return false;
        }

        chatMessage gptMsg = new chatMessage();
        gptMsg.role = "user";
        gptMsg.content = $"-{currentuserName} says {msg}"; // Using currentuserName here
        string messageContent = $"Queued message -{currentuserName} says {msg}";
        CPH.LogInfo("{messageContent}");
        QueueMessage(gptMsg);
        return true;
    }

    private void QueueMessage(chatMessage gptMsg)
    {
        ChatGPTLog.Enqueue(gptMsg);
        if (ChatGPTLog.Count > 20)
        {
            ChatGPTLog.Dequeue();
        }
    }

    // Clear internal history
    public bool ClearHistory()
    {
        ChatGPTLog.Clear();
        return true;
    }

    public class chatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}