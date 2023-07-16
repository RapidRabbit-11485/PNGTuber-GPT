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
            // Set the file paths for context and keyword-contexts
            string contextFilePath = args["CONTEXT_FILE_PATH"].ToString();
            string keywordContextFilePath = args["KEYWORD_FILE_PATH"].ToString();
            // Read the username and raw input
            string userName = args["userName"].ToString();
            string rawInput = args["rawInput"].ToString();
            // Read the context from a text file
            string context = File.ReadAllText(contextFilePath);
            // Read the keyword-context mapping from a JSON file
            Dictionary<string, string> keywordContexts = new Dictionary<string, string>();
            if (File.Exists(keywordContextFilePath))
            {
                string jsonContent = File.ReadAllText(keywordContextFilePath);
                keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            }

            // Set up the prompt with the provided context
            string prompt = userName + " asks " + rawInput;
            // Check if any keywords are mentioned in the prompt
            var mentionedKeywords = keywordContexts.Keys;
            bool keywordMatch = false;
            // Check if any mentioned keywords are exact matches in the prompt
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

            // Set up the prompt with the updated context
            string combinedContext = keywordMatch ? context : "";
            // Log the combinedContext
            CPH.LogInfo("Combined Context: " + combinedContext);
            // Specify the excluded categories for moderation
            string[] excludedCategories =
            {
                "sexual",
                "hate"
            }; // Add the categories you want to exclude
            // Call the moderation endpoint
            List<string> flaggedCategories = CallModerationEndpoint(prompt, excludedCategories);
            // Check if any categories are flagged
            if (flaggedCategories.Except(excludedCategories).Any())
            {
                string flaggedCategoriesString = string.Join(", ", flaggedCategories);
                responseVariable = $"{userName} your request flagged for harmful or hateful content. Repeated attempts at abuse will result in a permanent ban.";
                CPH.SetArgument("response", responseVariable);
                CPH.LogInfo("Response: " + responseVariable);
                return true; // Return early if the prompt is flagged
            }

            // Execute the GPT Query and store the response in the variable
            GenerateChatCompletion(prompt, combinedContext).Wait();
            // Set the response using the stored variable
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
            // Log any exceptions that occur
            CPH.LogInfo("Exception: " + ex.ToString());
            return false;
        }
    }

    public async Task GenerateChatCompletion(string prompt, string combinedContext)
    {
        // Set up your OpenAI API credentials
        string apiKey = args["OPENAI_API_KEY"].ToString();
        CPH.LogInfo("API Key: " + apiKey);
        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";
        // Create the completions request payload
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
        // Serialize the completions request to JSON
        var completionsJsonPayload = JsonConvert.SerializeObject(completionsRequest);
        var completionsContentBytes = Encoding.UTF8.GetBytes(completionsJsonPayload);
        // Create the completions request
        WebRequest completionsWebRequest = WebRequest.Create(completionsEndpoint);
        completionsWebRequest.Method = "POST";
        completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        completionsWebRequest.ContentType = "application/json";
        completionsWebRequest.ContentLength = completionsContentBytes.Length;
        // Write the completions request payload to the request stream
        using (Stream requestStream = completionsWebRequest.GetRequestStream())
        {
            requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
        }

        // Send the completions request and get the response
        using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
        {
            // Read the completions response
            using (Stream responseStream = completionsWebResponse.GetResponseStream())
            {
                using (StreamReader responseReader = new StreamReader(responseStream))
                {
                    string completionsResponseContent = responseReader.ReadToEnd();
                    // Log the completions response
                    CPH.LogInfo("Completions Response: " + completionsResponseContent);
                    // Deserialize the response JSON for chat completions
                    var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                    // Extract the generated text from the response
                    string generatedText = completionsJsonResponse?.Choices?[0]?.Message?.content ?? string.Empty;
                    // Check if generated text is empty
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
        // Set up your OpenAI API credentials
        string apiKey = args["OPENAI_API_KEY"].ToString();
        string moderationEndpoint = "https://api.openai.com/v1/moderations";
        // Set up the request payload for moderation
        var moderationRequestBody = new
        {
            input = prompt
        };
        // Serialize the request payload for moderation
        var moderationJsonPayload = JsonConvert.SerializeObject(moderationRequestBody);
        var moderationContentBytes = Encoding.UTF8.GetBytes(moderationJsonPayload);
        // Create the moderation request
        WebRequest moderationWebRequest = WebRequest.Create(moderationEndpoint);
        moderationWebRequest.Method = "POST";
        moderationWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        moderationWebRequest.ContentType = "application/json";
        moderationWebRequest.ContentLength = moderationContentBytes.Length;
        // Write the moderation request payload to the request stream
        using (Stream requestStream = moderationWebRequest.GetRequestStream())
        {
            requestStream.Write(moderationContentBytes, 0, moderationContentBytes.Length);
        }

        // Send the moderation request and get the response
        using (WebResponse moderationWebResponse = moderationWebRequest.GetResponse())
        {
            // Read the moderation response
            using (Stream responseStream = moderationWebResponse.GetResponseStream())
            {
                using (StreamReader responseReader = new StreamReader(responseStream))
                {
                    string moderationResponseContent = responseReader.ReadToEnd();
                    // Log the moderation response
                    CPH.LogInfo("Moderation Response: " + moderationResponseContent);
                    // Deserialize the response JSON for moderation
                    var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);
                    // Check if any categories are flagged
                    List<string> flaggedCategories = moderationJsonResponse?.Results?[0]?.Categories
                        ?.Where(category => category.Value && !excludedCategories.Contains(category.Key))
                        .Select(category => category.Key)
                        .ToList() ?? new List<string>();
                    // Log the flagged categories
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
