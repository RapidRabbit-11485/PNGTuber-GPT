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
using System.Security.Cryptography;
using Newtonsoft.Json;

public class CPHInline
{
    public Queue<chatMessage> GPTLog { get; set; } = new Queue<chatMessage>(); 
    public Queue<chatMessage> ChatLog { get; set; } = new Queue<chatMessage>(); 

    public class AppSettings
    {
        public string OpenApiKey { get; set; }
        public string OpenAiModel { get; set; }
        public string DatabasePath { get; set; }
        public string IgnoreBotUsernames { get; set; }
        public string VoiceAlias { get; set; }
        public string StripEmojisFromResponse { get; set; }
        public string LoggingLevel { get; set; }
        public string Version { get; set; }
        public string HateAllowed { get; set; }
        public string HateThreateningAllowed { get; set; }
        public string SelfHarmAllowed { get; set; }
        public string ViolenceAllowed { get; set; }
        public string SelfHarmIntentAllowed { get; set; }
        public string SelfHarmInstructionsAllowed { get; set; }
        public string HarassmentAllowed { get; set; }
        public string HarassmentThreateningAllowed { get; set; }
        public string IllicitAllowed { get; set; }
        public string IllicitViolentAllowed { get; set; }
        public string LogGptQuestionsToDiscord { get; set; }
        public string DiscordWebhookUrl { get; set; }
        public string DiscordBotUsername { get; set; }
        public string DiscordAvatarUrl { get; set; }
        public string PostToChat { get; set; }
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

    public class chatMessage
    {

        public string role { get; set; }

        public string content { get; set; }

        public override string ToString()
        {
            return $"Role: {role}, Content: {content}";
        }
    }

    public class PronounCacheEntry
    {

        public string FormattedPronouns { get; set; }

        public DateTime Expiration { get; set; }
    }

    private void QueueMessage(chatMessage chatMsg)
    {

        LogToFile($"Entering QueueMessage with chatMsg: {chatMsg}", "DEBUG");
        try
        {

            LogToFile($"Enqueuing chat message: {chatMsg}", "INFO");
            ChatLog.Enqueue(chatMsg);

            LogToFile($"ChatLog Count after enqueuing: {ChatLog.Count}", "DEBUG");
            if (ChatLog.Count > 20)
            {

                chatMessage dequeuedMessage = ChatLog.Peek(); 
                LogToFile($"Dequeuing chat message to maintain queue size: {dequeuedMessage}", "DEBUG");
                ChatLog.Dequeue();

                LogToFile($"ChatLog Count after dequeuing: {ChatLog.Count}", "DEBUG");
            }
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while enqueuing or dequeuing a chat message: {ex.Message}", "ERROR");
        }
    }

    private void QueueGPTMessage(string userContent, string assistantContent)
    {

        LogToFile("Entering QueueGPTMessage with paired messages.", "DEBUG");

        chatMessage userMessage = new chatMessage
        {
            role = "user",
            content = userContent
        };
        chatMessage assistantMessage = new chatMessage
        {
            role = "assistant",
            content = assistantContent
        };
        try
        {

            GPTLog.Enqueue(userMessage);
            GPTLog.Enqueue(assistantMessage);

            LogToFile($"Enqueuing user message: {userMessage}", "INFO");
            LogToFile($"Enqueuing assistant message: {assistantMessage}", "INFO");

            if (GPTLog.Count > 10)
            {
                LogToFile("GPTLog limit exceeded. Dequeuing the oldest pair of messages.", "DEBUG");
                GPTLog.Dequeue(); 
                GPTLog.Dequeue(); 
            }

            LogToFile($"GPTLog Count after enqueueing/dequeueing: {GPTLog.Count}", "DEBUG");
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while enqueuing GPT messages: {ex.Message}", "ERROR");
        }
    }

    public bool Execute()
    {

        LogToFile("Starting initialization of the PNGTuber-GPT application.", "INFO");

        LogToFile("Initialization of PNGTuber-GPT successful. Added all global variables to memory.", "INFO");

        LogToFile("Starting to retrieve the version number from a global variable.", "DEBUG");

        string initializeVersionNumber = CPH.GetGlobalVar<string>("Version", true);

        LogToFile($"Retrieved version number: {initializeVersionNumber}", "DEBUG");

        if (string.IsNullOrWhiteSpace(initializeVersionNumber))
        {

            LogToFile("The 'Version' global variable is not found or is empty.", "ERROR");
            return false;
        }

        LogToFile($"Sending version number to chat: {initializeVersionNumber}", "DEBUG");

        CPH.SendMessage($"{initializeVersionNumber} has been initialized successfully.", true);

        LogToFile("Version number sent to chat successfully.", "INFO");

        return true;
        return true;
    }

    public bool GetNicknamewPronouns()
    {
        LogToFile("Entering GetNicknamewPronouns method.", "DEBUG");
        string userName = args["userName"].ToString();
        LogToFile($"Retrieved 'userName': {userName}", "DEBUG");
        if (string.IsNullOrWhiteSpace(userName))
        {
            LogToFile("'userName' value is either not found or not a valid string.", "ERROR");
            return false;
        }

        try
        {

            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' value is either not found or not a valid string.", "ERROR");
                return false;
            }

            string filePath = Path.Combine(databasePath, "preferred_userNames.json");
            if (!File.Exists(filePath))
            {
                LogToFile("'preferred_userNames.json' does not exist. Creating default file.", "WARN");
                CreateDefaultUserNameFile(filePath);
            }

            string preferredUserName = GetPreferredUsername(userName, filePath);
            if (string.IsNullOrWhiteSpace(preferredUserName))
            {
                LogToFile("Preferred user name could not be retrieved.", "ERROR");
                return false;
            }

            string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
            if (string.IsNullOrWhiteSpace(ignoreNamesString))
            {
                LogToFile("'Ignore Bot Usernames' global variable is not found or not a valid string.", "ERROR");
                return false;
            }

            LogToFile($"Bot usernames to ignore: {ignoreNamesString}", "DEBUG");

            List<string> ignoreNamesList = ignoreNamesString.Split(',').Select(name => name.Trim()).ToList();
            if (ignoreNamesList.Contains(userName, StringComparer.OrdinalIgnoreCase))
            {
                LogToFile($"Message from {userName} ignored as it's in the bot ignore list.", "INFO");
                return false;
            }

            string pronouns = GetOrCreatePronouns(userName, databasePath);
            string formattedUsername = $"{preferredUserName}{(string.IsNullOrEmpty(pronouns) ? "" : $" ({pronouns})")}";
            LogToFile($"Formatted 'nicknamePronouns': {formattedUsername}", "INFO");

            CPH.SetArgument("nicknamePronouns", formattedUsername);
            CPH.SetArgument("Pronouns", pronouns);
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred in GetNicknamewPronouns: {ex.Message}", "ERROR");
            return false;
        }
    }

    private string GetOrCreatePronouns(string username, string databasePath)
    {

        string pronounsCachePath = Path.Combine(databasePath, "pronouns.json");

        Dictionary<string, PronounCacheEntry> pronounsCache = File.Exists(pronounsCachePath) ? JsonConvert.DeserializeObject<Dictionary<string, PronounCacheEntry>>(File.ReadAllText(pronounsCachePath)) : new Dictionary<string, PronounCacheEntry>();

        if (pronounsCache.TryGetValue(username, out PronounCacheEntry cacheEntry) && cacheEntry.Expiration > DateTime.UtcNow)
        {
            LogToFile($"Using cached pronouns for user '{username}': {cacheEntry.FormattedPronouns}", "INFO");
            return cacheEntry.FormattedPronouns;
        }
        else
        {

            string pronouns = FetchPronouns(username, databasePath);
            if (!string.IsNullOrEmpty(pronouns))
            {
                pronounsCache[username] = new PronounCacheEntry
                {
                    FormattedPronouns = pronouns,
                    Expiration = DateTime.UtcNow.AddHours(24)
                };

                try
                {
                    File.WriteAllText(pronounsCachePath, JsonConvert.SerializeObject(pronounsCache, Formatting.Indented));
                    LogToFile($"Fetched and cached new pronouns for user '{username}': {pronouns}", "INFO");
                }
                catch (Exception ex)
                {
                    LogToFile($"Failed to write pronouns cache to file '{pronounsCachePath}': {ex.Message}", "ERROR");
                }
            }
            else
            {
                LogToFile($"Failed to fetch pronouns for user '{username}' and no cached pronouns were available.", "ERROR");
            }

            return pronouns;
        }
    }

