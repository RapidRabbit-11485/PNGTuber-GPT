using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class CPHInline
{
    private string responseVariable; // Variable to store the response value

    public bool Execute()
    {
        try
        {
            string contextFilePath = args["CONTEXT_FILE_PATH"].ToString();
            string keywordContextFilePath = args["KEYWORD_FILE_PATH"].ToString();
            string userName = args["userName"].ToString();
            string rawInput = args["rawInput"].ToString();
            string context = File.ReadAllText(contextFilePath);
            Dictionary<string, string> keywordContexts = new Dictionary<string, string>();
            if (File.Exists(keywordContextFilePath))
            {
                string jsonContent = File.ReadAllText(keywordContextFilePath);
                keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            }

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
                    context += $"\n{keywordPhrase} {keywordValue}\n";
                    break;
                }
            }

            if (keywordContexts.ContainsKey(userName))
            {
                string usernamePhrase = $"something you know about {userName} is";
                string usernameValue = keywordContexts[userName];
                context += $"\n{usernamePhrase} {usernameValue}\n";
            }

            string combinedContext = keywordMatch ? context : context;

            CPH.LogInfo("Combined Context: " + combinedContext);

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

            GenerateChatCompletion(prompt, combinedContext).Wait();
            if (string.IsNullOrEmpty(responseVariable))
            {
                responseVariable = "ChatGPT did not return a response.";
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

    public async Task GenerateChatCompletion(string prompt, string combinedContext)
    {
        string apiKey = args["OPENAI_API_KEY"].ToString();
        CPH.LogInfo("API Key: " + apiKey);
        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";
        var completionsRequest = new
        {
            model = "gpt-3.5-turbo",
            messages = new List<Message>
            {
                new Message
                {
                    role = "system",
                    content = combinedContext
                },
                new Message
                {
                    role = "user",
                    content = prompt
                }
            }
        };
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
                        CPH.LogInfo("Response: " + responseVariable);
                        return;
                    }

                    responseVariable = generatedText;
                    CPH.LogInfo("Response: " + responseVariable);
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
                    CPH.LogInfo("Moderation Response: " + moderationResponseContent);
                    var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);
                    List<string> flaggedCategories = moderationJsonResponse?.Results?[0]?.Categories
                        ?.Where(category => category.Value && !excludedCategories.Contains(category.Key))
                        .Select(category => category.Key)
                        .ToList() ?? new List<string>();
                    CPH.LogInfo("Flagged Categories: " + string.Join(", ", flaggedCategories));
                    return flaggedCategories;
                }
            }
        }
    }
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
