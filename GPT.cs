using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class CPHInline
{
    public Queue<chatMessage> GPTLog { get; set; } = new Queue<chatMessage>(); // Store previous prompts and responses in a queue
    public Queue<chatMessage> ChatLog { get; set; } = new Queue<chatMessage>(); // Store the chat log in a queue

    // Initialize Classes
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

    public class PronounUser
    {
        public string id { get; set; }
        public string login { get; set; }
        public string pronoun_id { get; set; }
    }

    public class Datum
    {
        public string broadcaster_id { get; set; }
        public string broadcaster_login { get; set; }
        public string broadcaster_name { get; set; }
        public string broadcaster_language { get; set; }
        public string game_id { get; set; }
        public string game_name { get; set; }
        public string title { get; set; }
        public int delay { get; set; }
    }

    public class Root
    {
        public List<Datum> data { get; set; }
    }

    public class AllDatas
    {
        public string UserName { get; set; }
        public string gameName { get; set; }
        public string titleName { get; set; }
    }

    private void QueueMessage(chatMessage chatMsg)
    {
        try
        {
            ChatLog.Enqueue(chatMsg);
            if (ChatLog.Count > 20)
            {
                ChatLog.Dequeue();
                CPH.LogInfo("Successfully dequeued a chat message to maintain the queue size.");
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred while dequeuing a chat message: {ex.Message}");
        }
    }

    public class chatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    // Begin initialization
    public bool Execute()
    {
        CPH.LogInfo("Initialization of PNGTuber-GPT successful.");
        return true;
    }

    public bool GetNicknamewPronouns()
    {
        try
        {
            string userName = args["userName"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
            {
                CPH.LogError("'userName' value is either not found or not a valid string.");
                return false;
            }

            // Retrieve Database Path using GetGlobalVar method
            string databasePath = CPH.GetGlobalVar<string>("Database Path");
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                CPH.LogError("'Database Path' value is either not found or not a valid string.");
                return false;
            }

            string filePath = databasePath + "\\preferred_userNames.json";
            if (!File.Exists(filePath))
            {
                CreateDefaultUserNameFile(filePath);
            }

            string preferredUserName = GetPreferredUsername(userName, filePath);
            if (string.IsNullOrWhiteSpace(preferredUserName))
            {
                CPH.LogError("Preferred user name could not be retrieved.");
                return false;
            }

            string pronouns = FetchPronouns(userName);
            string pronounsFormatted = string.IsNullOrEmpty(pronouns) ? "" : $" ({pronouns})";
            string formattedUsername = $"{preferredUserName}{pronounsFormatted}";
            CPH.SetArgument("nicknamePronouns", formattedUsername);
            CPH.SetArgument("Pronouns", pronounsFormatted);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in GetFormattedUsername: {ex.Message}");
            return false;
        }
    }

    public bool GetNickname()
    {
        try
        {
            string userName = args["userName"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
            {
                CPH.LogError("'userName' value is either not found or not a valid string.");
                return false;
            }

            // Retrieve Database Path using GetGlobalVar method
            string databasePath = CPH.GetGlobalVar<string>("Database Path");
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                CPH.LogError("'Database Path' value is either not found or not a valid string.");
                return false;
            }

            string filePath = databasePath + "\\preferred_userNames.json";
            if (!File.Exists(filePath))
            {
                CreateDefaultUserNameFile(filePath);
            }

            string preferredUserName = GetPreferredUsername(userName, filePath);
            if (string.IsNullOrWhiteSpace(preferredUserName))
            {
                CPH.LogError("Preferred user name could not be retrieved.");
                return false;
            }

            CPH.SetArgument("nickname", preferredUserName);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in GetFormattedUsername: {ex.Message}");
            return false;
        }
    }

    private void CreateDefaultUserNameFile(string filePath)
    {
        var defaultUser = new Dictionary<string, string>
        {
            {
                "DefaultUser",
                "Default User"
            }
        };
        string jsonData = JsonConvert.SerializeObject(defaultUser, Formatting.Indented);
        File.WriteAllText(filePath, jsonData);
    }

    private string GetPreferredUsername(string currentUserName, string filePath)
    {
        string preferredUserName = currentUserName;
        try
        {
            if (File.Exists(filePath))
            {
                string jsonData = File.ReadAllText(filePath);
                var userPreferences = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);
                if (userPreferences != null && userPreferences.TryGetValue(currentUserName, out var preferredName))
                {
                    preferredUserName = preferredName;
                }
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"Error reading or deserializing user preferred names: {ex.Message}");
            // Return the original username if an error occurs
            preferredUserName = currentUserName;
        }

        return preferredUserName;
    }

    public bool SetPronouns()
    {
        CPH.SendMessage("You can set your pronouns at https://pronouns.alejo.io/. Your pronouns will be available via a Public API. This means that users of 7TV, FFZ, and BTTV extensions can see your pronouns in chat.", true);
        return true;
    }

    private string FetchPronouns(string username)
    {
        string url = $"https://pronouns.alejo.io/api/users/{username}";
        using (var httpClient = new HttpClient())
        {
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult(); // This makes the call synchronous
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); // This also makes the call synchronous
                var users = JsonConvert.DeserializeObject<List<PronounUser>>(jsonResponse);
                var user = users.FirstOrDefault(u => u.login.Equals(username, StringComparison.OrdinalIgnoreCase));
                if (user != null)
                {
                    return FormatPronouns(user.pronoun_id);
                }
            }
        }

        return null;
    }

    private string FormatPronouns(string pronounId)
    {
        var pronounsList = new List<string>
        {
            "he",
            "she",
            "they",
            "xe",
            "ze",
            "ey",
            "per",
            "ve",
            "it",
            "him",
            "her",
            "them",
            "hir",
            "xis",
            "zer",
            "em",
            "pers",
            "vers",
            "its"
        };
        var formattedPronouns = new List<string>();
        string remainingPronounId = pronounId.ToLower();
        foreach (var pronoun in pronounsList)
        {
            string lowerCasePronoun = pronoun.ToLower();
            if (remainingPronounId.Contains(lowerCasePronoun))
            {
                formattedPronouns.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pronoun));
                remainingPronounId = Regex.Replace(remainingPronounId, $"\\b{lowerCasePronoun}\\b", "");
            }
        }

        return string.Join("/", formattedPronouns);
    }

    public bool GetCurrentNickname()
    {
        try
        {
            string userName = args["userName"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
            {
                CPH.LogError("'userName' value is either not found or not a valid string.");
                return false;
            }

            string nicknamePronouns = args["nicknamePronouns"].ToString();
            if (string.IsNullOrWhiteSpace(nicknamePronouns))
            {
                CPH.LogError("'nicknamePronouns' value is either not found or not a valid string.");
                return false;
            }

            // Split the nicknamePronouns at the first space to extract the nickname
            string[] split = nicknamePronouns.Split(new[] { ' ' }, 2);
            string nickname = split[0];
            if (userName.Equals(nickname, StringComparison.OrdinalIgnoreCase))
            {
                // If the userName is the same as the nickname, the user hasn't set a custom nickname.
                CPH.SendMessage($"You don't have a custom nickname set. Your username is: {nicknamePronouns}", true);
            }
            else
            {
                // If the userName is different from the nickname, the user has set a custom nickname.
                CPH.SendMessage($"Your current nickname is: {nicknamePronouns}", true);
            }

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred while getting the current nickname: {ex.Message}");
            return false;
        }
    }

    public bool SetPreferredUsername()
    {
        try
        {
            // Retrieve current user name
            string userName = args["userName"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
            {
                CPH.LogError("'userName' value is either not found or not a valid string.");
                return false;
            }

            string pronouns = args["Pronouns"].ToString();
            if (string.IsNullOrWhiteSpace(pronouns))
            {
                CPH.LogError("'Pronouns' value is either not found or not a valid string.");
                return false;
            }

            // Retrieve the preferred user name
            if (!args.ContainsKey("rawInput"))
            {
                CPH.LogError("Key 'rawInput' not found in args.");
                return false;
            }

            string preferredUserNameInput = args["rawInput"].ToString();
            if (string.IsNullOrWhiteSpace(preferredUserNameInput))
            {
                CPH.LogError("Preferred user name input is either not found or not a valid string.");
                return false;
            }

            // Retrieve Database Path using GetGlobalVar method
            string databasePath = CPH.GetGlobalVar<string>("Database Path");
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                CPH.LogError("'Database Path' value is either not found or not a valid string.");
                return false;
            }

            string filePath = databasePath + "\\preferred_userNames.json";
            // Check if the file exists, if not, create it
            Dictionary<string, string> userPreferences;
            if (!File.Exists(filePath))
            {
                userPreferences = new Dictionary<string, string>
                {
                    {
                        "DefaultUser",
                        "Default User"
                    }
                };
            }
            else
            {
                string jsonData = File.ReadAllText(filePath);
                userPreferences = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);
            }

            // Update the preferred user name
            userPreferences[userName] = preferredUserNameInput;
            // Write the updated user preferences back to the file
            File.WriteAllText(filePath, JsonConvert.SerializeObject(userPreferences, Formatting.Indented));
            CPH.SendMessage($"{userName} your nickname has been set to {preferredUserNameInput}{pronouns}.", true);
            return true;
        }
        catch (Exception ex)
        {
            // Retrieve current user name
            string userName = args["userName"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
            {
                CPH.LogError("'userName' value is either not found or not a valid string.");
                return false;
            }

            CPH.LogError($"An error occurred in SetPreferredUsername: {ex.Message}");
            CPH.SendMessage($"{userName} I was unable to set your nickname. Please try again later.", true);
            return false;
        }
    }

    public bool SaveMessage()
    {
        // Check if the message starts with "!"
        string msg = args["rawInput"].ToString();
        if (string.IsNullOrWhiteSpace(msg))
        {
            CPH.LogError("'rawInput' value is either not found or not a valid string.");
            return false;
        }

        if (msg.StartsWith("!"))
        {
            CPH.LogInfo("Ignoring message because it contains a command.");
            return false;
        }

        // Retrieve bot names to ignore
        string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
        if (string.IsNullOrWhiteSpace(ignoreNamesString))
        {
            CPH.LogError("'Ignore Bot Usernames' value is either not found or not a valid string.");
            return false;
        }

        // Retrieve current user name
        string userName = args["userName"].ToString();
        if (string.IsNullOrWhiteSpace(userName))
        {
            CPH.LogError("'userName' value is either not found or not a valid string.");
            return false;
        }

        // Process list of bot names to ignore
        List<string> ignoreNamesList = new List<string>(ignoreNamesString.Split(','));
        ignoreNamesList = ignoreNamesList.Select(name => name.Trim()).ToList();
        // Check if the current userName exists in the ignore list
        if (ignoreNamesList.Contains(userName, StringComparer.OrdinalIgnoreCase))
        {
            CPH.LogInfo("Ignoring message from bot.");
            return false;
        }

        // Retrieve formatted user name or fall back to userName
        string nicknamePronouns = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : userName;
        // If formattedUserName is not found, log an error
        if (nicknamePronouns == userName)
        {
            CPH.LogError("Nickname with pronouns was not found. Falling back to userName.");
        }

        // Initialize ChatLog if it's null
        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            CPH.LogInfo("ChatLog queue has been initialized.");
        }

        // Retrieve the message content
        if (!args.ContainsKey("rawInput"))
        {
            CPH.LogError("Key 'rawInput' not found in args.");
            return false;
        }

        // Queue the message
        string messageContent = $"Queued message - {nicknamePronouns} says {msg}";
        CPH.LogInfo(messageContent);
        chatMessage chatMsg = new chatMessage
        {
            role = "user",
            content = $"-{nicknamePronouns} says {msg}"};
        QueueMessage(chatMsg);
        // Sleep for 500ms
        System.Threading.Thread.Sleep(250);
        return true;
    }

    // Clear Chat History
    public bool ClearChatHistory()
    {
        try
        {
            ChatLog.Clear();
            CPH.LogInfo("Chat history has been successfully cleared.");
            CPH.SendMessage("Chat history has been cleared.", true);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred while clearing the chat history: {ex.Message}");
            CPH.SendMessage("I was unable to clear the chat history. Check the log file for more details.", true);
            return false;
        }
    }

    public bool PerformModeration()
    {
        try
        {
            string input = args["rawInput"].ToString();
            if (string.IsNullOrWhiteSpace(input))
            {
                CPH.LogError("'rawInput' value is either not found or not a valid string.");
                return false;
            }

            // Load global variables for moderation preferences
            var preferences = new Dictionary<string, bool>
            {
                {
                    "hate_allowed",
                    CPH.GetGlobalVar<bool>("hate_allowed")
                },
                {
                    "hate_threatening_allowed",
                    CPH.GetGlobalVar<bool>("hate_threatening_allowed")
                },
                {
                    "self_harm_allowed",
                    CPH.GetGlobalVar<bool>("self_harm_allowed")
                },
                {
                    "self_harm_intent_allowed",
                    CPH.GetGlobalVar<bool>("self_harm_intent_allowed")
                },
                {
                    "self_harm_instructions_allowed",
                    CPH.GetGlobalVar<bool>("self_harm_instructions_allowed")
                },
                {
                    "harassment_allowed",
                    CPH.GetGlobalVar<bool>("harassment_allowed")
                },
                {
                    "harassment_threatening_allowed",
                    CPH.GetGlobalVar<bool>("harassment_threatening_allowed")
                },
                {
                    "sexual_allowed",
                    CPH.GetGlobalVar<bool>("sexual_allowed")
                },
                {
                    "sexual_minors_allowed",
                    CPH.GetGlobalVar<bool>("sexual_minors_allowed")
                },
                {
                    "violence_allowed",
                    CPH.GetGlobalVar<bool>("violence_allowed")
                },
                {
                    "violence_graphic_allowed",
                    CPH.GetGlobalVar<bool>("violence_graphic_allowed")
                }
            };
            var excludedCategories = preferences.Where(p => p.Value).Select(p => p.Key.Replace("_allowed", "").Replace("_", "/")).ToList();
            List<string> flaggedCategories = CallModerationEndpoint(input, excludedCategories.ToArray());
            if (flaggedCategories == null)
            {
                CPH.LogError("Moderation endpoint failed to respond or responded with an error.");
                return false;
            }

            if (flaggedCategories.Any())
            {
                string flaggedCategoriesString = string.Join(", ", flaggedCategories);
                string outputMessage = $"Your message was flagged in the following categories: {flaggedCategoriesString}. Repeated attempts at abuse may result in a ban.";
                string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
                if (string.IsNullOrWhiteSpace(voiceAlias))
                {
                    CPH.LogError("'Voice Alias' global variable is not found or not a valid string.");
                    CPH.SendMessage("I was unable to speak that message. Check the log for the error.", true);
                    return false;
                }

                int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
                CPH.LogInfo(outputMessage);
                CPH.SendMessage(outputMessage, true);
                return false;
            }
            else
            {
                CPH.SetArgument("moderatedMessage", input);
                return true;
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in PerformModeration: {ex.Message}");
            return false;
        }
    }

    private List<string> CallModerationEndpoint(string prompt, string[] excludedCategories)
    {
        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            CPH.LogError("The OpenAI API Key is not set or is invalid.");
            return null;
        }

        try
        {
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
                        CPH.LogInfo($"Moderation Response Content: {moderationResponseContent}");
                        var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);
                        if (moderationJsonResponse?.Results == null || !moderationJsonResponse.Results.Any())
                        {
                            CPH.LogError("No moderation results were returned from the API.");
                            return null;
                        }

                        List<string> flaggedCategories = moderationJsonResponse.Results[0].Categories.Where(category => category.Value && !excludedCategories.Contains(category.Key)).Select(category => category.Key).ToList();
                        return flaggedCategories;
                    }
                }
            }
        }
        catch (WebException webEx)
        {
            using (var stream = webEx.Response?.GetResponseStream())
            using (var reader = new StreamReader(stream ?? new MemoryStream()))
            {
                string responseContent = reader.ReadToEnd();
                CPH.LogError($"A WebException was caught during the moderation request: {responseContent}");
            }

            return null;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An exception occurred while calling the moderation endpoint: {ex.Message}");
            return null;
        }
    }

    public bool Speak()
    {
        try
        {
            string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                CPH.LogError("'Voice Alias' global variable is not found or not a valid string.");
                CPH.SendMessage("I was unable to speak that message. Check the log for the error.", true);
                return false;
            }

            object nicknameObj;
            string userToSpeak = args.TryGetValue("nickname", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : args.TryGetValue("userName", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : "";
            if (string.IsNullOrWhiteSpace(userToSpeak))
            {
                CPH.LogError("Both 'nickname' and 'userName' are not found or are empty strings.");
                CPH.SendMessage("I was unable to speak that message. Check the log for the error.", true);
                return false;
            }

            object messageObj;
            string messageToSpeak = args.TryGetValue("moderatedMessage", out messageObj) && !string.IsNullOrWhiteSpace(messageObj?.ToString()) ? messageObj.ToString() : args.TryGetValue("rawInput", out messageObj) && !string.IsNullOrWhiteSpace(messageObj?.ToString()) ? messageObj.ToString() : "";
            if (string.IsNullOrWhiteSpace(messageToSpeak))
            {
                CPH.LogError("Both 'moderatedMessage' and 'rawInput' are not found or are empty strings.");
                CPH.SendMessage("I was unable to speak that message. Check the log for the error.", true);
                return false;
            }

            string outputMessage = $"{userToSpeak} said {messageToSpeak}";
            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            CPH.LogInfo($"User {userToSpeak} said {messageToSpeak}");
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in Speak: {ex.Message}");
            CPH.SendMessage("I was unable to speak that message. Check the log for the error.", true);
            return false;
        }
    }

    public bool RememberThis()
    {
        try
        {
            string databasePath = CPH.GetGlobalVar<string>("Database Path");
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                CPH.LogError("'Database Path' global variable is not found or not a valid string.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                CPH.LogError("'Voice Alias' global variable is not found or not a valid string.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string userName;
            if (!args.TryGetValue("userName", out object userNameObj) || string.IsNullOrWhiteSpace(userNameObj?.ToString()))
            {
                CPH.LogError("'userName' argument is not found or not a valid string.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            userName = userNameObj.ToString();
            object nicknameObj;
            string userToSpeak = args.TryGetValue("nickname", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : args.TryGetValue("userName", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : "";
            if (string.IsNullOrWhiteSpace(userToSpeak))
            {
                CPH.LogError("Both 'nickname' and 'userName' are not found or are empty strings.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string fullMessage;
            if (args.TryGetValue("moderatedMessage", out object moderatedMessageObj) && !string.IsNullOrWhiteSpace(moderatedMessageObj?.ToString()))
            {
                fullMessage = moderatedMessageObj.ToString();
            }
            else if (args.TryGetValue("rawInput", out object rawInputObj) && !string.IsNullOrWhiteSpace(rawInputObj?.ToString()))
            {
                fullMessage = rawInputObj.ToString();
            }
            else
            {
                CPH.LogError("Both 'moderatedMessage' and 'rawInput' are not found or are empty strings.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            // Process the full message to extract the keyword and the message to remember
            var parts = fullMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                CPH.LogError("The message does not contain enough parts to extract a keyword and a definition.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            // The first word is the keyword, and the rest is the definition
            string keyword = parts[0];
            string definition = string.Join(" ", parts.Skip(1)); // Skip the first word (keyword)
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{}");
            }

            string jsonContent = File.ReadAllText(filePath);
            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            keywordContexts[keyword] = definition; // Save the definition with the keyword
            File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
            CPH.LogInfo($"Keyword: {keyword}, Definition: {definition}, File Path: {filePath}");
            // Speak what we are remembering
            string outputMessage = $"OK, {userToSpeak}, I will remember '{definition}' about '{keyword}'.";
            CPH.SendMessage(outputMessage, true);
            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage);
            CPH.LogInfo($"Bot said: {outputMessage}");
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in RememberThis: {ex.Message}");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
    }

    public bool RememberThisAboutMe()
    {
        try
        {
            string databasePath = CPH.GetGlobalVar<string>("Database Path");
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                CPH.LogError("'Database Path' global variable is not found or not a valid string.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                CPH.LogError("'Voice Alias' global variable is not found or not a valid string.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            object nicknameObj;
            string userToSpeak = args.TryGetValue("nickname", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : args.TryGetValue("userName", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : "";
            if (string.IsNullOrWhiteSpace(userToSpeak))
            {
                CPH.LogError("Both 'nickname' and 'userName' are not found or are empty strings.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string userName;
            if (!args.TryGetValue("userName", out object userNameObj) || string.IsNullOrWhiteSpace(userNameObj?.ToString()))
            {
                CPH.LogError("'userName' argument is not found or not a valid string.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            userName = userNameObj.ToString();
            string messageToRemember;
            if (args.TryGetValue("moderatedMessage", out object moderatedMessageObj) && !string.IsNullOrWhiteSpace(moderatedMessageObj?.ToString()))
            {
                messageToRemember = moderatedMessageObj.ToString();
            }
            else if (args.TryGetValue("rawInput", out object rawInputObj) && !string.IsNullOrWhiteSpace(rawInputObj?.ToString()))
            {
                messageToRemember = rawInputObj.ToString();
            }
            else
            {
                CPH.LogError("Both 'moderatedMessage' and 'rawInput' are not found or are empty strings.");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{}");
            }

            string jsonContent = File.ReadAllText(filePath);
            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            keywordContexts[userName] = messageToRemember;
            File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
            CPH.LogInfo($"Username: {userName}, Message: {messageToRemember}, File Path: {filePath}");
            // Speak what we are remembering
            string outputMessage = $"OK, {userToSpeak}, I will remember {messageToRemember} about you.";
            CPH.SendMessage($"{outputMessage}", true);
            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            CPH.LogInfo($"Bot said {outputMessage}");
            return true;
        }
        catch (JsonException jsonEx)
        {
            CPH.LogError($"JSON error in RememberThisAboutMe: {jsonEx.Message}");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
        catch (IOException ioEx)
        {
            CPH.LogError($"IO error in RememberThisAboutMe: {ioEx.Message}");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in RememberThisAboutMe: {ex.Message}");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
    }

    // Clear Prompt History
    public bool ClearPromptHistory()
    {
        try
        {
            GPTLog.Clear();
            CPH.LogInfo("Prompt history has been successfully cleared.");
            CPH.SendMessage("Prompt history has been cleared.", true);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred while clearing the prompt history: {ex.Message}");
            CPH.SendMessage("I was unable to clear the prompt history. Check the log file for more details.", true);
            return false;
        }
    }

    public bool AskGPT()
    {
        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            CPH.LogInfo("ChatLog queue has been initialized.");
        }
        else
        {
            string chatLogAsString = JsonConvert.SerializeObject(ChatLog, Formatting.Indented);
            CPH.LogInfo("ChatLog Content: " + chatLogAsString);
        }

        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            CPH.LogError("'Voice Alias' global variable is not found or not a valid string.");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Check the log for details.", true);
            int speakResult2 = CPH.TtsSpeak(voiceAlias, "I'm sorry, but I can't answer that question right now. Check the log for details.", false);
            return false;
        }

        string userName;
        if (!args.TryGetValue("userName", out object userNameObj) || string.IsNullOrWhiteSpace(userNameObj?.ToString()))
        {
            CPH.LogError("'userName' argument is not found or not a valid string.");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Check the log for details.", true);
            int speakResult3 = CPH.TtsSpeak(voiceAlias, "I'm sorry, but I can't answer that question right now. Check the log for details.", false);
            return false;
        }

        userName = userNameObj.ToString();
        object nicknameObj;
        string userToSpeak = args.TryGetValue("nicknamePronouns", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : args.TryGetValue("userName", out nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : "";
        if (string.IsNullOrWhiteSpace(userToSpeak))
        {
            CPH.LogError("Both 'nicknamePronouns' and 'userName' are not found or are empty strings.");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Check the log for details.", true);
            int speakResult4 = CPH.TtsSpeak(voiceAlias, "I'm sorry, but I can't answer that question right now. Check the log for details.", false);
            return false;
        }

        string databasePath = CPH.GetGlobalVar<string>("Database Path");
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            CPH.LogError("'Database Path' global variable is not found or not a valid string.");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Check the log for details.", true);
            int speakResult5 = CPH.TtsSpeak(voiceAlias, "I'm sorry, but I can't answer that question right now. Check the log for details.", false);
            return false;
        }

        string fullMessage;
        if (args.TryGetValue("moderatedMessage", out object moderatedMessageObj) && !string.IsNullOrWhiteSpace(moderatedMessageObj?.ToString()))
        {
            fullMessage = moderatedMessageObj.ToString();
        }
        else if (args.TryGetValue("rawInput", out object rawInputObj) && !string.IsNullOrWhiteSpace(rawInputObj?.ToString()))
        {
            fullMessage = rawInputObj.ToString();
        }
        else
        {
            CPH.LogError("Both 'moderatedMessage' and 'rawInput' are not found or are empty strings.");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Check the log for details.", true);
            int speakResult6 = CPH.TtsSpeak(voiceAlias, "I'm sorry, but I can't answer that question right now. Check the log for details.", false);
            return false;
        }

        string ContextFilePath = Path.Combine(databasePath, "context.txt");
        string keywordContextFilePath = Path.Combine(databasePath, "keyword_contexts.json");
        bool stripEmojis = CPH.GetGlobalVar<bool>("Strip Emojis From Response", true);
        Dictionary<string, string> keywordContexts = new Dictionary<string, string>();
        if (File.Exists(keywordContextFilePath))
        {
            string jsonContent = File.ReadAllText(keywordContextFilePath);
            keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
        }

        string context = File.ReadAllText(ContextFilePath);
        string broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
        string currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
        string currentGame = CPH.GetGlobalVar<string>("currentGame", false);
        string contextBody = $"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}";
        string prompt = userToSpeak + " asks " + fullMessage;
        var mentionedKeywords = keywordContexts.Keys;
        bool keywordMatch = false;
        foreach (var keyword in mentionedKeywords)
        {
            if (prompt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                keywordMatch = true;
                string keywordPhrase = $"something you know about {keyword} is";
                string keywordValue = keywordContexts[keyword];
                contextBody += $"\n{keywordPhrase} {keywordValue}\n";
                break;
            }
        }

        if (keywordContexts.ContainsKey(userName))
        {
            string usernamePhrase = $"something you know about {userToSpeak} is";
            string usernameValue = keywordContexts[userName];
            contextBody += $"\n{usernamePhrase} {usernameValue}\n";
        }

        try
        {
            string GPTResponse = GenerateChatCompletion(prompt, contextBody);
            if (string.IsNullOrEmpty(GPTResponse))
            {
                GPTResponse = null;
                CPH.LogError("ChatGPT did not return a response.");
                CPH.SendMessage("I'm sorry, but I can't answer that question right now. Check the log for details.", true);
                int speakResult6 = CPH.TtsSpeak(voiceAlias, "I'm sorry, but I can't answer that question right now. Check the log for details.", false);
            }

            if (stripEmojis)
            {
                GPTResponse = RemoveEmojis(GPTResponse);
            }

            CPH.LogInfo("Response: " + GPTResponse);
            int speakResult = CPH.TtsSpeak(voiceAlias, GPTResponse, false);
            System.Threading.Thread.Sleep(15000);
            // Split the response into chunks of 500 characters
            for (int i = 0; i < GPTResponse.Length; i += 500)
            {
                string messageChunk = GPTResponse.Substring(i, Math.Min(500, GPTResponse.Length - i));
                CPH.SendMessage(messageChunk, true);
                // Sleep after sending each message chunk to avoid flooding
                System.Threading.Thread.Sleep(1000);
            }

            return true;
        }
        catch (AggregateException aggEx)
        {
            // If there was an exception during the task, it will be wrapped in an AggregateException
            foreach (var ex in aggEx.InnerExceptions)
            {
                CPH.LogError($"An error occurred in AskGPT: {ex.Message}");
            }

            return false;
        }
        catch (Exception ex)
        {
            CPH.LogError($"An error occurred in AskGPT: {ex.Message}");
            return false;
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

    public string GenerateChatCompletion(string prompt, string contextBody)
    {
        string generatedText = string.Empty;
        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);
        string initialResponse = "I am ready to receive messages, and will comply with your instructions.";
        string chatResponse = "I have received the chat log and will reference users or questions from there that are relevant to the current prompt";
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(AIModel))
        {
            CPH.LogError("One or more configuration values are missing or invalid.");
            return "Configuration error. Please check the log for details.";
        }

        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";
        var messages = new List<chatMessage>
        {
            new chatMessage
            {
                role = "system",
                content = contextBody
            },
            new chatMessage
            {
                role = "assistant",
                content = initialResponse
            }
        };
        // Iterate over the ChatGPTLog variable to add each message to the messages list
        foreach (var chatMessage in ChatLog)
        {
            messages.Add(new chatMessage { role = chatMessage.role, content = chatMessage.content });
        }

        messages.Add(new chatMessage { role = "assistant", content = chatResponse });
        // Add previous prompts and responses to the messages list from PromptResponseLog
        foreach (var chatMessage in GPTLog)
        {
            messages.Add(new chatMessage { role = chatMessage.role, content = chatMessage.content });
        }
		string discordPrompt = prompt;
        prompt = "You must respond in less than 500 characters. " + prompt;
        messages.Add(new chatMessage { role = "user", content = prompt });
        var completionsRequest = new
        {
            model = AIModel,
            messages = messages
        };
        string completionsRequestJSON = JsonConvert.SerializeObject(completionsRequest, Formatting.Indented);
        CPH.LogInfo($"Request: {completionsRequestJSON}");
        var completionsJsonPayload = JsonConvert.SerializeObject(completionsRequest);
        WebRequest completionsWebRequest = WebRequest.Create(completionsEndpoint);
        completionsWebRequest.Method = "POST";
        completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        completionsWebRequest.ContentType = "application/json";
        try
        {
            using (Stream requestStream = completionsWebRequest.GetRequestStream())
            {
                byte[] completionsContentBytes = Encoding.UTF8.GetBytes(completionsJsonPayload);
                requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
            }

            using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
            {
                using (StreamReader responseReader = new StreamReader(completionsWebResponse.GetResponseStream()))
                {
                    string completionsResponseContent = responseReader.ReadToEnd();
                    CPH.LogInfo("Completions Response: " + completionsResponseContent);
                    var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                    generatedText = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                }
            }
        }
        catch (WebException webEx)
        {
            CPH.LogError($"A WebException was caught: {webEx.Message}");
            if (webEx.Response != null)
            {
                using (var reader = new StreamReader(webEx.Response.GetResponseStream()))
                {
                    CPH.LogError(reader.ReadToEnd());
                }
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"An exception occurred: {ex.Message}");
        }

        if (string.IsNullOrEmpty(generatedText))
        {
            generatedText = "ChatGPT did not return a response.";
        }

        // The following line replaces line breaks with spaces in the generated text
        generatedText = generatedText.Replace("\r\n", " ").Replace("\n", " ");
        // Assuming GPTLog is a Queue or similar collection where we store logs
        GPTLog.Enqueue(new chatMessage { role = "user", content = prompt });
        GPTLog.Enqueue(new chatMessage { role = "assistant", content = generatedText });
        // Post question and answer to Discord
        bool logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
        if (logDiscord) // corrected equality check and lowercase 'if'
        {
            string discordWebhookUrl = CPH.GetGlobalVar<string>("Discord Webhook URL", true);
            string discordUsername = CPH.GetGlobalVar<string>("Discord Bot Username", true);
            string discordAvatarUrl = CPH.GetGlobalVar<string>("Discord Avatar Url", true);
            string discordOutput = $"Question: {discordPrompt}\nAnswer: {generatedText}"; // corrected newline escape
            CPH.LogInfo($"Discord Message Sent: {discordWebhookUrl} {discordOutput} {discordUsername} {discordAvatarUrl}");
            CPH.DiscordPostTextToWebhook(discordWebhookUrl, discordOutput, discordUsername, discordAvatarUrl, false);
        }

        return generatedText;
    }

    public bool GetStreamInfo()
    {
        // your main code goes here
        Task<AllDatas> getAllDatasTask = FunctionGetAllDatas();
        getAllDatasTask.Wait();
        AllDatas datas = getAllDatasTask.Result;
        string broadcaster = datas.UserName;
        CPH.SetGlobalVar("broadcaster", broadcaster, false);
        string currentGame = datas.gameName;
        CPH.SetGlobalVar("currentGame", currentGame, false);
        string currentTitle = datas.titleName;
        CPH.SetGlobalVar("currentTitle", currentTitle, false);
        CPH.LogInfo($"Retrieved current information: {broadcaster} {currentGame} {currentTitle}");
        return true;
    }

    public HttpClient client = new HttpClient();
    public async Task<AllDatas> FunctionGetAllDatas()
    {
        string to_id = args["broadcastUserId"].ToString();
        string tokenValue = CPH.TwitchOAuthToken;
        string clientIdValue = CPH.TwitchClientId;
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("client-ID", clientIdValue);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
        HttpResponseMessage response = await client.GetAsync("https://api.twitch.tv/helix/channels?broadcaster_id=" + to_id);
        HttpContent responseContent = response.Content;
        string responseBody = await response.Content.ReadAsStringAsync();
        Root root = JsonConvert.DeserializeObject<Root>(responseBody);
        return new AllDatas
        {
            UserName = root.data[0].broadcaster_name,
            gameName = root.data[0].game_name,
            titleName = root.data[0].title,
        };
    }

    public bool Version()
    {
        CPH.SendMessage("PNGTuber-GPT v1.1", true);
        return true;
    }
}