    private void CreateDefaultUserNameFile(string filePath)
    {

        LogToFile($"Entering CreateDefaultUserNameFile method with filePath: {filePath}", "DEBUG");
        try
        {

            LogToFile("Creating a default user dictionary for the username file.", "DEBUG");

            var defaultUser = new Dictionary<string, string>
            {
                {
                    "DefaultUser",
                    "Default User"
                }
            };

            string jsonData = JsonConvert.SerializeObject(defaultUser, Formatting.Indented);
            LogToFile("Serialized default user data to JSON.", "DEBUG");

            File.WriteAllText(filePath, jsonData);

            LogToFile($"Created and wrote to the file: {filePath}", "INFO");
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while creating the default username file: {ex.Message}", "ERROR");
        }
    }

    private string GetPreferredUsername(string currentUserName, string filePath)
    {

        string preferredUserName = currentUserName;

        LogToFile($"Entering GetPreferredUsername method with currentUserName: {currentUserName} and filePath: {filePath}", "DEBUG");
        try
        {

            if (File.Exists(filePath))
            {

                string jsonData = File.ReadAllText(filePath);
                LogToFile("Read user preferences JSON data from file.", "DEBUG");

                var userPreferences = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);

                LogToFile($"Attempting to find preferred username for {currentUserName}.", "DEBUG");

                if (userPreferences != null && userPreferences.TryGetValue(currentUserName, out var preferredName))
                {

                    preferredUserName = preferredName;
                    LogToFile($"Found and set preferred username: {preferredUserName}", "DEBUG");
                }
                else
                {

                    LogToFile($"Preferred username for {currentUserName} not found in file. Using current username as preferred.", "INFO");
                }
            }
            else
            {

                LogToFile($"File not found: {filePath}. Using current username as preferred.", "WARN");
            }

            string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
            if (string.IsNullOrWhiteSpace(ignoreNamesString))
            {
                LogToFile("'Ignore Bot Usernames' global variable is not found or not a valid string.", "ERROR");
                return preferredUserName;
            }

            LogToFile($"Bot usernames to ignore: {ignoreNamesString}", "DEBUG");

            List<string> ignoreNamesList = ignoreNamesString.Split(',').Select(name => name.Trim()).ToList();
            if (ignoreNamesList.Contains(currentUserName, StringComparer.OrdinalIgnoreCase))
            {
                LogToFile($"Username {currentUserName} is in the bot ignore list. Using current username as preferred.", "DEBUG");
                return currentUserName;
            }
        }
        catch (Exception ex)
        {

            LogToFile($"Error reading or deserializing user preferred names from file: {ex.Message}", "ERROR");
        }

        LogToFile($"Returning preferred or original username: {preferredUserName}", "INFO");

        return preferredUserName;
    }

    public bool SetPronouns()
    {

        LogToFile("Entering SetPronouns method.", "DEBUG");
        try
        {

            LogToFile("Preparing to send pronouns setting information message to user.", "DEBUG");

            string message = "You can set your pronouns at https://pronouns.alejo.io/. Your pronouns will be available via a Public API. This means that users of 7TV, FFZ, and BTTV extensions can see your pronouns in chat.";
            CPH.SendMessage(message, true);

            LogToFile("!setpronouns was triggered, sent pronouns setting information message to user.", "INFO");

            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred in SetPronouns while sending message: {ex.Message}", "ERROR");

            return false;
        }
    }

    private string FetchPronouns(string username, string databasePath)
    {
        LogToFile($"Entering FetchPronouns method for username: {username}", "DEBUG");

        string pronounsCachePath = Path.Combine(databasePath, "pronouns.json");

        Dictionary<string, PronounCacheEntry> pronounsCache = LoadPronounsCache(pronounsCachePath);

        if (pronounsCache.TryGetValue(username, out PronounCacheEntry cacheEntry) && cacheEntry.Expiration > DateTime.UtcNow)
        {
            LogToFile($"Using cached pronouns for user '{username}'.", "INFO");
            return cacheEntry.FormattedPronouns;
        }

        string url = $"https://pronouns.alejo.io/api/users/{username.ToLower()}";
        LogToFile($"Fetching pronouns from URL: {url}", "DEBUG");
        try
        {
            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    LogToFile($"Received JSON response for pronouns: {jsonResponse}", "DEBUG");
                    var users = JsonConvert.DeserializeObject<List<PronounUser>>(jsonResponse);
                    var user = users?.FirstOrDefault(u => u.login.Equals(username, StringComparison.OrdinalIgnoreCase));
                    if (user != null)
                    {
                        string formattedPronouns = FormatPronouns(user.pronoun_id);
                        LogToFile($"Pronouns found for {username}: {formattedPronouns}", "INFO");

                        pronounsCache[username] = new PronounCacheEntry
                        {
                            FormattedPronouns = formattedPronouns,
                            Expiration = DateTime.UtcNow.AddHours(24)
                        };

                        SavePronounsCache(pronounsCache, pronounsCachePath);
                        return formattedPronouns;
                    }
                    else
                    {
                        LogToFile($"No pronouns found for {username}.", "INFO");
                    }
                }
                else
                {
                    LogToFile($"Failed to fetch pronouns. HTTP response status: {response.StatusCode}", "ERROR");
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while fetching pronouns for {username}: {ex.Message}", "ERROR");
        }

        LogToFile($"Pronouns for {username} were not found or an error occurred.", "DEBUG");
        return null;
    }

    private Dictionary<string, PronounCacheEntry> LoadPronounsCache(string path)
    {
        if (File.Exists(path))
        {
            string jsonContent = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Dictionary<string, PronounCacheEntry>>(jsonContent) ?? new Dictionary<string, PronounCacheEntry>();
        }

        return new Dictionary<string, PronounCacheEntry>();
    }

    private void SavePronounsCache(Dictionary<string, PronounCacheEntry> cache, string path)
    {
        string jsonContent = JsonConvert.SerializeObject(cache, Formatting.Indented);
        File.WriteAllText(path, jsonContent);
        LogToFile("Pronouns cache updated.", "DEBUG");
    }

    private string FormatPronouns(string pronounId)
    {
        LogToFile($"Entering FormatPronouns method with pronounId: {pronounId}", "DEBUG");

        var pronounsList = new List<string>
        {
            "they",
            "she",
            "he",
            "xe",
            "ze",
            "ey",
            "per",
            "ve",
            "it",
            "them",
            "him",
            "her",
            "hir",
            "xis",
            "zer",
            "em",
            "pers",
            "vers",
            "its"
        };

        pronounsList.Sort((a, b) => b.Length.CompareTo(a.Length));

        var formattedPronouns = new List<string>();

        string remainingPronounId = pronounId.ToLower();
        LogToFile("Starting pronoun formatting.", "DEBUG");

        while (!string.IsNullOrEmpty(remainingPronounId))
        {
            bool matchFound = false;
            foreach (var pronoun in pronounsList)
            {
                if (remainingPronounId.StartsWith(pronoun))
                {

                    formattedPronouns.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pronoun));

                    remainingPronounId = remainingPronounId.Substring(pronoun.Length);
                    matchFound = true;
                    LogToFile($"Matched Pronoun: {pronoun}, Remaining ID: {remainingPronounId}", "DEBUG");
                    break; 
                }
            }

            if (!matchFound)
            {
                break;
            }
        }

        string formattedPronounsString = string.Join("/", formattedPronouns);
        LogToFile($"Formatted pronouns: {formattedPronounsString}", "DEBUG");
        return formattedPronounsString;
    }

    public bool GetCurrentNickname()
    {
        try
        {
            LogToFile("Entering GetCurrentNickname method.", "DEBUG");
            if (args.ContainsKey("nicknamePronouns"))
            {
                string nicknamePronouns = args["nicknamePronouns"].ToString();
                LogToFile($"Retrieved 'nicknamePronouns' argument: {nicknamePronouns}", "DEBUG");
                if (string.IsNullOrWhiteSpace(nicknamePronouns))
                {
                    LogToFile("'nicknamePronouns' value is either not found or not a valid string.", "ERROR");
                    return false;
                }

                string userName = args["userName"].ToString();
                LogToFile($"Retrieved 'userName' argument: {userName}", "DEBUG");
                if (string.IsNullOrWhiteSpace(userName))
                {
                    LogToFile("'userName' value is either not found or not a valid string.", "ERROR");
                    return false;
                }

                LogToFile("Processing the nickname and pronouns for message sending.", "DEBUG");

                string[] split = nicknamePronouns.Split(new[] { ' ' }, 2);
                string nickname = split[0];
                if (userName.Equals(nickname, StringComparison.OrdinalIgnoreCase))
                {

                    CPH.SendMessage($"You don't have a custom nickname set. Your username is: {nicknamePronouns}", true);
                    LogToFile("Informed user they don't have a custom nickname set.", "INFO");
                }
                else
                {

                    CPH.SendMessage($"Your current nickname is: {nicknamePronouns}", true);
                    LogToFile("Sent message with the user's current nickname.", "INFO");
                }

                return true;
            }
            else
            {
                LogToFile("The 'nicknamePronouns' key is missing from args.", "ERROR");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while getting the current nickname: {ex.Message}", "ERROR");
            return false;
        }
    }

    public bool SetPreferredUsername()
    {
        LogToFile("Entering SetPreferredUsername method.", "DEBUG");

        string userName = args["userName"]?.ToString();
        string pronouns = args["Pronouns"]?.ToString();
        string preferredUserNameInput = args["rawInput"]?.ToString();
        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);

        LogToFile($"Retrieved parameters: userName={userName}, Pronouns={pronouns}, rawInput={preferredUserNameInput}, Database Path={databasePath}", "DEBUG");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(preferredUserNameInput))
        {
            string missingParameter = string.IsNullOrWhiteSpace(userName) ? "userName" : string.IsNullOrWhiteSpace(databasePath) ? "Database Path" : "rawInput";
            LogToFile($"'${missingParameter}' value is either not found or not a valid string.", "ERROR");
            return false;
        }

        if (string.IsNullOrWhiteSpace(pronouns))
        {
            pronouns = "";
            LogToFile("Pronouns value not found. Proceeding without pronouns.", "DEBUG");
        }

        string filePath = Path.Combine(databasePath, "preferred_userNames.json");
        LogToFile($"File path for preferred usernames: {filePath}", "DEBUG");
        try
        {

            Dictionary<string, string> userPreferences = File.Exists(filePath) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath)) : new Dictionary<string, string>();

            userPreferences[userName] = preferredUserNameInput;
            LogToFile($"Set preferred username for '{userName}' to '{preferredUserNameInput}'.", "DEBUG");

            File.WriteAllText(filePath, JsonConvert.SerializeObject(userPreferences, Formatting.Indented));
            LogToFile("Updated user preferences file successfully.", "INFO");

            string message = $"{userName}, your nickname has been set to {preferredUserNameInput} ({pronouns}).";
            CPH.SendMessage(message, true);
            LogToFile($"Sent confirmation message to user: {message}", "INFO");
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while setting the preferred username: {ex.Message}", "ERROR");
            string errorMessage = $"{userName}, I was unable to set your nickname. Please try again later.";
            CPH.SendMessage(errorMessage, true);
            LogToFile($"Sent error message to user: {errorMessage}", "INFO");
            return false;
        }
    }

    public bool RemoveNick()
    {
        LogToFile("Entering RemoveNick method.", "DEBUG");
        try
        {
            string userName = args["userName"].ToString();
            LogToFile($"Attempting to remove nickname for user: {userName}", "DEBUG");
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            string filePath = Path.Combine(databasePath, "preferred_userNames.json");
            if (!File.Exists(filePath))
            {
                LogToFile("The keyword contexts file does not exist. No action necessary.", "INFO");
                CPH.SendMessage("There is no custom nickname to remove.", true);
                return true;
            }

            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
            if (keywordContexts != null && keywordContexts.ContainsKey(userName))
            {
                keywordContexts.Remove(userName);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
                LogToFile($"Removed nickname for user: {userName}", "INFO");
                CPH.SendMessage($"The custom nickname for {userName} has been removed.", true);
            }
            else
            {
                LogToFile($"No custom nickname found for user: {userName}", "INFO");
                CPH.SendMessage($"There was no custom nickname set for {userName}.", true);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while removing the nickname: {ex.Message}", "ERROR");
            CPH.SendMessage("An error occurred while attempting to remove the custom nickname. Please try again later.", true);
            return false;
        }
    }

    public bool ForgetThis()
    {
        LogToFile("Entering ForgetThis method.", "DEBUG");
        try
        {
            string keywordToRemove = args["rawInput"].ToString();
            LogToFile($"Attempting to remove definition for keyword: {keywordToRemove}", "DEBUG");
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            if (!File.Exists(filePath))
            {
                LogToFile("The keyword contexts file does not exist. No action necessary.", "INFO");
                CPH.SendMessage("I don't have a definition for the specified keyword.", true);
                return true;
            }

            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
            if (keywordContexts != null && keywordContexts.ContainsKey(keywordToRemove))
            {
                keywordContexts.Remove(keywordToRemove);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
                LogToFile($"Removed definition for keyword: {keywordToRemove}", "INFO");
                CPH.SendMessage($"The definition for {keywordToRemove} has been removed.", true);
            }
            else
            {
                LogToFile($"No definition found for keyword: {keywordToRemove}", "INFO");
                CPH.SendMessage($"There was no definition set for {keywordToRemove}.", true);
            }

            return true;
        }
        catch (Exception ex)
        {
            string ErrorKeywordToRemove = args["rawInput"].ToString();
            LogToFile($"An error occurred while removing the definition for {ErrorKeywordToRemove}: {ex.Message}", "ERROR");
            CPH.SendMessage("An error occurred while attempting to remove the definition for {ErrorKeywordToRemove}. Please try again later.", true);
            return false;
        }
    }

    public bool ForgetThisAboutMe()
    {
        LogToFile("Entering ForgetThisAboutMe method.", "DEBUG");
        try
        {
            string userName = args["userName"].ToString();
            LogToFile($"Attempting to remove memory for username: {userName}", "DEBUG");
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            if (!File.Exists(filePath))
            {
                LogToFile("The keyword contexts file does not exist. No action necessary.", "INFO");
                CPH.SendMessage("I don't have a memory set for you.", true);
                return true;
            }

            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
            if (keywordContexts != null && keywordContexts.ContainsKey(userName))
            {
                keywordContexts.Remove(userName);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
                LogToFile($"Removed memory for username: {userName}", "INFO");
                CPH.SendMessage($"The memory for {userName} has been removed.", true);
            }
            else
            {
                LogToFile($"No memory found for username: {userName}", "INFO");
                CPH.SendMessage($"There was no memory set for {userName}.", true);
            }

            return true;
        }
        catch (Exception ex)
        {
            string errorUserName = args["userName"].ToString();
            LogToFile($"An error occurred while removing the memory for {errorUserName}: {ex.Message}", "ERROR");
            CPH.SendMessage("An error occurred while attempting to remove the memory for {errorUserName}. Please try again later.", true);
            return false;
        }
    }

    public bool GetMemory()
    {
        LogToFile("Entering GetMemory method.", "DEBUG");
        try
        {
            string userName = args["userName"].ToString();
            string nicknamePronouns = args["nicknamePronouns"].ToString();
            LogToFile($"Attempting to retrieve stored information for user: {userName}", "DEBUG");
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");

            if (!File.Exists(filePath))
            {
                LogToFile("The keyword contexts file does not exist. No information to retrieve.", "WARN");
                CPH.SendMessage("No information has been stored for you, {nicknamePronouns}", true);
                return true;
            }

            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));

            if (keywordContexts != null && keywordContexts.TryGetValue(userName, out string storedInfo))
            {
                LogToFile($"Retrieved stored information for user: {userName}", "INFO");
                CPH.SendMessage($"Here's what I remember about you, {nicknamePronouns}: {storedInfo}", true);
            }
            else
            {
                LogToFile($"No information found for user: {userName}", "INFO");
                CPH.SendMessage($"I don't have any information stored for you, {nicknamePronouns}.", true);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while retrieving stored information: {ex.Message}", "ERROR");
            CPH.SendMessage("An error occurred while attempting to retrieve stored information. Please try again later.", true);
            return false;
        }
    }

    public bool SaveMessage()
    {
        LogToFile("Entering SaveMessage method.", "DEBUG");

        string msg = args["rawInput"]?.ToString();
        string userName = args["userName"]?.ToString();
        string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);

        if (string.IsNullOrWhiteSpace(msg) || string.IsNullOrWhiteSpace(userName))
        {
            LogToFile($"'rawInput' or 'userName' value is not found or not a valid string. rawInput: {msg}, userName: {userName}", "ERROR");
            return false;
        }

        LogToFile($"Retrieved message: {msg}, from user: {userName}", "INFO");

        if (msg.StartsWith("!"))
        {
            LogToFile("Message is a command and will be ignored.", "INFO");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ignoreNamesString))
        {
            LogToFile("'Ignore Bot Usernames' global variable is not found or not a valid string.", "ERROR");
            return false;
        }

        LogToFile($"Bot usernames to ignore: {ignoreNamesString}", "DEBUG");

        List<string> ignoreNamesList = ignoreNamesString.Split(',').Select(name => name.Trim()).ToList();
        if (ignoreNamesList.Contains(userName, StringComparer.OrdinalIgnoreCase))
        {
            LogToFile($"Message from {userName} ignored as it's in the bot ignore list.", "INFO");
            return false;
        }

        string nicknamePronouns = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : userName;

        LogToFile($"Retrieved formatted nickname with pronouns: {nicknamePronouns}", "DEBUG");

        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            LogToFile("ChatLog queue has been initialized.", "DEBUG");
        }

        string messageContent = $"{nicknamePronouns} says: {msg}";
        LogToFile($"Preparing to queue message: {messageContent}", "DEBUG");

        chatMessage chatMsg = new chatMessage
        {
            role = "user",
            content = messageContent
        };
        QueueMessage(chatMsg);

        LogToFile($"Message queued successfully: {messageContent}", "INFO");

        System.Threading.Thread.Sleep(250);
        return true;
    }

    public bool ClearChatHistory()
    {
        LogToFile("Attempting to clear chat history.", "DEBUG");

        if (ChatLog == null)
        {
            LogToFile("ChatLog is not initialized and cannot be cleared.", "ERROR");
            CPH.SendMessage("Chat history is already empty.", true);
            return false;
        }

        try
        {

            ChatLog.Clear();
            LogToFile("Chat history has been successfully cleared.", "INFO");

            CPH.SendMessage("Chat history has been cleared.", true);
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while clearing the chat history: {ex.Message}", "ERROR");
            CPH.SendMessage("I was unable to clear the chat history. Please check the log file for more details.", true);
            return false;
        }
    }

    public bool PerformModeration()
    {
        LogToFile("Entering PerformModeration method.", "DEBUG");

        string input = args["rawInput"]?.ToString();
        if (string.IsNullOrWhiteSpace(input))
        {
            LogToFile("'rawInput' value is either not found or not a valid string.", "ERROR");
            return false;
        }

        LogToFile($"Message for moderation: {input}", "INFO");

        var preferences = LoadModerationPreferences();
        var excludedCategories = preferences
            .Where(p => p.Value)
            .Select(p => p.Key)
            .ToList();
        LogToFile($"Excluded categories for moderation: {string.Join(", ", excludedCategories)}", "DEBUG");
        try
        {

            List<string> flaggedCategories = CallModerationEndpoint(input, excludedCategories.ToArray());
            if (flaggedCategories == null)
            {
                LogToFile("Moderation endpoint failed to respond or responded with an error.", "ERROR");
                return false;
            }

            bool moderationResult = HandleModerationResponse(flaggedCategories, input);
            LogToFile($"Moderation result: {(moderationResult ? "Passed" : "Failed")}", "INFO");
            return moderationResult;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred in PerformModeration: {ex.Message}", "ERROR");
            return false;
        }
    }

    private Dictionary<string, bool> LoadModerationPreferences()
    {
        LogToFile("Loading moderation preferences.", "DEBUG");

        var keyMap = new Dictionary<string, string>
        {
            { "hate_allowed", "hate" },
            { "hate_threatening_allowed", "hate/threatening" },
            { "harassment_allowed", "harassment" },
            { "harassment_threatening_allowed", "harassment/threatening" },
            { "sexual_allowed", "sexual" },
            { "violence_allowed", "violence" },
            { "violence_graphic_allowed", "violence/graphic" },
            { "self_harm_allowed", "self-harm" },
            { "self_harm_intent_allowed", "self-harm/intent" },
            { "self_harm_instructions_allowed", "self-harm/instructions" },
            { "illicit_allowed", "illicit" },
            { "illicit_violent_allowed", "illicit/violent" }
        };

        var preferences = new Dictionary<string, bool>();
        foreach (var kvp in keyMap)
        {
            bool value = CPH.GetGlobalVar<bool>(kvp.Key, true);
            preferences.Add(kvp.Value, value);
            LogToFile($"Loaded moderation preference: {kvp.Key} (API: {kvp.Value}) = {value}", "DEBUG");
        }

        return preferences;
    }

    private bool HandleModerationResponse(List<string> flaggedCategories, string input)
    {
        if (flaggedCategories.Any())
        {
            string flaggedCategoriesString = string.Join(", ", flaggedCategories);
            string outputMessage = $"This message was flagged in the following categories: {flaggedCategoriesString}. Repeated attempts at abuse may result in a ban.";
            LogToFile(outputMessage, "INFO");

            string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                LogToFile("'Voice Alias' global variable is not found or not a valid string.", "ERROR");
                return false;
            }

            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            LogToFile($"TTS speak result: {speakResult}", "DEBUG");

            CPH.SendMessage(outputMessage, true);
            return false;
        }
        else
        {

            CPH.SetArgument("moderatedMessage", input);
            LogToFile("Message passed moderation.", "DEBUG");
            return true;
        }
    }

    private List<string> CallModerationEndpoint(string prompt, string[] excludedCategories)
    {
        LogToFile("Entering CallModerationEndpoint method.", "DEBUG");

        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogToFile("The OpenAI API Key is not set or is invalid.", "ERROR");
            return null;
        }

        try
        {

            string moderationEndpoint = "https://api.openai.com/v1/moderations";

            var moderationRequestBody = new
            {
                model = "omni-moderation-latest",
                input = prompt
            };
            string moderationJsonPayload = JsonConvert.SerializeObject(moderationRequestBody);
            byte[] moderationContentBytes = Encoding.UTF8.GetBytes(moderationJsonPayload);

            WebRequest moderationWebRequest = WebRequest.Create(moderationEndpoint);
            moderationWebRequest.Method = "POST";
            moderationWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            moderationWebRequest.ContentType = "application/json";
            moderationWebRequest.ContentLength = moderationContentBytes.Length;

            LogToFile("Sending moderation request to OpenAI API.", "DEBUG");

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
                        LogToFile($"Received moderation response: {moderationResponseContent}", "DEBUG");

                        var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);

                        if (moderationJsonResponse?.Results == null || !moderationJsonResponse.Results.Any())
                        {
                            LogToFile("No moderation results were returned from the API.", "ERROR");
                            return null;
                        }

                        List<string> flaggedCategories = moderationJsonResponse.Results[0].Categories.Where(category => category.Value && !excludedCategories.Contains(category.Key)).Select(category => category.Key).ToList();

                        if (flaggedCategories != null && flaggedCategories.Any())
                        {
                            LogToFile($"Flagged categories: {string.Join(", ", flaggedCategories)}", "INFO");
                        }

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
                LogToFile($"A WebException was caught during the moderation request: {responseContent}", "ERROR");
            }

            return null;
        }
        catch (Exception ex)
        {

            LogToFile($"An exception occurred while calling the moderation endpoint: {ex.Message}", "ERROR");
            return null;
        }
    }

    public bool Speak()
    {
        LogToFile("Entering Speak method.", "DEBUG");

        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            LogToFile("'Voice Alias' global variable is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I was unable to speak that message. Please check the configuration.", true);
            return false;
        }

        string userToSpeak = args.ContainsKey("nickname") && !string.IsNullOrWhiteSpace(args["nickname"].ToString()) ? args["nickname"].ToString() : args.ContainsKey("userName") && !string.IsNullOrWhiteSpace(args["userName"].ToString()) ? args["userName"].ToString() : "";
        if (string.IsNullOrWhiteSpace(userToSpeak))
        {
            LogToFile("Unable to retrieve a valid 'nickname' or 'userName' for speaking.", "ERROR");
            CPH.SendMessage("I was unable to speak that message. Please check the input.", true);
            return false;
        }

        string messageToSpeak = args.ContainsKey("moderatedMessage") && !string.IsNullOrWhiteSpace(args["moderatedMessage"].ToString()) ? args["moderatedMessage"].ToString() : args.ContainsKey("rawInput") && !string.IsNullOrWhiteSpace(args["rawInput"].ToString()) ? args["rawInput"].ToString() : "";
        if (string.IsNullOrWhiteSpace(messageToSpeak))
        {
            LogToFile("Unable to retrieve a valid 'moderatedMessage' or 'rawInput' for speaking.", "ERROR");
            CPH.SendMessage("I was unable to speak that message. Please check the input.", true);
            return false;
        }

        string outputMessage = $"{userToSpeak} said: {messageToSpeak}";
        LogToFile($"Speaking message: {outputMessage}", "INFO");
        try
        {

            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            if (speakResult != 0)
            {

                LogToFile($"TTS returned result code: {speakResult}", "INFO");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An exception occurred while trying to speak: {ex.Message}", "ERROR");
            return false;
        }
    }

    public bool RememberThis()
    {
        LogToFile("Entering RememberThis method.", "DEBUG");

        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string userName = args.ContainsKey("userName") ? args["userName"].ToString() : "";
        string nicknamePronouns = args.ContainsKey("nicknamePronouns") ? args["nicknamePronouns"].ToString() : "";
        string userToConfirm = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : nicknamePronouns;
        string fullMessage = args.ContainsKey("moderatedMessage") && !string.IsNullOrWhiteSpace(args["moderatedMessage"].ToString()) ? args["moderatedMessage"].ToString() : args.ContainsKey("rawInput") && !string.IsNullOrWhiteSpace(args["rawInput"].ToString()) ? args["rawInput"].ToString() : "";

        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(fullMessage))
        {
            string missingParameter = string.IsNullOrWhiteSpace(databasePath) ? "Database Path" : string.IsNullOrWhiteSpace(voiceAlias) ? "Voice Alias" : string.IsNullOrWhiteSpace(userName) ? "userName" : "message";
            LogToFile($"'{missingParameter}' value is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }

        try
        {

            var parts = fullMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                LogToFile("The message does not contain enough parts to extract a keyword and a definition.", "ERROR");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            string keyword = parts[0];
            string definition = string.Join(" ", parts.Skip(1));
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{}");
            }

            string jsonContent = File.ReadAllText(filePath);
            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            keywordContexts[keyword] = definition;

            File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
            LogToFile($"Keyword '{keyword}' and definition '{definition}' saved to {filePath}", "INFO");

            string outputMessage = $"OK, {userToConfirm}, I will remember '{definition}' for '{keyword}'.";
            CPH.SendMessage(outputMessage, true);
            LogToFile($"Confirmation message sent to user: {outputMessage}", "INFO");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while trying to remember: {ex.Message}", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
    }

    public bool RememberThisAboutMe()
    {
        LogToFile("Entering RememberThisAboutMe method.", "DEBUG");

        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string userName = args.ContainsKey("userName") ? args["userName"].ToString() : "";
        string nicknamePronouns = args.ContainsKey("nicknamePronouns") ? args["nicknamePronouns"].ToString() : "";
        string userToConfirm = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : nicknamePronouns;
        string messageToRemember = args.ContainsKey("moderatedMessage") && !string.IsNullOrWhiteSpace(args["moderatedMessage"].ToString()) ? args["moderatedMessage"].ToString() : args.ContainsKey("rawInput") && !string.IsNullOrWhiteSpace(args["rawInput"].ToString()) ? args["rawInput"].ToString() : "";

        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(messageToRemember))
        {
            string missingParameter = string.IsNullOrWhiteSpace(databasePath) ? "Database Path" : string.IsNullOrWhiteSpace(voiceAlias) ? "Voice Alias" : string.IsNullOrWhiteSpace(userName) ? "userName" : "messageToRemember";
            LogToFile($"'{missingParameter}' value is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }

        try
        {

            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{}");
            }

            string jsonContent = File.ReadAllText(filePath);
            var userContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            userContexts[userName] = messageToRemember;

            File.WriteAllText(filePath, JsonConvert.SerializeObject(userContexts, Formatting.Indented));
            LogToFile($"Information about user '{userName}' saved: {messageToRemember}", "INFO");

            string outputMessage = $"OK, {userToConfirm}, I will remember {messageToRemember} about you.";
            CPH.SendMessage(outputMessage, true);
            LogToFile($"Confirmation message sent to user: {outputMessage}", "INFO");
            return true;
        }
        catch (JsonException jsonEx)
        {
            LogToFile($"JSON error in RememberThisAboutMe: {jsonEx.Message}", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
        catch (IOException ioEx)
        {
            LogToFile($"IO error in RememberThisAboutMe: {ioEx.Message}", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred in RememberThisAboutMe: {ex.Message}", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }
    }

    public bool ClearPromptHistory()
    {
        LogToFile("Entering ClearPromptHistory method.", "DEBUG");
        LogToFile("Attempting to clear prompt history.", "DEBUG");

        if (GPTLog == null)
        {
            LogToFile("GPTLog is not initialized and cannot be cleared.", "ERROR");
            CPH.SendMessage("Prompt history is already empty.", true);
            return false;
        }

        try
        {

            GPTLog.Clear();
            LogToFile("Prompt history has been successfully cleared.", "INFO");

            CPH.SendMessage("Prompt history has been cleared.", true);
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while clearing the prompt history: {ex.Message}", "ERROR");
            CPH.SendMessage("I was unable to clear the prompt history. Please check the log file for more details.", true);
            return false;
        }
    }

    public bool AskGPT()
    {
        LogToFile("Entering AskGPT method.", "DEBUG");

        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            LogToFile("ChatLog queue has been initialized for the first time.", "INFO");
        }
        else
        {

            string chatLogAsString = string.Join(Environment.NewLine, ChatLog.Select(m => m.content ?? "null"));
            LogToFile($"ChatLog Content before asking GPT: {Environment.NewLine}{chatLogAsString}", "INFO");
        }

        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            LogToFile("'Voice Alias' global variable is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        LogToFile("Retrieved and validated 'Voice Alias' global variable.", "DEBUG");

        string userName;
        if (!args.TryGetValue("userName", out object userNameObj) || string.IsNullOrWhiteSpace(userNameObj?.ToString()))
        {
            LogToFile("'userName' argument is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        userName = userNameObj.ToString();
        LogToFile("Retrieved and validated 'userName' argument.", "DEBUG");

        string userToSpeak = args.TryGetValue("nicknamePronouns", out object nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : userName;
        if (string.IsNullOrWhiteSpace(userToSpeak))
        {
            LogToFile("Both 'nicknamePronouns' and 'userName' are not found or are empty strings.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        string databasePath = CPH.GetGlobalVar<string>("Database Path");
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            LogToFile("'Database Path' global variable is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        LogToFile("Retrieved and validated 'Database Path' global variable.", "DEBUG");

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
            LogToFile("Both 'moderatedMessage' and 'rawInput' are not found or are empty strings.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        string ContextFilePath = Path.Combine(databasePath, "context.txt");
        string keywordContextFilePath = Path.Combine(databasePath, "keyword_contexts.json");
        LogToFile("Constructed file paths for context and keyword context storage.", "DEBUG");

        Dictionary<string, string> keywordContexts;
        if (File.Exists(keywordContextFilePath))
        {
            string jsonContent = File.ReadAllText(keywordContextFilePath);
            keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            LogToFile("Loaded existing keyword contexts from file.", "DEBUG");
        }
        else
        {
            keywordContexts = new Dictionary<string, string>();
            LogToFile("Initialized new dictionary for keyword contexts.", "DEBUG");
        }

        string context = File.Exists(ContextFilePath) ? File.ReadAllText(ContextFilePath) : "";
        string broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
        string currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
        string currentGame = CPH.GetGlobalVar<string>("currentGame", false);
        string contextBody = $"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}";
        LogToFile("Assembled context body for GPT prompt.", "DEBUG");

        string prompt = $"{userToSpeak} asks: {fullMessage}";
        LogToFile($"Constructed prompt for GPT: {prompt}", "DEBUG");

        bool keywordMatch = keywordContexts.Keys.Any(keyword => prompt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        if (keywordMatch)
        {

            string keyword = keywordContexts.Keys.First(keyword => prompt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            string keywordPhrase = $"Something you know about {keyword} is:";
            string keywordValue = keywordContexts[keyword];
            contextBody += $"\n{keywordPhrase} {keywordValue}\n";
            LogToFile("Added keyword-specific context to context body.", "DEBUG");
        }

        if (keywordContexts.ContainsKey(userName))
        {
            string usernamePhrase = $"Something you know about {userToSpeak} is:";
            string usernameValue = keywordContexts[userName];
            contextBody += $"\n{usernamePhrase} {usernameValue}\n";
            LogToFile("Added user-specific context to context body.", "DEBUG");
        }

        try
        {

            string GPTResponse = GenerateChatCompletion(prompt, contextBody); 
            if (string.IsNullOrWhiteSpace(GPTResponse))
            {
                LogToFile("GPT model did not return a response.", "ERROR");
                CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
                return false;
            }

            LogToFile($"GPT model response: {GPTResponse}", "DEBUG");
            CPH.SetGlobalVar("Response", GPTResponse, true);
            LogToFile("Stored GPT response in global variable 'Response'.", "INFO");

            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (!postToChat)
            {
                LogToFile("Posting GPT responses to chat is disabled by settings.", "INFO");
                CPH.TtsSpeak(voiceAlias, GPTResponse, false);
                LogToFile("Spoke GPT's response (chat posting skipped).", "INFO");
                return true;
            }

            CPH.TtsSpeak(voiceAlias, GPTResponse, false);
            LogToFile("Spoke GPT's response.", "INFO");

            if (GPTResponse.Length > 500)
            {
                LogToFile("The response is too long for Twitch; it will be sent in chunks to the chat.", "INFO");
                int startIndex = 0;
                while (startIndex < GPTResponse.Length)
                {

                    int chunkSize = Math.Min(500, GPTResponse.Length - startIndex);
                    int endIndex = startIndex + chunkSize;

                    if (endIndex < GPTResponse.Length)
                    {
                        int lastSpaceIndex = GPTResponse.LastIndexOf(' ', endIndex, chunkSize);
                        int lastPunctuationIndex = GPTResponse.LastIndexOf('.', endIndex, chunkSize);
                        lastPunctuationIndex = Math.Max(lastPunctuationIndex, GPTResponse.LastIndexOf('!', endIndex, chunkSize));
                        lastPunctuationIndex = Math.Max(lastPunctuationIndex, GPTResponse.LastIndexOf('?', endIndex, chunkSize));
                        int lastBreakIndex = Math.Max(lastSpaceIndex, lastPunctuationIndex);
                        if (lastBreakIndex > startIndex)
                        {
                            endIndex = lastBreakIndex;
                        }
                    }

                    string messageChunk = GPTResponse.Substring(startIndex, endIndex - startIndex).Trim();
                    CPH.SendMessage(messageChunk, true);

                    startIndex = endIndex;

                    System.Threading.Thread.Sleep(1000);
                }

                return true;
            }
            else
            {
                CPH.SendMessage(GPTResponse, true);
                LogToFile("Sent GPT response to chat.", "INFO");
            }

            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred while processing the AskGPT request: {ex.Message}", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please try again later.", true);
            return false;
        }
    }

    private string RemoveEmojis(string text)
    {
        LogToFile("Entering RemoveEmojis method.", "DEBUG");

        LogToFile($"Original text before removing emojis: {text}", "DEBUG");

        string emojiPattern = @"[\uD83C-\uDBFF\uDC00-\uDFFF]";  

        LogToFile($"Using regex pattern to remove emojis: {emojiPattern}", "DEBUG");

        string sanitizedText = Regex.Replace(text, emojiPattern, "");

        LogToFile($"Text after removing emojis: {sanitizedText}", "DEBUG");

        sanitizedText = Regex.Replace(sanitizedText, @"\s+", " ").Trim();

        LogToFile($"Sanitized text without emojis: {sanitizedText}", "INFO");
        return sanitizedText;
    }

    public string GenerateChatCompletion(string prompt, string contextBody)
    {
        LogToFile("Entering GenerateChatCompletion method.", "DEBUG");

        string generatedText = string.Empty;

        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);

        LogToFile($"Voice Alias: {voiceAlias}, AI Model: {AIModel}", "DEBUG");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(AIModel))
        {
            LogToFile("One or more configuration values are missing or invalid. Please check the OpenAI API Key, Voice Alias, and AI Model settings.", "ERROR");
            return "Configuration error. Please check the log for details.";
        }

        LogToFile("All configuration values are valid and present.", "DEBUG");

        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";

        LogToFile("All configuration values are valid and present.", "DEBUG");

        var messages = new List<chatMessage>
        {
            new chatMessage
            {
                role = "system",
                content = contextBody
            },

            new chatMessage
            {
                role = "user",
                content = "I am going to send you the chat log from Twitch. You should reference these messages for all future prompts if it is relevant to the prompt being asked. Each message will be prefixed with the users name that you can refer to them as, if referring to their message in the response. After each message you receive, you will return simply \"OK\" to indicate you have received this message, and no other text. When I am finished I will say FINISHED, and you will again respond with simply \"OK\" and nothing else, and then resume normal operation on all future prompts."
            },

            new chatMessage
            {
                role = "assistant",
                content = "OK"
            }
        };

        if (ChatLog != null)
        {
            foreach (var chatMessage in ChatLog)
            {
                messages.Add(chatMessage);

                messages.Add(new chatMessage { role = "assistant", content = "OK" });
            }
        }

        messages.Add(new chatMessage { role = "user", content = "FINISHED" });
        messages.Add(new chatMessage { role = "assistant", content = "OK" });

        if (GPTLog != null)
        {
            foreach (var gptMessage in GPTLog)
            {
                messages.Add(gptMessage);
            }
        }

        messages.Add(new chatMessage { role = "user", content = $"{prompt} You must respond in less than 500 characters." });

        string completionsRequestJSON = JsonConvert.SerializeObject(new { model = AIModel, messages = messages }, Formatting.Indented);

        LogToFile($"Request JSON: {completionsRequestJSON}", "DEBUG");

        WebRequest completionsWebRequest = WebRequest.Create(completionsEndpoint);
        completionsWebRequest.Method = "POST";
        completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        completionsWebRequest.ContentType = "application/json";

        try
        {
            using (Stream requestStream = completionsWebRequest.GetRequestStream())
            {
                byte[] completionsContentBytes = Encoding.UTF8.GetBytes(completionsRequestJSON);
                requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
            }

            using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
            {
                using (StreamReader responseReader = new StreamReader(completionsWebResponse.GetResponseStream()))
                {
                    string completionsResponseContent = responseReader.ReadToEnd();
                    LogToFile($"Response JSON: {completionsResponseContent}", "DEBUG");
                    var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                    generatedText = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                }

                // Regex to find and remove markdown-style citations like ([source](url))
                string citationPattern = @"\s*\(\[.*?\]\(https?:\/\/[^\)]+\)\)";
                generatedText = Regex.Replace(generatedText, citationPattern, "").Trim();
                LogToFile("Removed markdown citations from the response.", "DEBUG");

                // Replace common non-ASCII punctuation with their ASCII equivalents before stripping emojis.
                generatedText = generatedText.Replace('’', '\''); // Curly apostrophe
                generatedText = generatedText.Replace('‘', '\''); // Curly single quote
                generatedText = generatedText.Replace('“', '"');  // Curly double quote
                generatedText = generatedText.Replace('”', '"');  // Curly double quote
                LogToFile("Replaced non-ASCII punctuation with ASCII equivalents.", "DEBUG");

                bool stripEmojis = CPH.GetGlobalVar<bool>("Strip Emojis From Response", true);
                if (stripEmojis)
                {
                    generatedText = RemoveEmojis(generatedText);
                    LogToFile("Emojis have been removed from the response.", "INFO");
                }
            }
        }
        catch (WebException webEx)
        {
            LogToFile($"A WebException was caught: {webEx.Message}", "ERROR");
            if (webEx.Response != null)
            {
                using (var reader = new StreamReader(webEx.Response.GetResponseStream()))
                {
                    LogToFile($"WebException Response: {reader.ReadToEnd()}", "ERROR");
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"An exception occurred: {ex.Message}", "ERROR");
        }

        if (string.IsNullOrEmpty(generatedText))
        {
            generatedText = "ChatGPT did not return a response.";
            LogToFile("The GPT model did not return any text.", "ERROR");
        }
        else
        {

            generatedText = generatedText.Replace("\r\n", " ").Replace("\n", " ");
            LogToFile($"Prompt: {prompt}", "INFO");
            LogToFile($"Response: {generatedText}", "INFO");
        }

        QueueGPTMessage(prompt, generatedText);

        PostToDiscord(prompt, generatedText);

        return generatedText;
    }

    private void PostToDiscord(string question, string answer)
    {

        bool logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);

        if (!logDiscord)
        {
            LogToFile("Posting to Discord is disabled. The message will not be sent.", "INFO");
            return;
        }

        string discordWebhookUrl = CPH.GetGlobalVar<string>("Discord Webhook URL", true);
        string discordUsername = CPH.GetGlobalVar<string>("Discord Bot Username", true);
        string discordAvatarUrl = CPH.GetGlobalVar<string>("Discord Avatar Url", true);

        LogToFile("Retrieved Discord webhook settings.", "DEBUG");

        string discordOutput = $"Question: {question}\nAnswer: {answer}";

        LogToFile($"Attempting to post to Discord: {discordOutput}", "INFO");

        try
        {

            CPH.DiscordPostTextToWebhook(discordWebhookUrl, discordOutput, discordUsername, discordAvatarUrl, false);
            LogToFile("The message was successfully posted to Discord.", "INFO");
        }
        catch (Exception ex)
        {

            LogToFile($"Failed to post the message to Discord: {ex.Message}", "ERROR");
        }
    }

    public bool GetStreamInfo()
    {
        LogToFile("Attempting to retrieve stream information.", "DEBUG");

        Task<AllDatas> getAllDatasTask = FunctionGetAllDatas();
        getAllDatasTask.Wait(); 
        AllDatas datas = getAllDatasTask.Result; 
        if (datas != null)
        {

            CPH.SetGlobalVar("broadcaster", datas.UserName, false);
            CPH.SetGlobalVar("currentGame", datas.gameName, false);
            CPH.SetGlobalVar("currentTitle", datas.titleName, false);

            LogToFile($"Retrieved stream information: Broadcaster - {datas.UserName}, Game - {datas.gameName}, Title - {datas.titleName}", "INFO");
            return true; 
        }
        else
        {

            LogToFile("Failed to retrieve stream information.", "ERROR");
            return false;
        }
    }

    public async Task<AllDatas> FunctionGetAllDatas()
    {
        LogToFile("Starting retrieval of stream data using WebRequest.", "DEBUG");

        string broadcasterId = args["broadcastUserId"].ToString();
        LogToFile($"Broadcast user ID: {broadcasterId}", "DEBUG");

        string twitchApiEndpoint = "https://api.twitch.tv/helix/channels?broadcaster_id=" + broadcasterId;
        LogToFile($"Twitch API endpoint: {twitchApiEndpoint}", "DEBUG");

        string clientIdValue = CPH.TwitchClientId;
        string tokenValue = CPH.TwitchOAuthToken;
        WebRequest request = WebRequest.Create(twitchApiEndpoint);
        request.Method = "GET";
        request.Headers.Add("Client-ID", clientIdValue);
        request.Headers.Add("Authorization", "Bearer " + tokenValue);
        request.ContentType = "application/json";
        try
        {

            using (WebResponse response = await request.GetResponseAsync())
            {

                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseBody = await reader.ReadToEndAsync();
                    LogToFile($"Response from Twitch API: {responseBody}", "DEBUG");

                    Root root = JsonConvert.DeserializeObject<Root>(responseBody);
                    return new AllDatas
                    {
                        UserName = root.data[0].broadcaster_name,
                        gameName = root.data[0].game_name,
                        titleName = root.data[0].title,
                    };
                }
            }
        }
        catch (WebException webEx)
        {
            LogToFile($"WebException occurred: {webEx.Message}", "ERROR");
            if (webEx.Response != null)
            {
                using (StreamReader reader = new StreamReader(webEx.Response.GetResponseStream()))
                {
                    LogToFile($"WebException response content: {reader.ReadToEnd()}", "ERROR");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LogToFile($"Exception occurred: {ex.Message}", "ERROR");
            return null;
        }
    }

    private void LogToFile(string logItem, string logLevel)
    {

        string globalLogLevel = CPH.GetGlobalVar<string>("Logging Level", true);

        if (globalLogLevel == null)
        {
            string notFoundMessage = "The 'Logging Level' global variable is not found.";
            CPH.LogError(notFoundMessage);
            throw new InvalidOperationException(notFoundMessage);
        }

        string[] validLogLevels =
        {
            "INFO",
            "WARN",
            "ERROR",
            "DEBUG"
        };
        if (string.IsNullOrWhiteSpace(globalLogLevel) || !validLogLevels.Contains(globalLogLevel))
        {
            string errorMessage = $"Invalid global logging level: '{globalLogLevel}'. Expected values are INFO, WARN, ERROR, or DEBUG.";
            CPH.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        Dictionary<string, int> logLevelPriority = new Dictionary<string, int>
        {
            {
                "DEBUG",
                1
            },
            {
                "ERROR",
                2
            },
            {
                "WARN",
                3
            },
            {
                "INFO",
                4
            }
        };

        if (logLevelPriority[logLevel] >= logLevelPriority[globalLogLevel])
        {

            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            string logDirectoryPath = Path.Combine(databasePath, "logs");
            if (!Directory.Exists(logDirectoryPath))
            {
                Directory.CreateDirectory(logDirectoryPath);
            }

            string logFileName = DateTime.Now.ToString("PNGTuber-GPT_yyyyMMdd") + ".log";
            string logFilePath = Path.Combine(logDirectoryPath, logFileName);

            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {logLevel}] {logItem}{Environment.NewLine}";

            File.AppendAllText(logFilePath, logEntry);
        }
    }

    public bool ClearLogFile()
    {

        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        if (string.IsNullOrEmpty(databasePath))
        {
            string errorMessage = "The 'Database Path' global variable is not found or is empty.";
            LogToFile(errorMessage, "ERROR");
            return false;
        }

        string logDirectoryPath = Path.Combine(databasePath, "logs");
        if (!Directory.Exists(logDirectoryPath))
        {

            LogToFile("The 'logs' subdirectory does not exist. No log file to clear.", "INFO");
            return false;
        }

        string logFileName = DateTime.Now.ToString("PNGTuber-GPT_yyyyMMdd") + ".log";
        string logFilePath = Path.Combine(logDirectoryPath, logFileName);

        if (File.Exists(logFilePath))
        {
            try
            {

                File.WriteAllText(logFilePath, $"Cleared the log file: {logFileName}");
                File.WriteAllText(logFilePath, string.Empty);
                CPH.SendMessage($"Cleared the log file.", true);
                return true;
            }
            catch (Exception ex)
            {

                LogToFile($"An error occurred while clearing the log file: {ex.Message}", "ERROR");
                CPH.SendMessage("An error occurred while clearing the log file.", true);
                return false;
            }
        }
        else
        {

            LogToFile("No log file exists for the current day to clear.", "INFO");
            CPH.SendMessage("No log file exists for the current day to clear.", true);
            return false;
        }
    }

    public bool Version()
    {

        LogToFile("Starting to retrieve the version number from a global variable.", "DEBUG");

        string versionNumber = CPH.GetGlobalVar<string>("Version", true);

        LogToFile($"Retrieved version number: {versionNumber}", "DEBUG");

        if (string.IsNullOrWhiteSpace(versionNumber))
        {

            LogToFile("The 'Version' global variable is not found or is empty.", "ERROR");
            return false;
        }

        LogToFile($"Sending version number to chat: {versionNumber}", "DEBUG");

        CPH.SendMessage(versionNumber, true);

        LogToFile("Version number sent to chat successfully.", "INFO");

        return true;
    }

    public bool SayPlay()
    {

        LogToFile("Entering the SayPlay method.", "DEBUG");

        CPH.SendMessage("!play", true);

        LogToFile("Sent !play command to chat successfully.", "INFO");

        return true;
    }

    public bool GetNickname()
    {
        try
        {

            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' value is either not found or not a valid string.", "ERROR");
                return false;
            }

            string filePath = Path.Combine(databasePath, "preferred_userNames.json");
            if (!File.Exists(filePath))
            {
                LogToFile("'preferred_userNames.json' does not exist. Creating default file.", "WARN");
                CreateDefaultUserNameFile(filePath);
            }

            string userName = args.ContainsKey("userName") ? args["userName"].ToString() : "";

            string preferredUserName = GetPreferredUsername(userName, filePath);
            if (string.IsNullOrWhiteSpace(preferredUserName))
            {
                LogToFile("Preferred user name could not be retrieved.", "WARN");
            }

            CPH.SetArgument("nickname", preferredUserName);
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"An error occurred in GetNickname: {ex.Message}", "ERROR");
            return false;
        }
    }

    public bool SaveSettings()
    {
        try
        {

            LogToFile("Entering SaveSettings method.", "DEBUG");

            AppSettings settings = new AppSettings
            {
                OpenApiKey = EncryptData(CPH.GetGlobalVar<string>("OpenAI API Key", true)),
                OpenAiModel = CPH.GetGlobalVar<string>("OpenAI Model", true),
                DatabasePath = CPH.GetGlobalVar<string>("Database Path", true),
                IgnoreBotUsernames = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true),
                VoiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true),
                StripEmojisFromResponse = CPH.GetGlobalVar<string>("Strip Emojis From Response", true),
                LoggingLevel = CPH.GetGlobalVar<string>("Logging Level", true),
                Version = CPH.GetGlobalVar<string>("Version", true),

                HateAllowed = CPH.GetGlobalVar<bool>("hate_allowed", true).ToString(),
                HateThreateningAllowed = CPH.GetGlobalVar<bool>("hate_threatening_allowed", true).ToString(),
                SelfHarmAllowed = CPH.GetGlobalVar<bool>("self_harm_allowed", true).ToString(),
                ViolenceAllowed = CPH.GetGlobalVar<bool>("violence_allowed", true).ToString(),
                SelfHarmIntentAllowed = CPH.GetGlobalVar<bool>("self_harm_intent_allowed", true).ToString(),
                SelfHarmInstructionsAllowed = CPH.GetGlobalVar<bool>("self_harm_instructions_allowed", true).ToString(),
                HarassmentAllowed = CPH.GetGlobalVar<bool>("harassment_allowed", true).ToString(),
                HarassmentThreateningAllowed = CPH.GetGlobalVar<bool>("harassment_threatening_allowed", true).ToString(),
                IllicitAllowed = CPH.GetGlobalVar<bool>("illicit_allowed", true).ToString(),
                IllicitViolentAllowed = CPH.GetGlobalVar<bool>("illicit_violent_allowed", true).ToString(),
                PostToChat = CPH.GetGlobalVar<bool>("Post To Chat", true).ToString(),
                LogGptQuestionsToDiscord = CPH.GetGlobalVar<string>("Log GPT Questions to Discord", true),
                DiscordWebhookUrl = CPH.GetGlobalVar<string>("Discord Webhook URL", true),
                DiscordBotUsername = CPH.GetGlobalVar<string>("Discord Bot Username", true),
                DiscordAvatarUrl = CPH.GetGlobalVar<string>("Discord Avatar Url", true)
            };

            LogToFile($"OpenApiKey: {settings.OpenApiKey}", "DEBUG");
            LogToFile($"OpenAiModel: {settings.OpenAiModel}", "DEBUG");

            if (string.IsNullOrWhiteSpace(settings.OpenApiKey) || string.IsNullOrWhiteSpace(settings.OpenAiModel) || string.IsNullOrWhiteSpace(settings.DatabasePath) || string.IsNullOrWhiteSpace(settings.IgnoreBotUsernames) || string.IsNullOrWhiteSpace(settings.VoiceAlias) || string.IsNullOrWhiteSpace(settings.LoggingLevel) || string.IsNullOrWhiteSpace(settings.Version) || string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl) || string.IsNullOrWhiteSpace(settings.DiscordBotUsername) || string.IsNullOrWhiteSpace(settings.DiscordAvatarUrl))
            {
                LogToFile("One or more settings are null or empty.", "WARN");
                return false;
            }

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);

            var filePath = Path.Combine(settings.DatabasePath, "settings.json");
            File.WriteAllText(filePath, json);

            LogToFile($"Settings saved successfully. Settings: {json}", "INFO");

            LogToFile("Encryption of OpenAI API Key successful.", "INFO");

            LogToFile("Exiting SaveSettings method.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"Error saving settings: {ex.Message}", "ERROR");
            return false;
        }
    }

    public bool ReadSettings()
    {
        try
        {
            LogToFile("Entering ReadSettings method.", "DEBUG");

            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' value is either not found or not a valid string.", "ERROR");
                return false;
            }

            string filePath = Path.Combine(databasePath, "settings.json");

            if (!File.Exists(filePath))
            {
                LogToFile("Settings file not found.", "WARN");
                return false;
            }

            string json = File.ReadAllText(filePath);

            AppSettings settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json);

            CPH.SetGlobalVar("OpenAI API Key", DecryptData(settings.OpenApiKey), true);
            CPH.SetGlobalVar("OpenAI Model", settings.OpenAiModel, true);
            CPH.SetGlobalVar("Database Path", settings.DatabasePath, true);
            CPH.SetGlobalVar("Ignore Bot Usernames", settings.IgnoreBotUsernames, true);
            CPH.SetGlobalVar("Voice Alias", settings.VoiceAlias, true);
            CPH.SetGlobalVar("Strip Emojis From Response", settings.StripEmojisFromResponse, true);
            CPH.SetGlobalVar("Logging Level", settings.LoggingLevel, true);
            CPH.SetGlobalVar("Version", settings.Version, true);
            CPH.SetGlobalVar("hate_allowed", settings.HateAllowed, true);
            CPH.SetGlobalVar("hate_threatening_allowed", settings.HateThreateningAllowed, true);
            CPH.SetGlobalVar("self_harm_allowed", settings.SelfHarmAllowed, true);
            CPH.SetGlobalVar("violence_allowed", settings.ViolenceAllowed, true);
            CPH.SetGlobalVar("self_harm_intent_allowed", settings.SelfHarmIntentAllowed, true);
            CPH.SetGlobalVar("self_harm_instructions_allowed", settings.SelfHarmInstructionsAllowed, true);
            CPH.SetGlobalVar("harassment_allowed", settings.HarassmentAllowed, true);
            CPH.SetGlobalVar("harassment_threatening_allowed", settings.HarassmentThreateningAllowed, true);
            CPH.SetGlobalVar("illicit_allowed", settings.IllicitAllowed, true);
            CPH.SetGlobalVar("illicit_violent_allowed", settings.IllicitViolentAllowed, true);
            CPH.SetGlobalVar("Post To Chat", settings.PostToChat, true);
            CPH.SetGlobalVar("Log GPT Questions to Discord", settings.LogGptQuestionsToDiscord, true);
            CPH.SetGlobalVar("Discord Webhook URL", settings.DiscordWebhookUrl, true);
            CPH.SetGlobalVar("Discord Bot Username", settings.DiscordBotUsername, true);
            CPH.SetGlobalVar("Discord Avatar Url", settings.DiscordAvatarUrl, true);

            LogToFile($"Settings loaded successfully. Settings: {json}", "INFO");
            LogToFile("Exiting ReadSettings method.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {

            LogToFile($"Error reading settings: {ex.Message}", "ERROR");
            return false;
        }
    }

    private string EncryptData(string data)
    {
        try
        {
            LogToFile("Entering EncryptData method.", "DEBUG");

            LogToFile("Encrypting data with user-specific key.", "INFO");
            byte[] encryptedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(data), null, DataProtectionScope.CurrentUser);

            LogToFile("Converting encrypted data to base64 string.", "INFO");
            string base64EncryptedData = Convert.ToBase64String(encryptedData);
            LogToFile("Data encrypted successfully.", "INFO");
            LogToFile("Exiting EncryptData method.", "DEBUG");
            return base64EncryptedData;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred in EncryptData: {ex.Message}", "ERROR");
            return null;
        }
    }

    private string DecryptData(string encryptedData)
    {
        try
        {
            LogToFile("Entering DecryptData method.", "DEBUG");
            if (string.IsNullOrWhiteSpace(encryptedData))
            {
                LogToFile("Encrypted data is null or empty.", "WARN");
                return null;
            }

            byte[] encryptedDataBytes = Convert.FromBase64String(encryptedData);

            byte[] decryptedData = ProtectedData.Unprotect(encryptedDataBytes, null, DataProtectionScope.CurrentUser);

            string data = Encoding.UTF8.GetString(decryptedData);
            LogToFile("Data decrypted successfully.", "INFO");
            LogToFile("Exiting DecryptData method.", "DEBUG");
            return data;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred in DecryptData: {ex.Message}", "ERROR");
            return null;
        }
    }
}