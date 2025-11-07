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
using LiteDB;

public class CPHInline
{
    private static LiteDatabase _db;
    public Queue<chatMessage> GPTLog { get; set; } = new Queue<chatMessage>();
    public Queue<chatMessage> ChatLog { get; set; } = new Queue<chatMessage>();

    public bool Startup()
    {
        LogToFile(">>> Entering Startup()", "DEBUG");
        try
        {
            if (_db != null)
            {
                LogToFile("Existing LiteDB connection detected. Disposing before reinitialization.", "WARN");
                _db.Dispose();
                _db = null;
                LogToFile("Condition _db != null evaluated TRUE in Startup()", "DEBUG");
            }
            else
            {
                LogToFile("Condition _db != null evaluated FALSE in Startup()", "DEBUG");
            }

            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            LogToFile("Completed GetGlobalVar() successfully.", "DEBUG");

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' global variable is not found or invalid.", "ERROR");
                LogToFile("Condition string.IsNullOrWhiteSpace(databasePath) evaluated TRUE in Startup()", "DEBUG");
                LogToFile("<<< Exiting Startup() with return value: false", "DEBUG");
                return false;
            }

            LogToFile("Condition string.IsNullOrWhiteSpace(databasePath) evaluated FALSE in Startup()", "DEBUG");

            string dbFilePath = Path.Combine(databasePath, "PNGTuberGPT.db");
            _db = new LiteDatabase(dbFilePath);

            var settings = _db.GetCollection<AppSettings>("settings");
            var userProfiles = _db.GetCollection<UserProfile>("user_profiles");
            var keywords = _db.GetCollection<Keyword>("keywords");

            userProfiles.EnsureIndex(x => x.UserName, true);
            userProfiles.EnsureIndex(x => x.PreferredName, false);

            LogToFile("LiteDB initialized with collections: settings, user_profiles, keywords.", "INFO");
            LogToFile("<<< Exiting Startup() with return value: true", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Failed to initialize LiteDB: {ex.Message}", "ERROR");
            LogToFile($"Exception stack trace: {ex.StackTrace}", "DEBUG");
            LogToFile("<<< Exiting Startup() with return value: false", "DEBUG");
            return false;
        }
    }

    public void Dispose()
    {
        LogToFile(">>> Entering Dispose()", "DEBUG");
        try
        {
            _db?.Dispose();
            LogToFile("LiteDB connection closed successfully.", "INFO");
        }
        catch (Exception ex)
        {
            LogToFile($"Error while disposing LiteDB connection: {ex.Message}", "ERROR");
            LogToFile($"Exception stack trace: {ex.StackTrace}", "DEBUG");
        }
    }

    public bool Execute()
    {
        LogToFile(">>> Entering Execute()", "DEBUG");

        LogToFile("Starting initialization of the PNGTuber-GPT application.", "INFO");

        LogToFile("Initialization of PNGTuber-GPT successful. Added all global variables to memory.", "INFO");

        LogToFile("Starting to retrieve the version number from a global variable.", "DEBUG");

        string initializeVersionNumber = CPH.GetGlobalVar<string>("Version", true);
        LogToFile("Completed GetGlobalVar() successfully.", "DEBUG");

        LogToFile($"Retrieved version number: {initializeVersionNumber}", "DEBUG");

        if (string.IsNullOrWhiteSpace(initializeVersionNumber))
        {
            LogToFile("The 'Version' global variable is not found or is empty.", "ERROR");
            LogToFile("Condition string.IsNullOrWhiteSpace(initializeVersionNumber) evaluated TRUE in Execute()", "DEBUG");
            LogToFile("<<< Exiting Execute() with return value: false", "DEBUG");
            return false;
        }
        else
        {
            LogToFile("Condition string.IsNullOrWhiteSpace(initializeVersionNumber) evaluated FALSE in Execute()", "DEBUG");
        }

        LogToFile($"Sending version number to chat: {initializeVersionNumber}", "DEBUG");

        bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
        if (postToChat)
            CPH.SendMessage($"{initializeVersionNumber} has been initialized successfully.", true);
        else
            LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {initializeVersionNumber} has been initialized successfully.", "DEBUG");
        LogToFile("Completed SendMessage() successfully.", "DEBUG");

        LogToFile("Version number sent to chat successfully.", "INFO");

        LogToFile("<<< Exiting Execute() with return value: true", "DEBUG");
        return true;
    }

    public class Keyword
    {
        public ObjectId Id { get; set; }
        public string Word { get; set; }
        public string Definition { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class AppSettings
    {
        public string OpenApiKey { get; set; }
        public string OpenAiModel { get; set; }
        public string DatabasePath { get; set; }
        public string IgnoreBotUsernames { get; set; }

        public string CharacterVoiceAlias_1 { get; set; }
        public string CharacterVoiceAlias_2 { get; set; }
        public string CharacterVoiceAlias_3 { get; set; }
        public string CharacterVoiceAlias_4 { get; set; }
        public string CharacterVoiceAlias_5 { get; set; }

        public string CharacterFile_1 { get; set; }
        public string CharacterFile_2 { get; set; }
        public string CharacterFile_3 { get; set; }
        public string CharacterFile_4 { get; set; }
        public string CharacterFile_5 { get; set; }

        public string CompletionsEndpoint { get; set; }

        public string LoggingLevel { get; set; }
        public string TextCleanMode { get; set; }
        public string HateThreshold { get; set; }
        public string HateThreateningThreshold { get; set; }
        public string HarassmentThreshold { get; set; }
        public string HarassmentThreateningThreshold { get; set; }
        public string SexualThreshold { get; set; }
        public string ViolenceThreshold { get; set; }
        public string ViolenceGraphicThreshold { get; set; }
        public string SelfHarmThreshold { get; set; }
        public string SelfHarmIntentThreshold { get; set; }
        public string SelfHarmInstructionsThreshold { get; set; }
        public string IllicitThreshold { get; set; }
        public string IllicitViolentThreshold { get; set; }
        public string Version { get; set; }
        public string LogGptQuestionsToDiscord { get; set; }
        public string DiscordWebhookUrl { get; set; }
        public string DiscordBotUsername { get; set; }
        public string DiscordAvatarUrl { get; set; }
        public string PostToChat { get; set; }

        public string VoiceEnabled { get; set; }
        public string OutboundWebhookUrl { get; set; }
        public string OutboundWebhookMode { get; set; }

        public bool moderation_enabled { get; set; } = true;
        public bool moderation_rebuke_enabled { get; set; } = true;
        public int max_chat_history { get; set; } = 20;
        public int max_prompt_history { get; set; } = 10;
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

    public class UserProfile
    {
        public ObjectId Id { get; set; }
        public string UserName { get; set; }
        public string PreferredName { get; set; }
        public string Pronouns { get; set; }

        public List<string> Knowledge { get; set; } = new List<string>();
    }

    public UserProfile GetOrCreateUserProfile(string userName)
    {
        LogToFile(">>> Entering GetOrCreateUserProfile()", "DEBUG");
        try
        {
            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            userCollection.EnsureIndex(x => x.UserName, true);
            var profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            LogToFile("Completed FindOne() successfully.", "DEBUG");

            if (profile == null)
            {
                LogToFile("Condition profile == null evaluated TRUE in GetOrCreateUserProfile()", "DEBUG");
                profile = new UserProfile
                {
                    UserName = userName,
                    PreferredName = userName,
                    Pronouns = ""
                };
                userCollection.Insert(profile);
                LogToFile("Completed Insert() successfully.", "DEBUG");
                LogToFile($"[UserProfile] Created new profile for {userName}.", "DEBUG");
            }
            else
            {
                LogToFile("Condition profile == null evaluated FALSE in GetOrCreateUserProfile()", "DEBUG");
            }

            string pronouns = null;
            string pronounSubject = null;
            string pronounObject = null;
            string pronounPossessive = null;
            string pronounReflexive = null;
            string pronounPronoun = null;

            if (!CPH.TryGetArg("pronouns", out pronouns))
                pronouns = CPH.GetGlobalVar<string>("pronouns", false);
            if (!CPH.TryGetArg("pronounSubject", out pronounSubject))
                pronounSubject = CPH.GetGlobalVar<string>("pronounSubject", false);
            if (!CPH.TryGetArg("pronounObject", out pronounObject))
                pronounObject = CPH.GetGlobalVar<string>("pronounObject", false);
            if (!CPH.TryGetArg("pronounPossessive", out pronounPossessive))
                pronounPossessive = CPH.GetGlobalVar<string>("pronounPossessive", false);
            if (!CPH.TryGetArg("pronounReflexive", out pronounReflexive))
                pronounReflexive = CPH.GetGlobalVar<string>("pronounReflexive", false);
            if (!CPH.TryGetArg("pronounPronoun", out pronounPronoun))
                pronounPronoun = CPH.GetGlobalVar<string>("pronounPronoun", false);

            LogToFile($"Pronoun retrieval results: pronouns='{pronouns}', pronounSubject='{pronounSubject}', pronounObject='{pronounObject}', pronounPossessive='{pronounPossessive}', pronounReflexive='{pronounReflexive}', pronounPronoun='{pronounPronoun}'", "DEBUG");

            if (string.IsNullOrWhiteSpace(pronouns))
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(pronounSubject))
                    parts.Add(pronounSubject);
                if (!string.IsNullOrWhiteSpace(pronounObject))
                    parts.Add(pronounObject);
                if (!string.IsNullOrWhiteSpace(pronounPossessive))
                    parts.Add(pronounPossessive);
                if (!string.IsNullOrWhiteSpace(pronounReflexive))
                    parts.Add(pronounReflexive);
                if (!string.IsNullOrWhiteSpace(pronounPronoun))
                    parts.Add(pronounPronoun);
                if (parts.Count > 0)
                    pronouns = $"({string.Join("/", parts)})";
            }

            LogToFile($"Final pronouns value after construction: '{pronouns}'", "DEBUG");

            if (!string.IsNullOrWhiteSpace(pronouns) && !string.Equals(pronouns, profile.Pronouns, StringComparison.Ordinal))
            {
                profile.Pronouns = pronouns;
                userCollection.Update(profile);
                LogToFile($"[UserProfile] Updated pronouns for {userName} to '{pronouns}'.", "INFO");
            }
            else if (string.IsNullOrWhiteSpace(pronouns))
            {
                LogToFile("No pronoun data found in args or globals; no pronoun update performed.", "DEBUG");
            }
            else
            {
                LogToFile("Pronouns found but unchanged; no update needed.", "DEBUG");
            }

            LogToFile("<<< Exiting GetOrCreateUserProfile() with return value: profile", "DEBUG");
            return profile;
        }
        catch (Exception ex)
        {
            LogToFile($"[UserProfile] Error retrieving or creating user profile for {userName}: {ex.Message}", "ERROR");
            LogToFile($"Exception stack trace: {ex.StackTrace}", "DEBUG");
            LogToFile("<<< Exiting GetOrCreateUserProfile() with return value: new UserProfile", "DEBUG");
            return new UserProfile
            {
                UserName = userName,
                PreferredName = userName,
                Pronouns = ""
            };
        }
    }

    private void QueueMessage(chatMessage chatMsg)
    {
        LogToFile(">>> Entering QueueMessage()", "DEBUG");
        LogToFile($"Entering QueueMessage with chatMsg: {chatMsg}", "DEBUG");
        try
        {
            LogToFile($"Enqueuing chat message: {chatMsg}", "INFO");
            ChatLog.Enqueue(chatMsg);
            LogToFile("Variable ChatLog.Count = " + ChatLog.Count, "DEBUG");

            LogToFile($"ChatLog Count after enqueuing: {ChatLog.Count}", "DEBUG");
            int maxChatHistory = CPH.GetGlobalVar<int>("max_chat_history", true);
            LogToFile("Completed GetGlobalVar() successfully.", "DEBUG");
            if (ChatLog.Count > maxChatHistory)
            {
                LogToFile("Condition ChatLog.Count > maxChatHistory evaluated TRUE in QueueMessage()", "DEBUG");
                chatMessage dequeuedMessage = ChatLog.Peek();
                LogToFile($"Dequeuing chat message to maintain queue size: {dequeuedMessage}", "DEBUG");
                ChatLog.Dequeue();
                LogToFile($"ChatLog Count after dequeuing: {ChatLog.Count}", "DEBUG");
            }
            else
            {
                LogToFile("Condition ChatLog.Count > maxChatHistory evaluated FALSE in QueueMessage()", "DEBUG");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while enqueuing or dequeuing a chat message: {ex.Message}", "ERROR");
            LogToFile($"Exception stack trace: {ex.StackTrace}", "DEBUG");
        }
    }

    private void QueueGPTMessage(string userContent, string assistantContent)
    {
        LogToFile(">>> Entering QueueGPTMessage()", "DEBUG");
        LogToFile("Entering QueueGPTMessage with paired messages.", "DEBUG");

        chatMessage userMessage = new chatMessage
        {
            role = "user",
            content = userContent
        };
        LogToFile($"Variable userMessage = {userMessage}", "DEBUG");
        chatMessage assistantMessage = new chatMessage
        {
            role = "assistant",
            content = assistantContent
        };
        LogToFile($"Variable assistantMessage = {assistantMessage}", "DEBUG");
        try
        {
            GPTLog.Enqueue(userMessage);
            GPTLog.Enqueue(assistantMessage);
            LogToFile("Variable GPTLog.Count = " + GPTLog.Count, "DEBUG");

            LogToFile($"Enqueuing user message: {userMessage}", "INFO");
            LogToFile($"Enqueuing assistant message: {assistantMessage}", "INFO");

            int maxPromptHistory = CPH.GetGlobalVar<int>("max_prompt_history", true);
            LogToFile("Completed GetGlobalVar() successfully.", "DEBUG");
            if (GPTLog.Count > maxPromptHistory * 2)
            {
                LogToFile("Condition GPTLog.Count > maxPromptHistory * 2 evaluated TRUE in QueueGPTMessage()", "DEBUG");
                LogToFile("GPTLog limit exceeded. Dequeuing the oldest pair of messages.", "DEBUG");
                GPTLog.Dequeue();
                GPTLog.Dequeue();
            }
            else
            {
                LogToFile("Condition GPTLog.Count > maxPromptHistory * 2 evaluated FALSE in QueueGPTMessage()", "DEBUG");
            }

            LogToFile($"GPTLog Count after enqueueing/dequeueing: {GPTLog.Count}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while enqueuing GPT messages: {ex.Message}", "ERROR");
            LogToFile($"Exception stack trace: {ex.StackTrace}", "DEBUG");
        }
    }

    public bool UpdateUserPreferredName()
    {
        LogToFile("Entering UpdateUserPreferredName method.", "DEBUG");
        try
        {
            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("Failed to retrieve 'userName' argument via TryGetArg in UpdateUserPreferredName.", "DEBUG");
                LogToFile("userName missing.", "ERROR");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'userName' argument via TryGetArg in UpdateUserPreferredName: {userName}", "DEBUG");
            }

            if (!CPH.TryGetArg("rawInput", out string preferredName) || string.IsNullOrWhiteSpace(preferredName))
            {
                LogToFile("Failed to retrieve 'rawInput' argument via TryGetArg in UpdateUserPreferredName.", "DEBUG");
                LogToFile("preferredName missing.", "ERROR");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'rawInput' argument via TryGetArg in UpdateUserPreferredName: {preferredName}", "DEBUG");
            }

            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            var profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                profile = new UserProfile { UserName = userName, PreferredName = preferredName, Pronouns = "" };
                userCollection.Insert(profile);
                LogToFile($"Created new profile for {userName} with preferred name {preferredName}.", "INFO");
            }
            else
            {
                profile.PreferredName = preferredName;
                userCollection.Update(profile);
                LogToFile($"Updated preferred name for {userName} to {preferredName}.", "INFO");
            }

            string message = $"{userName}, your nickname has been set to {preferredName}.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
            {
                CPH.SendMessage(message, true);
            }
            else
            {
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            }
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in UpdateUserPreferredName: {ex.Message}", "ERROR");
            string message = "I'm sorry, I couldn't update your nickname right now.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
            {
                CPH.SendMessage(message, true);
            }
            else
            {
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            }
            return false;
        }
    }

    public bool DeleteUserProfile()
    {
        LogToFile("Entering DeleteUserProfile method.", "DEBUG");
        try
        {
            if (!CPH.TryGetArg("userName", out string userName))
            {
                LogToFile("Failed to retrieve 'userName' argument via TryGetArg in DeleteUserProfile.", "DEBUG");
                LogToFile("userName missing.", "ERROR");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'userName' argument via TryGetArg in DeleteUserProfile: {userName}", "DEBUG");
            }

            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            var profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                string message = $"{userName}, you don't have a custom nickname set.";
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                LogToFile($"No profile found for {userName}.", "INFO");
                return true;
            }

            profile.PreferredName = userName;
            userCollection.Update(profile);
            LogToFile($"Reset preferred name for {userName}.", "INFO");

            {
                string message = $"{userName}, your nickname has been reset to your username.";
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            }
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DeleteUserProfile: {ex.Message}", "ERROR");
            string message = "An error occurred while resetting your nickname.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }
    }

    public bool ShowCurrentUserProfile()
    {
        LogToFile("Entering ShowCurrentUserProfile method.", "DEBUG");
        try
        {
            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("Failed to retrieve 'userName' argument via TryGetArg in ShowCurrentUserProfile.", "DEBUG");
                LogToFile("userName argument missing.", "ERROR");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'userName' argument via TryGetArg in ShowCurrentUserProfile: {userName}", "DEBUG");
            }

            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            var profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            string displayName = profile.PreferredName;
            string message = string.IsNullOrWhiteSpace(profile?.Pronouns)
                ? $"Your current username is set to {displayName}"
                : $"Your current username is set to {displayName} ({profile.Pronouns})";

            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            LogToFile($"Displayed profile for {userName}: {message}", "INFO");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in ShowCurrentUserProfile: {ex.Message}", "ERROR");
            return false;
        }
    }

    public bool ForgetThis()
    {
        LogToFile("Entering ForgetThis method (LiteDB).", "DEBUG");
        try
        {
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);

            if (!CPH.TryGetArg("rawInput", out string keywordToRemove) || string.IsNullOrWhiteSpace(keywordToRemove))
            {
                LogToFile("Failed to retrieve 'rawInput' argument via TryGetArg in ForgetThis.", "DEBUG");
                if (postToChat)
                    CPH.SendMessage("You need to tell me what to forget.", true);
                else
                    LogToFile("[Skipped Chat Output] Post To Chat disabled. Message: You need to tell me what to forget.", "DEBUG");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'rawInput' argument via TryGetArg in ForgetThis: {keywordToRemove}", "DEBUG");
            }

            var keywordsCol = _db.GetCollection<BsonDocument>("Keywords");
            var existing = keywordsCol.FindAll().FirstOrDefault(doc =>
                string.Equals(doc["Keyword"]?.AsString, keywordToRemove, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                keywordsCol.Delete(existing["_id"]);
                LogToFile($"Removed keyword '{keywordToRemove}' from LiteDB.", "INFO");
                if (postToChat)
                    CPH.SendMessage($"The definition for '{keywordToRemove}' has been removed.", true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: The definition for '{keywordToRemove}' has been removed.", "DEBUG");
            }
            else
            {
                LogToFile($"No definition found for keyword '{keywordToRemove}'.", "INFO");
                if (postToChat)
                    CPH.SendMessage($"I don't have a definition stored for '{keywordToRemove}'.", true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: I don't have a definition stored for '{keywordToRemove}'.", "DEBUG");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred in ForgetThis: {ex.Message}", "ERROR");
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage("An error occurred while attempting to forget that. Please try again later.", true);
            else
                LogToFile("[Skipped Chat Output] Post To Chat disabled. Message: An error occurred while attempting to forget that. Please try again later.", "DEBUG");
            return false;
        }
    }

    public bool ForgetThisAboutMe()
    {
        LogToFile("Entering ForgetThisAboutMe method.", "DEBUG");
        try
        {
            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("Failed to retrieve 'userName' argument via TryGetArg in ForgetThisAboutMe.", "DEBUG");
                LogToFile("userName missing.", "ERROR");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'userName' argument via TryGetArg in ForgetThisAboutMe: {userName}", "DEBUG");
            }

            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            var profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);

            if (profile == null || profile.Knowledge == null || profile.Knowledge.Count == 0)
            {
                LogToFile($"No memories found for {userName}.", "INFO");
                if (postToChat)
                {
                    CPH.SendMessage($"{userName}, I don't have any memories stored for you.", true);
                }
                else
                {
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {userName}, I don't have any memories stored for you.", "DEBUG");
                }
                return true;
            }

            profile.Knowledge.Clear();
            userCollection.Update(profile);
            LogToFile($"Cleared all memories for {userName}.", "INFO");

            if (postToChat)
            {
                CPH.SendMessage($"{userName}, all your memories have been cleared.", true);
            }
            else
            {
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {userName}, all your memories have been cleared.", "DEBUG");
            }

            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in ForgetThisAboutMe: {ex.Message}", "ERROR");

            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
            {
                CPH.SendMessage("An error occurred while attempting to clear your memories. Please try again later.", true);
            }
            else
            {
                LogToFile("[Skipped Chat Output] Post To Chat disabled. Message: An error occurred while attempting to clear your memories. Please try again later.", "DEBUG");
            }

            return false;
        }
    }

    public bool GetMemory()
    {
        LogToFile("Entering GetMemory method.", "DEBUG");
        try
        {
            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("GetMemory: 'userName' argument missing or empty.", "WARN");
                return false;
            }
            LogToFile($"GetMemory: Using userName='{userName}'", "DEBUG");

            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            var profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (profile == null || profile.Knowledge == null || profile.Knowledge.Count == 0)
            {
                string noneMsg = $"I don’t have any saved memories for {userName}.";
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage(noneMsg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {noneMsg}", "DEBUG");
                LogToFile($"GetMemory: No profile or memories for '{userName}'.", "INFO");
                return true;
            }

            var combinedMemory = string.Join(", ", profile.Knowledge);
            var message = $"Something I remember about {userName} is: {combinedMemory}.";
            bool postToChat2 = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat2)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            LogToFile($"GetMemory: Retrieved memory for '{userName}': {combinedMemory}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in GetMemory: {ex.Message}", "ERROR");
            string message = "Sorry, something went wrong while retrieving your memory.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }
    }

    public bool SaveMessage()
    {
        LogToFile("Entering SaveMessage method.", "DEBUG");

        if (!CPH.TryGetArg("rawInput", out string msg) || string.IsNullOrWhiteSpace(msg))
        {
            LogToFile("Failed to retrieve 'rawInput' argument via TryGetArg in SaveMessage.", "DEBUG");
            return false;
        }

        if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
        {
            LogToFile("Failed to retrieve 'userName' argument via TryGetArg in SaveMessage.", "DEBUG");
            return false;
        }
        LogToFile($"Successfully retrieved arguments: userName={userName}, rawInput={msg}", "DEBUG");

        var profile = GetOrCreateUserProfile(userName);
        string displayName = profile.PreferredName;
        if (!string.IsNullOrWhiteSpace(profile.Pronouns))
            displayName += $" {profile.Pronouns}";

        LogToFile($"Computed displayName: {displayName}", "DEBUG");

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

        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            LogToFile("ChatLog queue has been initialized.", "DEBUG");
        }

        string messageContent = $"{displayName} says: {msg}";
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
            string message = "Chat history is already empty.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }

        try
        {
            ChatLog.Clear();
            LogToFile("Chat history has been successfully cleared.", "INFO");
            string message = "Chat history has been cleared.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while clearing the chat history: {ex.Message}", "ERROR");
            string message = "I was unable to clear the chat history. Please check the log file for more details.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }
    }

    public bool PerformModeration()
    {
        LogToFile("==== Begin Moderation Evaluation ====", "INFO");
        LogToFile("Entering PerformModeration method.", "DEBUG");

        bool moderationEnabled = CPH.GetGlobalVar<bool>("moderation_enabled", true);
        bool moderationRebukeEnabled = CPH.GetGlobalVar<bool>("moderation_rebuke_enabled", true);
        bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
        LogToFile($"[INFO] 'Post To Chat' global variable: {postToChat}", "INFO");

        if (!CPH.TryGetArg("rawInput", out string input) || string.IsNullOrWhiteSpace(input))
        {
            LogToFile("PerformModeration failed: missing or invalid rawInput", "ERROR");
            if (postToChat)
                CPH.SendMessage("Moderation failed — message could not be processed.", true);
            LogToFile("==== End Moderation Evaluation ====", "INFO");
            return false;
        }

        if (!moderationEnabled)
        {
            LogToFile("Moderation is globally disabled by settings.", "INFO");
            CPH.SetArgument("moderatedMessage", input);
            LogToFile("==== End Moderation Evaluation ====", "INFO");
            return true;
        }

        LogToFile($"Message for moderation: {input}", "INFO");

        try
        {
            var response = CallModerationEndpoint(input);
            if (response?.Results == null || response.Results.Count == 0)
            {
                LogToFile("PerformModeration failed: moderation endpoint returned null or empty results", "ERROR");
                LogToFile("[DEBUG] Moderation API call failed or returned no results (communication or flagging failure).", "DEBUG");
                if (postToChat)
                    CPH.SendMessage("Moderation failed — message could not be processed.", true);
                LogToFile("==== End Moderation Evaluation ====", "INFO");
                return false;
            }
            else
            {
                LogToFile("[DEBUG] Moderation API call succeeded and returned results.", "DEBUG");
            }

            var result = response.Results[0];
            var scores = result.Category_scores ?? new Dictionary<string, double>();

            var flaggedCategories = new List<string>();

            LogToFile("Moderation Results (using local thresholds)", "INFO");
            LogToFile("--------------------------------------------------", "INFO");
            LogToFile($"{"Category",-25}{"Score",-10}{"Threshold",-12}{"Flagged",-8}", "INFO");
            LogToFile("--------------------------------------------------", "INFO");

            foreach (var kvp in scores)
            {
                string category = kvp.Key;
                double score = kvp.Value;

                double threshold = category switch
                {
                    "violence" => ParseThreshold(CPH.GetGlobalVar<string>("violence_threshold", true), 0.5),
                    "violence/graphic" => ParseThreshold(CPH.GetGlobalVar<string>("violence_graphic_threshold", true), 0.5),
                    "self-harm" => ParseThreshold(CPH.GetGlobalVar<string>("self_harm_threshold", true), 0.4),
                    "self-harm/intent" => ParseThreshold(CPH.GetGlobalVar<string>("self_harm_intent_threshold", true), 0.4),
                    "self-harm/instructions" => ParseThreshold(CPH.GetGlobalVar<string>("self_harm_instructions_threshold", true), 0.4),
                    "harassment" => ParseThreshold(CPH.GetGlobalVar<string>("harassment_threshold", true), 0.5),
                    "harassment/threatening" => ParseThreshold(CPH.GetGlobalVar<string>("harassment_threatening_threshold", true), 0.5),
                    "hate" => ParseThreshold(CPH.GetGlobalVar<string>("hate_threshold", true), 0.5),
                    "hate/threatening" => ParseThreshold(CPH.GetGlobalVar<string>("hate_threatening_threshold", true), 0.5),
                    "illicit" => ParseThreshold(CPH.GetGlobalVar<string>("illicit_threshold", true), 0.5),
                    "illicit/violent" => ParseThreshold(CPH.GetGlobalVar<string>("illicit_violent_threshold", true), 0.5),
                    "sexual" => ParseThreshold(CPH.GetGlobalVar<string>("sexual_threshold", true), 0.5),
                    _ => 0.5
                };

                bool flagged = score >= threshold;
                if (flagged)
                    flaggedCategories.Add(category);

                LogToFile($"{category,-25}{score,-10:F3}{threshold,-12:F2}{(flagged ? "Yes" : "No"),-8}", "INFO");
            }

            LogToFile("--------------------------------------------------", "INFO");
            if (flaggedCategories.Any())
                LogToFile($"Flagged Categories: {string.Join(", ", flaggedCategories)}", "INFO");
            else
                LogToFile("Flagged Categories: None", "INFO");

            bool passed = !flaggedCategories.Any();
            if (!passed)
            {
                // Only rebuke if enabled, do not send "could not process" message
                HandleModerationResponse(flaggedCategories, input, moderationRebukeEnabled);
                LogToFile("==== End Moderation Evaluation ====", "INFO");
                return false;
            }
            else
            {
                // Passed moderation, no additional chat output
                CPH.SetArgument("moderatedMessage", input);
                LogToFile("Message passed moderation.", "DEBUG");
                LogToFile("==== End Moderation Evaluation ====", "INFO");
                return true;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred in PerformModeration: {ex.Message}", "ERROR");
            LogToFile($"Exception stack trace: {ex.StackTrace}", "DEBUG");
            LogToFile("[DEBUG] Moderation API call failed or exception thrown (communication or flagging failure).", "DEBUG");
            if (postToChat)
                CPH.SendMessage("Moderation failed — message could not be processed.", true);
            LogToFile("==== End Moderation Evaluation ====", "INFO");
            return false;
        }
    }

    private bool HandleModerationResponse(List<string> flaggedCategories, string input, bool rebukeEnabled)
    {
        bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
        bool voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);

        // If no flagged categories, proceed as usual (success path)
        if (!flaggedCategories.Any())
        {
            CPH.SetArgument("moderatedMessage", input);
            LogToFile("Message passed moderation.", "DEBUG");
            return true;
        }

        // If flagged categories exist, apply unified guard for TTS and chat
        if (!postToChat)
        {
            LogToFile("[Skipped Moderation Output] Post To Chat is disabled; skipping all moderation rebuke, TTS, and chat output.", "INFO");
            return false;
        }

        if (!rebukeEnabled)
        {
            LogToFile("[Skipped Moderation Output] Moderation rebuke is disabled by settings. Skipping TTS and chat output.", "INFO");
            return false;
        }

        // At this point, postToChat == true && rebukeEnabled == true
        string flaggedCategoriesString = string.Join(", ", flaggedCategories);
        string outputMessage = $"This message was flagged in the following categories: {flaggedCategoriesString}. Repeated attempts at abuse may result in a ban.";
        LogToFile(outputMessage, "INFO");

        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        // Only do TTS if all relevant conditions are true
        if (postToChat && rebukeEnabled && voiceEnabled && !string.IsNullOrWhiteSpace(voiceAlias))
        {
            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            LogToFile($"TTS speak result: {speakResult}", "DEBUG");
        }
        else
        {
            LogToFile($"[Skipped TTS Output] TTS not performed. Conditions: PostToChat={postToChat}, RebukeEnabled={rebukeEnabled}, VoiceEnabled={voiceEnabled}, VoiceAliasPresent={!string.IsNullOrWhiteSpace(voiceAlias)}", "DEBUG");
        }

        // Only post to chat if postToChat && rebukeEnabled
        if (postToChat && rebukeEnabled)
        {
            CPH.SendMessage(outputMessage, true);
        }
        else
        {
            LogToFile($"[Skipped Chat Output] Conditions not met for chat output. PostToChat={postToChat}, RebukeEnabled={rebukeEnabled}. Message: {outputMessage}", "DEBUG");
        }

        return false;
    }

    private ModerationResponse CallModerationEndpoint(string prompt)
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
            using (Stream responseStream = moderationWebResponse.GetResponseStream())
            using (StreamReader responseReader = new StreamReader(responseStream))
            {
                string moderationResponseContent = responseReader.ReadToEnd();

                var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);

                if (moderationJsonResponse?.Results == null || !moderationJsonResponse.Results.Any())
                {
                    LogToFile("No moderation results were returned from the API.", "ERROR");
                    return null;
                }

                return moderationJsonResponse;
            }
        }
        catch (WebException webEx)
        {
            // Log status and response content separately
            if (webEx.Response != null)
            {
                try
                {
                    var httpResp = webEx.Response as HttpWebResponse;
                    if (httpResp != null)
                    {
                        LogToFile($"HTTP Status: {httpResp.StatusCode}", "ERROR");
                    }
                    using (var stream = webEx.Response.GetResponseStream())
                    using (var reader = new StreamReader(stream ?? new MemoryStream()))
                    {
                        string responseContent = reader.ReadToEnd();
                        LogToFile($"A WebException was caught during the moderation request: {responseContent}", "ERROR");
                    }
                }
                catch (Exception ex2)
                {
                    LogToFile($"Failed to read WebException response: {ex2.Message}", "ERROR");
                }
            }
            else
            {
                LogToFile($"A WebException was caught during the moderation request: {webEx.Status}", "ERROR");
            }
            return null;
        }
        catch (Exception ex)
        {
            LogToFile($"An exception occurred while calling the moderation endpoint: {ex.Message}", "ERROR");
            if (ex.InnerException != null)
                LogToFile($"Inner exception: {ex.InnerException.Message}", "ERROR");
            return null;
        }
    }

    private double ParseThreshold(string raw, double fallback)
    {
        double t;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out t))
            t = fallback;

        if (t < 0.0) t = 0.0;
        if (t > 1.0) t = 1.0;
        return t;
    }

    public bool Speak()
    {
        LogToFile("Entering Speak method (LiteDB + Pronoun integration).", "DEBUG");
        try
        {
            int characterNumber = 1;
            try
            {
                characterNumber = CPH.GetGlobalVar<int>("character", true);
                LogToFile($"Active character set to {characterNumber}.", "INFO");
            }
            catch
            {
                LogToFile("No active 'character' variable found. Defaulting to 1.", "WARN");
            }

            string voiceAlias = CPH.GetGlobalVar<string>($"character_voice_alias_{characterNumber}", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                string err = $"No voice alias configured for Character {characterNumber}. Please set 'character_voice_alias_{characterNumber}'.";
                LogToFile(err, "ERROR");
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage(err, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {err}", "DEBUG");
                return false;
            }

            string messageToSpeak = null;
            if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
                messageToSpeak = moderatedMessage;
            else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
                messageToSpeak = rawInput;
            else
                messageToSpeak = "";

            if (string.IsNullOrWhiteSpace(messageToSpeak))
            {
                LogToFile("No text provided to speak.", "ERROR");
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage("No text provided to speak.", true);
                else
                    LogToFile("[Skipped Chat Output] Post To Chat disabled. Message: No text provided to speak.", "DEBUG");
                return false;
            }

            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("'userName' argument is missing or empty in Speak().", "ERROR");
                return false;
            }
            var profile = GetOrCreateUserProfile(userName);

            string formattedUser = profile.PreferredName;
            LogToFile($"Speak(): Using PreferredName '{formattedUser}' for user '{userName}'.", "DEBUG");

            string outputMessage = $"{formattedUser} said: {messageToSpeak}";
            LogToFile($"Character {characterNumber} ({voiceAlias}) speaking message: {outputMessage}", "INFO");

            // Check global flags
            bool postToChatFlag = CPH.GetGlobalVar<bool>("Post To Chat", true);
            bool voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);

            // Send to chat if enabled
            if (postToChatFlag)
            {
                CPH.SendMessage(outputMessage, true);
            }
            else
            {
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {outputMessage}", "DEBUG");
            }

            // Speak via TTS if enabled
            if (voiceEnabled)
            {
                int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
                if (speakResult != 0)
                {
                    LogToFile($"TTS returned non-zero result code: {speakResult}", "WARN");
                    return false;
                }
            }
            else
            {
                LogToFile($"[Skipped TTS Output] Voice disabled. Message: {outputMessage}", "DEBUG");
            }

            // CPH.SetGlobalVar("character", 1, true);
            // LogToFile("Reset 'character' global to 1 after speaking.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"Speak() encountered an error: {ex.Message}", "ERROR");
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage("An internal error occurred while speaking.", true);
            else
                LogToFile("[Skipped Chat Output] Post To Chat disabled. Message: An internal error occurred while speaking.", "DEBUG");
            return false;
        }
    }

    public bool RememberThis()
    {
        LogToFile("Entering RememberThis method.", "DEBUG");
        try
        {
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            bool voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);

            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("Failed to retrieve 'userName' argument via TryGetArg in RememberThis.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'userName' argument via TryGetArg in RememberThis: {userName}", "DEBUG");
            }

            string fullMessage = null;
            if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
            {
                fullMessage = moderatedMessage;
                LogToFile("Retrieved 'moderatedMessage' via TryGetArg in RememberThis.", "DEBUG");
            }
            else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
            {
                fullMessage = rawInput;
                LogToFile("Retrieved 'rawInput' via TryGetArg in RememberThis.", "DEBUG");
            }

            if (string.IsNullOrWhiteSpace(fullMessage))
            {
                LogToFile("Failed to retrieve valid message content via TryGetArg in RememberThis.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }

            string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                LogToFile("'Voice Alias' is not set or invalid.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'Voice Alias' global variable in RememberThis: {voiceAlias}", "DEBUG");
            }

            var parts = fullMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                LogToFile("Message does not contain enough parts for keyword and definition.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }

            string keyword = parts[0];
            string definition = string.Join(" ", parts.Skip(1));

            var keywordsCol = _db.GetCollection<BsonDocument>("Keywords");
            var existing = keywordsCol.FindOne(Query.EQ("Keyword", keyword)) ??
                           keywordsCol.FindAll().FirstOrDefault(doc => string.Equals(doc["Keyword"]?.AsString, keyword, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing["Definition"] = definition;
                keywordsCol.Update(existing);
                LogToFile($"Updated keyword '{keyword}' with new definition in LiteDB.", "INFO");
            }
            else
            {
                var doc = new BsonDocument
                {
                    ["Keyword"] = keyword,
                    ["Definition"] = definition
                };
                keywordsCol.Insert(doc);
                LogToFile($"Inserted new keyword '{keyword}' with definition in LiteDB.", "INFO");
            }

            string outputMessage = $"OK, {userName}, I will remember '{definition}' for '{keyword}'.";
            if (postToChat)
                CPH.SendMessage(outputMessage, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {outputMessage}", "DEBUG");
            LogToFile($"Confirmation message sent to user: {outputMessage}", "INFO");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while trying to remember: {ex.Message}", "ERROR");
            string message = "I'm sorry, but I can't remember that right now. Please try again later.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }
    }

    public bool RememberThisAboutMe()
    {
        LogToFile("Entering RememberThisAboutMe method.", "DEBUG");
        try
        {
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);

            if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("Failed to retrieve 'userName' argument via TryGetArg in RememberThisAboutMe.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }
            else
            {
                LogToFile($"Successfully retrieved 'userName' argument via TryGetArg in RememberThisAboutMe: {userName}", "DEBUG");
            }

            string messageToRemember = null;
            if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
            {
                messageToRemember = moderatedMessage;
                LogToFile("Retrieved 'moderatedMessage' via TryGetArg in RememberThisAboutMe.", "DEBUG");
            }
            else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
            {
                messageToRemember = rawInput;
                LogToFile("Retrieved 'rawInput' via TryGetArg in RememberThisAboutMe.", "DEBUG");
            }

            if (string.IsNullOrWhiteSpace(messageToRemember))
            {
                LogToFile("Failed to retrieve valid message content via TryGetArg in RememberThisAboutMe.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }

            var profile = GetOrCreateUserProfile(userName);
            if (profile == null)
            {
                LogToFile($"Failed to retrieve or create profile for {userName}.", "ERROR");
                string message = "I'm sorry, but I can't remember that right now. Please try again later.";
                if (postToChat)
                    CPH.SendMessage(message, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
                return false;
            }

            if (profile.Knowledge == null)
                profile.Knowledge = new List<string>();

            if (!profile.Knowledge.Contains(messageToRemember))
            {
                profile.Knowledge.Add(messageToRemember);
                LogToFile($"Added new memory for user '{userName}': {messageToRemember}", "INFO");
            }
            else
            {
                LogToFile($"Memory already exists for user '{userName}': {messageToRemember}", "DEBUG");
            }

            var userCollection = _db.GetCollection<UserProfile>("user_profiles");
            userCollection.Update(profile);
            LogToFile($"Updated UserProfile for '{userName}' in LiteDB.", "DEBUG");

            string userToConfirm = !string.IsNullOrWhiteSpace(profile.PreferredName) ? profile.PreferredName : userName;
            string outputMessage = $"OK, {userToConfirm}, I will remember {messageToRemember} about you.";
            if (postToChat)
                CPH.SendMessage(outputMessage, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {outputMessage}", "DEBUG");
            LogToFile($"Confirmation message sent to user: {outputMessage}", "INFO");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred in RememberThisAboutMe: {ex.Message}", "ERROR");
            string message = "I'm sorry, but I can't remember that right now. Please try again later.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
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
            string message = "Prompt history is already empty.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }

        try
        {
            GPTLog.Clear();
            LogToFile("Prompt history has been successfully cleared.", "INFO");
            string message = "Prompt history has been cleared.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"An error occurred while clearing the prompt history: {ex.Message}", "ERROR");
            string message = "I was unable to clear the prompt history. Please check the log file for more details.";
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage(message, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {message}", "DEBUG");
            return false;
        }
    }

    public bool AskGPT()
    {
        LogToFile("==== Begin AskGPT Execution ====", "INFO");
        LogToFile("Entering AskGPT method (streamer.bot pronouns, LiteDB context, webhook/discord sync).", "DEBUG");

        // Global flag checks
        bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
        bool voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);

        int characterNumber = 1;
        try
        {
            characterNumber = CPH.GetGlobalVar<int>("character", true);
            LogToFile($"Active character number set to {characterNumber}.", "INFO");
        }
        catch
        {
            LogToFile("No active 'character' variable found. Defaulting to 1.", "WARN");
        }

        string voiceAlias = CPH.GetGlobalVar<string>($"character_voice_alias_{characterNumber}", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            string err = $"No voice alias configured for Character {characterNumber}. Please set 'character_voice_alias_{characterNumber}'.";
            LogToFile(err, "ERROR");
            if (postToChat)
                CPH.SendMessage(err, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {err}", "DEBUG");
            LogToFile("==== End AskGPT Execution ====", "INFO");
            return false;
        }

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

        if (!CPH.TryGetArg("userName", out string userName) || string.IsNullOrWhiteSpace(userName))
        {
            LogToFile("'userName' argument is not found or not a valid string.", "ERROR");
            string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
            if (postToChat)
                CPH.SendMessage(msg, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
            LogToFile("==== End AskGPT Execution ====", "INFO");
            return false;
        }
        LogToFile("Retrieved and validated 'userName' argument.", "DEBUG");

        string pronounSubject = CPH.GetGlobalVar<string>("pronounSubject", false);
        string pronounObject = CPH.GetGlobalVar<string>("pronounObject", false);
        string pronounPossessive = CPH.GetGlobalVar<string>("pronounPossessive", false);
        string pronounReflexive = CPH.GetGlobalVar<string>("pronounReflexive", false);
        string pronounDescription = "";
        if (!string.IsNullOrWhiteSpace(pronounSubject) && !string.IsNullOrWhiteSpace(pronounObject))
        {
            pronounDescription = $"({pronounSubject}/{pronounObject}";
            if (!string.IsNullOrWhiteSpace(pronounPossessive)) pronounDescription += $"/{pronounPossessive}";
            if (!string.IsNullOrWhiteSpace(pronounReflexive)) pronounDescription += $"/{pronounReflexive}";
            pronounDescription += ")";
        }

        string userToSpeak = userName;
        if (!string.IsNullOrWhiteSpace(pronounDescription))
            userToSpeak = $"{userName} {pronounDescription}";

        string fullMessage = null;
        if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
            fullMessage = moderatedMessage;
        else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
            fullMessage = rawInput;
        else
        {
            LogToFile("Both 'moderatedMessage' and 'rawInput' are not found or are empty strings.", "ERROR");
            string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
            if (postToChat)
                CPH.SendMessage(msg, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
            LogToFile("==== End AskGPT Execution ====", "INFO");
            return false;
        }

        string prompt = $"{userToSpeak} asks: {fullMessage}";
        LogToFile($"Constructed prompt for GPT: {prompt}", "DEBUG");

        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            LogToFile("'Database Path' global variable is not found or not a valid string.", "ERROR");
            string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
            if (postToChat)
                CPH.SendMessage(msg, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
            LogToFile("==== End AskGPT Execution ====", "INFO");
            return false;
        }
        string characterFileName = CPH.GetGlobalVar<string>($"character_file_{characterNumber}", true);
        if (string.IsNullOrWhiteSpace(characterFileName))
        {
            characterFileName = "context.txt";
            LogToFile($"Character file not set for {characterNumber}, defaulting to context.txt", "WARN");
        }
        string ContextFilePath = Path.Combine(databasePath, characterFileName);
        string context = File.Exists(ContextFilePath) ? File.ReadAllText(ContextFilePath) : "";
        string broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
        string currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
        string currentGame = CPH.GetGlobalVar<string>("currentGame", false);

        // Load user_profiles and Keywords collections into memory
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        var allUserProfiles = userCollection.FindAll().ToList();
        var keywordsCol = _db.GetCollection<BsonDocument>("Keywords");
        var keywordDocs = keywordsCol.FindAll().ToList();

        List<string> mentionedUsers = new List<string>();
        if (!mentionedUsers.Contains(userName, StringComparer.OrdinalIgnoreCase))
            mentionedUsers.Add(userName);
        if (!string.IsNullOrWhiteSpace(broadcaster))
        {
            if (fullMessage.IndexOf(broadcaster, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullMessage.IndexOf("@" + broadcaster, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!mentionedUsers.Contains(broadcaster, StringComparer.OrdinalIgnoreCase))
                    mentionedUsers.Add(broadcaster);
            }
        }
        var mentionMatches = System.Text.RegularExpressions.Regex.Matches(fullMessage, @"@(\w+)");
        foreach (System.Text.RegularExpressions.Match match in mentionMatches)
        {
            string muser = match.Groups[1].Value;
            if (!mentionedUsers.Contains(muser, StringComparer.OrdinalIgnoreCase))
                mentionedUsers.Add(muser);
        }

        var pronounContextEntries = new List<string>();
        var askerProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        if (askerProfile != null && !string.IsNullOrWhiteSpace(askerProfile.Pronouns))
            pronounContextEntries.Add($"{askerProfile.PreferredName} uses pronouns {askerProfile.Pronouns}.");
        var broadcasterProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(broadcaster, StringComparison.OrdinalIgnoreCase));
        if (broadcasterProfile != null && !string.IsNullOrWhiteSpace(broadcasterProfile.Pronouns))
            pronounContextEntries.Add($"{broadcasterProfile.PreferredName} uses pronouns {broadcasterProfile.Pronouns}.");
        foreach (var uname in mentionedUsers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mentionedProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(uname, StringComparison.OrdinalIgnoreCase));
            if (mentionedProfile != null && !string.IsNullOrWhiteSpace(mentionedProfile.Pronouns))
                pronounContextEntries.Add($"{mentionedProfile.PreferredName} uses pronouns {mentionedProfile.Pronouns}.");
        }
        var enrichmentSections = new List<string>();
        if (pronounContextEntries.Count > 0)
        {
            string pronounContext = "Known pronouns for participants: " + string.Join(" ", pronounContextEntries);
            enrichmentSections.Add(pronounContext);
            LogToFile($"Added pronoun context system message: {pronounContext}", "DEBUG");
        }
        enrichmentSections.Add($"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}");
        foreach (string uname in mentionedUsers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mentionedProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(uname, StringComparison.OrdinalIgnoreCase));
            if (mentionedProfile != null)
            {
                string preferred = mentionedProfile.PreferredName;
                string pronouns = "";
                if (uname.Equals(userName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pronounDescription))
                    pronouns = $" (pronouns: {pronounDescription})";
                else if (!string.IsNullOrWhiteSpace(mentionedProfile.Pronouns))
                    pronouns = $" (pronouns: {mentionedProfile.Pronouns})";
                enrichmentSections.Add($"User: {preferred}{pronouns}");
                if (mentionedProfile.Knowledge != null && mentionedProfile.Knowledge.Count > 0)
                    enrichmentSections.Add($"Memories about {preferred}: {string.Join("; ", mentionedProfile.Knowledge)}");
            }
        }
        foreach (var doc in keywordDocs)
        {
            string keyword = doc["Keyword"]?.AsString;
            string definition = doc["Definition"]?.AsString;
            if (!string.IsNullOrWhiteSpace(keyword) && !string.IsNullOrWhiteSpace(definition))
            {
                if (fullMessage.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    enrichmentSections.Add($"Something you know about {keyword}: {definition}");
            }
        }
        foreach (var doc in keywordDocs)
        {
            string keyword = doc["Keyword"]?.AsString;
            string definition = doc["Definition"]?.AsString;
            if (!string.IsNullOrWhiteSpace(keyword) && !string.IsNullOrWhiteSpace(definition))
            {
                if (mentionedUsers.Any(n => n.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                    enrichmentSections.Add($"Something you know about {keyword}: {definition}");
            }
        }
        string contextBody = string.Join("\n", enrichmentSections);
        LogToFile("Assembled dynamic context body for GPT prompt (LiteDB):\n" + contextBody, "DEBUG");

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        string completionsRequestJSON = null;
        string completionsResponseContent = null;
        string GPTResponse = null;
        int maxChatHistory = CPH.GetGlobalVar<int>("max_chat_history", true);
        int maxPromptHistory = CPH.GetGlobalVar<int>("max_prompt_history", true);
        string completionsUrl = CPH.GetGlobalVar<string>("openai_completions_url", true);
        if (string.IsNullOrWhiteSpace(completionsUrl))
            completionsUrl = "https://api.openai.com/v1/chat/completions";
        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        string AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);
        var messages = new List<chatMessage>();
        messages.Add(new chatMessage { role = "system", content = contextBody });
        messages.Add(new chatMessage
        {
            role = "user",
            content = "I am going to send you the chat log from Twitch. You should reference these messages for all future prompts if it is relevant to the prompt being asked. Each message will be prefixed with the users name that you can refer to them as, if referring to their message in the response. After each message you receive, you will return simply \"OK\" to indicate you have received this message, and no other text. When I am finished I will say FINISHED, and you will again respond with simply \"OK\" and nothing else, and then resume normal operation on all future prompts."
        });
        messages.Add(new chatMessage { role = "assistant", content = "OK" });
        if (ChatLog != null)
        {
            foreach (var chatMessage in ChatLog.Reverse().Take(maxChatHistory).Reverse())
            {
                messages.Add(chatMessage);
                messages.Add(new chatMessage { role = "assistant", content = "OK" });
            }
        }
        messages.Add(new chatMessage { role = "user", content = "FINISHED" });
        messages.Add(new chatMessage { role = "assistant", content = "OK" });
        if (GPTLog != null)
        {
            foreach (var gptMessage in GPTLog.Reverse().Take(maxPromptHistory).Reverse())
            {
                messages.Add(gptMessage);
            }
        }
        messages.Add(new chatMessage { role = "user", content = $"{prompt} You must respond in less than 500 characters." });
        completionsRequestJSON = JsonConvert.SerializeObject(new { model = AIModel, messages = messages }, Formatting.Indented);
        LogToFile($"Request JSON: {completionsRequestJSON}", "DEBUG");

        // Retry logic for OpenAI API call
        int maxAttempts = 3;
        int attempt = 0;
        bool apiSuccess = false;
        Exception lastException = null;
        int[] backoffSeconds = { 1, 2, 4 };
        for (attempt = 0; attempt < maxAttempts && !apiSuccess; attempt++)
        {
            try
            {
                var completionsWebRequest = WebRequest.Create(completionsUrl) as HttpWebRequest;
                completionsWebRequest.Method = "POST";
                completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                completionsWebRequest.ContentType = "application/json";
                byte[] completionsContentBytes = Encoding.UTF8.GetBytes(completionsRequestJSON);
                using (Stream requestStream = completionsWebRequest.GetRequestStream())
                {
                    requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
                }
                using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
                using (StreamReader responseReader = new StreamReader(completionsWebResponse.GetResponseStream()))
                {
                    completionsResponseContent = responseReader.ReadToEnd();
                    LogToFile($"Response JSON: {completionsResponseContent}", "DEBUG");
                    var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                    GPTResponse = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                    apiSuccess = true;
                }
            }
            catch (WebException webEx)
            {
                lastException = webEx;
                int statusCode = -1;
                string respBody = "";
                if (webEx.Response is HttpWebResponse httpResp)
                {
                    statusCode = (int)httpResp.StatusCode;
                    try
                    {
                        using (var stream = httpResp.GetResponseStream())
                        using (var reader = new StreamReader(stream ?? new MemoryStream()))
                        {
                            respBody = reader.ReadToEnd();
                        }
                    }
                    catch { }
                }
                LogToFile($"OpenAI API request failed (attempt {attempt + 1}/{maxAttempts}). HTTP Status: {statusCode}. Response: {respBody}", "ERROR");
                if (attempt < maxAttempts - 1)
                {
                    LogToFile($"Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
                    System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                LogToFile($"An error occurred during OpenAI API call (attempt {attempt + 1}/{maxAttempts}): {ex.Message}", "ERROR");
                if (attempt < maxAttempts - 1)
                {
                    LogToFile($"Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
                    System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                }
            }
        }

        sw.Stop();
        LogToFile($"OpenAI API call completed in {sw.ElapsedMilliseconds} ms.", "INFO");

        GPTResponse = CleanAIText(GPTResponse);
        LogToFile("Applied CleanAIText() to GPT response.", "DEBUG");
        if (!apiSuccess || string.IsNullOrWhiteSpace(GPTResponse))
        {
            if (!apiSuccess)
            {
                string apiFailMsg = "OpenAI API request failed after multiple attempts.";
                LogToFile(apiFailMsg, "ERROR");
                if (lastException is WebException webExFail && webExFail.Response is HttpWebResponse failResp)
                {
                    int status = (int)failResp.StatusCode;
                    string failBody = "";
                    try
                    {
                        using (var stream = failResp.GetResponseStream())
                        using (var reader = new StreamReader(stream ?? new MemoryStream()))
                        {
                            failBody = reader.ReadToEnd();
                        }
                    }
                    catch { }
                    LogToFile($"Final OpenAI API failure. HTTP Status: {status}. Response: {failBody}", "ERROR");
                }
                if (postToChat)
                    CPH.SendMessage(apiFailMsg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {apiFailMsg}", "DEBUG");
            }
            else
            {
                LogToFile("GPT model did not return a response.", "ERROR");
                string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
                if (postToChat)
                    CPH.SendMessage(msg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
            }
            // Clear temporary collections before exit
            if (allUserProfiles != null) { allUserProfiles.Clear(); allUserProfiles = null; }
            if (keywordDocs != null) { keywordDocs.Clear(); keywordDocs = null; }
            LogToFile("==== End AskGPT Execution ====", "INFO");
            return false;
        }

        LogToFile($"GPT model response: {GPTResponse}", "DEBUG");
        CPH.SetGlobalVar("Response", GPTResponse, true);
        LogToFile("Stored GPT response in global variable 'Response'.", "INFO");

        string outboundWebhookUrl = CPH.GetGlobalVar<string>("outbound_webhook_url", true);
        if (string.IsNullOrWhiteSpace(outboundWebhookUrl))
            outboundWebhookUrl = "https://api.openai.com/v1/chat/completions";
        string outboundWebhookMode = CPH.GetGlobalVar<string>("outbound_webhook_mode", true);
        if ((outboundWebhookMode ?? "").Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            LogToFile("Outbound webhook mode is set to 'Disabled'. Skipping webhook.", "INFO");
        }
        else if (!string.IsNullOrWhiteSpace(outboundWebhookUrl))
        {
            string payload = null;
            if ((outboundWebhookMode ?? "").ToLower() == "clean")
            {
                payload = GPTResponse ?? "";
            }
            else if ((outboundWebhookMode ?? "").ToLower() == "full")
            {
                var fullPayloadObj = new
                {
                    prompt = prompt,
                    contextBody = contextBody,
                    completionsRequestJSON = completionsRequestJSON,
                    completionsResponseContent = completionsResponseContent
                };
                payload = JsonConvert.SerializeObject(fullPayloadObj);
            }
            else
            {
                payload = JsonConvert.SerializeObject(new { response = GPTResponse });
            }
            LogToFile($"Sending outbound webhook payload: {payload}", "INFO");
            try
            {
                var request = WebRequest.Create(outboundWebhookUrl);
                request.Method = "POST";
                if ((outboundWebhookMode ?? "").ToLower() == "clean")
                    request.ContentType = "text/plain";
                else
                    request.ContentType = "application/json";
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(payloadBytes, 0, payloadBytes.Length);
                }
                using (var response = request.GetResponse())
                {
                    LogToFile("Outbound webhook POST successful.", "INFO");
                }
            }
            catch (WebException webEx)
            {
                int status = -1;
                string respBody = "";
                if (webEx.Response is HttpWebResponse resp)
                {
                    status = (int)resp.StatusCode;
                    try
                    {
                        using (var stream = resp.GetResponseStream())
                        using (var reader = new StreamReader(stream ?? new MemoryStream()))
                        {
                            respBody = reader.ReadToEnd();
                        }
                    }
                    catch { }
                }
                LogToFile($"Failed to POST outbound webhook. HTTP Status: {status}. Response: {respBody}", "ERROR");
                string msg = "Outbound webhook failed to deliver response.";
                if (postToChat)
                    CPH.SendMessage(msg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to POST outbound webhook: {ex.Message}", "ERROR");
                string msg = "Outbound webhook failed to deliver response.";
                if (postToChat)
                    CPH.SendMessage(msg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
            }
        }

        // TTS Output: voiceEnabled check
        if (voiceEnabled)
        {
            CPH.TtsSpeak(voiceAlias, GPTResponse, false);
            LogToFile($"Character {characterNumber} spoke GPT's response.", "INFO");
        }
        else
        {
            LogToFile($"[Skipped TTS Output] Voice disabled. Message: {GPTResponse}", "DEBUG");
        }

        // Chat Output: postToChat check
        if (postToChat)
        {
            CPH.SendMessage(GPTResponse, true);
            LogToFile("Sent GPT response to chat.", "INFO");
        }
        else
        {
            LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {GPTResponse}", "DEBUG");
        }

        bool logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
        if (logDiscord)
        {
            PostToDiscord(prompt, GPTResponse);
            LogToFile("Posted GPT result to Discord.", "INFO");
        }

        // CPH.SetGlobalVar("character", 1, true);
        // LogToFile("Reset 'character' global to 1 after AskGPT.", "DEBUG");
        // Explicitly clear in-memory caches to release memory
        if (allUserProfiles != null)
        {
            allUserProfiles.Clear();
            allUserProfiles = null;
        }
        if (keywordDocs != null)
        {
            keywordDocs.Clear();
            keywordDocs = null;
        }
        LogToFile("==== End AskGPT Execution ====", "INFO");
        return true;
    }

    public bool AskGPTWebhook()
    {
        LogToFile("==== Begin AskGPTWebhook Execution ====", "INFO");
        LogToFile("Entering AskGPTWebhook (LiteDB context enrichment, outbound webhook, pronoun support, TTS/chat/discord parity).", "DEBUG");

        // Respect Post To Chat and voice_enabled global variables
        bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
        bool voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);

        string fullMessage = null;
        if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
        {
            fullMessage = moderatedMessage;
            LogToFile("Retrieved 'moderatedMessage' via TryGetArg in AskGPTWebhook.", "DEBUG");
        }
        else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
        {
            fullMessage = rawInput;
            LogToFile("Retrieved 'rawInput' via TryGetArg in AskGPTWebhook.", "DEBUG");
        }
        if (string.IsNullOrWhiteSpace(fullMessage))
        {
            LogToFile("Both 'moderatedMessage' and 'rawInput' are missing or empty.", "ERROR");
            LogToFile("==== End AskGPTWebhook Execution ====", "INFO");
            return false;
        }

        int maxChatHistory = CPH.GetGlobalVar<int>("max_chat_history", true);
        int maxPromptHistory = CPH.GetGlobalVar<int>("max_prompt_history", true);

        int characterNumber = 1;
        try
        {
            characterNumber = CPH.GetGlobalVar<int>("character", true);
            LogToFile($"Active character number set to {characterNumber}.", "INFO");
        }
        catch
        {
            LogToFile("No active 'character' variable found. Defaulting to 1.", "WARN");
        }

        string voiceAlias = CPH.GetGlobalVar<string>($"character_voice_alias_{characterNumber}", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            string err = $"No voice alias configured for Character {characterNumber}. Please set 'character_voice_alias_{characterNumber}'.";
            LogToFile(err, "ERROR");
            if (postToChat)
                CPH.SendMessage(err, true);
            else
                LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {err}", "DEBUG");
            LogToFile("==== End AskGPTWebhook Execution ====", "INFO");
            return false;
        }

        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            LogToFile("'Database Path' global variable is not found or not a valid string.", "ERROR");
            LogToFile("==== End AskGPTWebhook Execution ====", "INFO");
            return false;
        }
        string characterFileName = CPH.GetGlobalVar<string>($"character_file_{characterNumber}", true);
        if (string.IsNullOrWhiteSpace(characterFileName))
        {
            characterFileName = "context.txt";
            LogToFile($"Character file not set for {characterNumber}, defaulting to context.txt", "WARN");
        }
        string ContextFilePath = Path.Combine(databasePath, characterFileName);
        string context = File.Exists(ContextFilePath) ? File.ReadAllText(ContextFilePath) : "";
        string broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
        string currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
        string currentGame = CPH.GetGlobalVar<string>("currentGame", false);

        // Load user_profiles and Keywords collections into memory
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        var allUserProfiles = userCollection.FindAll().ToList();
        var keywordsCol = _db.GetCollection<BsonDocument>("Keywords");
        var keywordDocs = keywordsCol.FindAll().ToList();

        string pronounSubject = CPH.GetGlobalVar<string>("pronounSubject", false);
        string pronounObject = CPH.GetGlobalVar<string>("pronounObject", false);
        string pronounPossessive = CPH.GetGlobalVar<string>("pronounPossessive", false);
        string pronounReflexive = CPH.GetGlobalVar<string>("pronounReflexive", false);
        string pronounDescription = "";
        if (!string.IsNullOrWhiteSpace(pronounSubject) && !string.IsNullOrWhiteSpace(pronounObject))
        {
            pronounDescription = $"({pronounSubject}/{pronounObject}";
            if (!string.IsNullOrWhiteSpace(pronounPossessive)) pronounDescription += $"/{pronounPossessive}";
            if (!string.IsNullOrWhiteSpace(pronounReflexive)) pronounDescription += $"/{pronounReflexive}";
            pronounDescription += ")";
        }

        string userToSpeak = "User";
        if (!string.IsNullOrWhiteSpace(pronounDescription))
            userToSpeak = $"User {pronounDescription}";

        List<string> mentionedUsers = new List<string>();
        if (!string.IsNullOrWhiteSpace(broadcaster))
        {
            if (fullMessage.IndexOf(broadcaster, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullMessage.IndexOf("@" + broadcaster, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!mentionedUsers.Contains(broadcaster, StringComparer.OrdinalIgnoreCase))
                    mentionedUsers.Add(broadcaster);
            }
        }
        var mentionMatches = System.Text.RegularExpressions.Regex.Matches(fullMessage, @"@(\w+)");
        foreach (System.Text.RegularExpressions.Match match in mentionMatches)
        {
            string muser = match.Groups[1].Value;
            if (!mentionedUsers.Contains(muser, StringComparer.OrdinalIgnoreCase))
                mentionedUsers.Add(muser);
        }
        var pronounContextEntries = new List<string>();
        var broadcasterProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(broadcaster, StringComparison.OrdinalIgnoreCase));
        if (broadcasterProfile != null && !string.IsNullOrWhiteSpace(broadcasterProfile.Pronouns))
            pronounContextEntries.Add($"{broadcasterProfile.PreferredName} uses pronouns {broadcasterProfile.Pronouns}.");
        foreach (var uname in mentionedUsers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mentionedProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(uname, StringComparison.OrdinalIgnoreCase));
            if (mentionedProfile != null && !string.IsNullOrWhiteSpace(mentionedProfile.Pronouns))
                pronounContextEntries.Add($"{mentionedProfile.PreferredName} uses pronouns {mentionedProfile.Pronouns}.");
        }
        var enrichmentSections = new List<string>();
        if (pronounContextEntries.Count > 0)
        {
            string pronounContext = "Known pronouns for participants: " + string.Join(" ", pronounContextEntries);
            enrichmentSections.Add(pronounContext);
            LogToFile($"Added pronoun context system message: {pronounContext}", "DEBUG");
        }
        enrichmentSections.Add($"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}");
        foreach (string uname in mentionedUsers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mentionedProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(uname, StringComparison.OrdinalIgnoreCase));
            if (mentionedProfile != null)
            {
                string preferred = mentionedProfile.PreferredName;
                string pronouns = "";
                if (!string.IsNullOrWhiteSpace(mentionedProfile.Pronouns))
                    pronouns = $" (pronouns: {mentionedProfile.Pronouns})";
                enrichmentSections.Add($"User: {preferred}{pronouns}");
                if (mentionedProfile.Knowledge != null && mentionedProfile.Knowledge.Count > 0)
                    enrichmentSections.Add($"Memories about {preferred}: {string.Join("; ", mentionedProfile.Knowledge)}");
            }
        }
        foreach (var doc in keywordDocs)
        {
            string keyword = doc["Keyword"]?.AsString;
            string definition = doc["Definition"]?.AsString;
            if (!string.IsNullOrWhiteSpace(keyword) && !string.IsNullOrWhiteSpace(definition))
            {
                if (fullMessage.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    enrichmentSections.Add($"Something you know about {keyword}: {definition}");
            }
        }
        foreach (var doc in keywordDocs)
        {
            string keyword = doc["Keyword"]?.AsString;
            string definition = doc["Definition"]?.AsString;
            if (!string.IsNullOrWhiteSpace(keyword) && !string.IsNullOrWhiteSpace(definition))
            {
                if (mentionedUsers.Any(n => n.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                    enrichmentSections.Add($"Something you know about {keyword}: {definition}");
            }
        }
        string contextBody = string.Join("\n", enrichmentSections);
        string prompt = $"{userToSpeak} asks: {fullMessage}";
        LogToFile($"Assembled enriched context for webhook:\n{contextBody}", "DEBUG");

        string completionsRequestJSON = null;
        string completionsResponseContent = null;
        string GPTResponse = null;
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        int maxAttempts = 3;
        int[] backoffSeconds = { 1, 2, 4 };
        Exception lastException = null;
        bool apiSuccess = false;
        string apiErrorType = null;
        string apiFailMsg = null;
        string apiFailRespBody = null;
        int apiFailStatus = -1;
        try
        {
            string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
            string AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);
            string completionsUrl = CPH.GetGlobalVar<string>("openai_completions_url", true);
            if (string.IsNullOrWhiteSpace(completionsUrl))
                completionsUrl = "https://api.openai.com/v1/chat/completions";
            LogToFile($"Using completions endpoint: {completionsUrl}", "DEBUG");
            var messages = new List<chatMessage>();
            messages.Add(new chatMessage { role = "system", content = contextBody });
            messages.Add(new chatMessage { role = "user", content = $"{prompt} You must respond in less than 500 characters." });
            completionsRequestJSON = JsonConvert.SerializeObject(new { model = AIModel, messages = messages }, Formatting.Indented);
            LogToFile($"Request JSON: {completionsRequestJSON}", "DEBUG");
            for (int attempt = 0; attempt < maxAttempts && !apiSuccess; attempt++)
            {
                try
                {
                    var completionsWebRequest = WebRequest.Create(completionsUrl) as HttpWebRequest;
                    completionsWebRequest.Method = "POST";
                    completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                    completionsWebRequest.ContentType = "application/json";
                    byte[] completionsContentBytes = Encoding.UTF8.GetBytes(completionsRequestJSON);
                    using (Stream requestStream = completionsWebRequest.GetRequestStream())
                    {
                        requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
                    }
                    using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
                    using (StreamReader responseReader = new StreamReader(completionsWebResponse.GetResponseStream()))
                    {
                        completionsResponseContent = responseReader.ReadToEnd();
                        LogToFile($"Response JSON: {completionsResponseContent}", "DEBUG");
                        var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                        GPTResponse = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                        apiSuccess = true;
                    }
                }
                catch (WebException webEx)
                {
                    lastException = webEx;
                    int statusCode = -1;
                    string respBody = "";
                    if (webEx.Response is HttpWebResponse httpResp)
                    {
                        statusCode = (int)httpResp.StatusCode;
                        try
                        {
                            using (var stream = httpResp.GetResponseStream())
                            using (var reader = new StreamReader(stream ?? new MemoryStream()))
                            {
                                respBody = reader.ReadToEnd();
                            }
                        }
                        catch { }
                    }
                    LogToFile($"OpenAI API request failed (attempt {attempt + 1}/{maxAttempts}). HTTP Status: {statusCode}. Response: {respBody}", "ERROR");
                    if (attempt < maxAttempts - 1)
                    {
                        LogToFile($"Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
                        System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                    }
                    if (attempt == maxAttempts - 1)
                    {
                        apiErrorType = "OpenAI API";
                        apiFailMsg = "OpenAI API request failed after multiple attempts.";
                        apiFailRespBody = respBody;
                        apiFailStatus = statusCode;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogToFile($"An error occurred during OpenAI API call (attempt {attempt + 1}/{maxAttempts}): {ex.Message}", "ERROR");
                    if (attempt < maxAttempts - 1)
                    {
                        LogToFile($"Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
                        System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                    }
                    if (attempt == maxAttempts - 1)
                    {
                        apiErrorType = "OpenAI API";
                        apiFailMsg = "OpenAI API request failed after multiple attempts.";
                        apiFailRespBody = ex.Message;
                        apiFailStatus = -1;
                    }
                }
            }
        }
        finally
        {
            // nothing to clean here yet
        }
        sw.Stop();
        LogToFile($"OpenAI API call completed in {sw.ElapsedMilliseconds} ms.", "INFO");

        GPTResponse = CleanAIText(GPTResponse);
        LogToFile("Applied CleanAIText() to GPT response.", "DEBUG");
        if (!apiSuccess || string.IsNullOrWhiteSpace(GPTResponse))
        {
            if (!apiSuccess)
            {
                string msg = apiFailMsg ?? "OpenAI API request failed after multiple attempts.";
                LogToFile(msg, "ERROR");
                if (apiFailStatus != -1 || !string.IsNullOrEmpty(apiFailRespBody))
                    LogToFile($"Final OpenAI API failure. HTTP Status: {apiFailStatus}. Response: {(apiFailRespBody != null && apiFailRespBody.Length > 300 ? apiFailRespBody.Substring(0, 300) + "..." : apiFailRespBody)}", "ERROR");
                string chatMsg = "GPT API unavailable";
                if (postToChat)
                    CPH.SendMessage(chatMsg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {chatMsg}", "DEBUG");
            }
            else
            {
                LogToFile("GPT model did not return a response.", "ERROR");
                string chatMsg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
                if (postToChat)
                    CPH.SendMessage(chatMsg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {chatMsg}", "DEBUG");
            }
            // Clear temporary collections before exit
            if (allUserProfiles != null) { allUserProfiles.Clear(); allUserProfiles = null; }
            if (keywordDocs != null) { keywordDocs.Clear(); keywordDocs = null; }
            LogToFile("==== End AskGPTWebhook Execution ====", "INFO");
            return false;
        }
        LogToFile($"GPT model response: {GPTResponse}", "DEBUG");
        CPH.SetGlobalVar("Response", GPTResponse, true);
        LogToFile("Stored GPT response in global variable 'Response'.", "INFO");

        // Outbound webhook POST with retry logic and error logging
        string outboundWebhookUrl = CPH.GetGlobalVar<string>("outbound_webhook_url", true);
        if (string.IsNullOrWhiteSpace(outboundWebhookUrl))
            outboundWebhookUrl = "https://api.openai.com/v1/chat/completions";
        string outboundWebhookMode = CPH.GetGlobalVar<string>("outbound_webhook_mode", true);
        bool webhookSuccess = true;
        string webhookFailMsg = null;
        string webhookFailRespBody = null;
        int webhookFailStatus = -1;
        System.Diagnostics.Stopwatch swWebhook = new System.Diagnostics.Stopwatch();
        swWebhook.Start();
        if ((outboundWebhookMode ?? "").Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            LogToFile("Outbound webhook mode is set to 'Disabled'. Skipping webhook.", "INFO");
        }
        else if (!string.IsNullOrWhiteSpace(outboundWebhookUrl))
        {
            string payload = null;
            if ((outboundWebhookMode ?? "").ToLower() == "clean")
            {
                payload = GPTResponse ?? "";
            }
            else if ((outboundWebhookMode ?? "").ToLower() == "full")
            {
                var fullPayloadObj = new
                {
                    prompt = prompt,
                    contextBody = contextBody,
                    completionsRequestJSON = completionsRequestJSON,
                    completionsResponseContent = completionsResponseContent
                };
                payload = JsonConvert.SerializeObject(fullPayloadObj);
            }
            else
            {
                payload = JsonConvert.SerializeObject(new { response = GPTResponse });
            }
            LogToFile($"Sending outbound webhook payload: {payload}", "INFO");
            int maxWebhookAttempts = 3;
            webhookSuccess = false;
            for (int attempt = 0; attempt < maxWebhookAttempts && !webhookSuccess; attempt++)
            {
                try
                {
                    var request = WebRequest.Create(outboundWebhookUrl);
                    request.Method = "POST";
                    if ((outboundWebhookMode ?? "").ToLower() == "clean")
                        request.ContentType = "text/plain";
                    else
                        request.ContentType = "application/json";
                    byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(payloadBytes, 0, payloadBytes.Length);
                    }
                    using (var response = request.GetResponse())
                    {
                        LogToFile("Outbound webhook POST successful.", "INFO");
                        webhookSuccess = true;
                    }
                }
                catch (WebException webEx)
                {
                    int status = -1;
                    string respBody = "";
                    if (webEx.Response is HttpWebResponse resp)
                    {
                        status = (int)resp.StatusCode;
                        try
                        {
                            using (var stream = resp.GetResponseStream())
                            using (var reader = new StreamReader(stream ?? new MemoryStream()))
                            {
                                respBody = reader.ReadToEnd();
                            }
                        }
                        catch { }
                    }
                    LogToFile($"Failed to POST outbound webhook (attempt {attempt + 1}/{maxWebhookAttempts}). HTTP Status: {status}. Response: {respBody}", "ERROR");
                    if (attempt < maxWebhookAttempts - 1)
                    {
                        LogToFile($"Retrying outbound webhook POST after {backoffSeconds[attempt]}s...", "WARN");
                        System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                    }
                    if (attempt == maxWebhookAttempts - 1)
                    {
                        webhookFailMsg = "Webhook delivery failed after 3 attempts";
                        webhookFailRespBody = respBody;
                        webhookFailStatus = status;
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Failed to POST outbound webhook (attempt {attempt + 1}/{maxWebhookAttempts}): {ex.Message}", "ERROR");
                    if (attempt < maxWebhookAttempts - 1)
                    {
                        LogToFile($"Retrying outbound webhook POST after {backoffSeconds[attempt]}s...", "WARN");
                        System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                    }
                    if (attempt == maxWebhookAttempts - 1)
                    {
                        webhookFailMsg = "Webhook delivery failed after 3 attempts";
                        webhookFailRespBody = ex.Message;
                        webhookFailStatus = -1;
                    }
                }
            }
        }
        swWebhook.Stop();
        LogToFile($"Webhook POST (if attempted) completed in {swWebhook.ElapsedMilliseconds} ms.", "INFO");
        if (!webhookSuccess)
        {
            if (webhookFailMsg != null)
            {
                LogToFile(webhookFailMsg, "ERROR");
                if (webhookFailStatus != -1 || !string.IsNullOrEmpty(webhookFailRespBody))
                    LogToFile($"Final webhook failure. HTTP Status: {webhookFailStatus}. Response: {(webhookFailRespBody != null && webhookFailRespBody.Length > 300 ? webhookFailRespBody.Substring(0, 300) + "..." : webhookFailRespBody)}", "ERROR");
                string chatMsg = "Webhook delivery failed after 3 attempts";
                if (postToChat)
                    CPH.SendMessage(chatMsg, true);
                else
                    LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {chatMsg}", "DEBUG");
            }
        }

        // TTS Output: voiceEnabled check
        if (voiceEnabled)
        {
            CPH.TtsSpeak(voiceAlias, GPTResponse, false);
            LogToFile($"Character {characterNumber} spoke GPT's response.", "INFO");
        }
        else
        {
            LogToFile($"[Skipped TTS Output] Voice disabled. Message: {GPTResponse}", "DEBUG");
        }

        // Chat Output: postToChat check
        if (postToChat)
        {
            CPH.SendMessage(GPTResponse, true);
            LogToFile("Sent GPT response to chat.", "INFO");
        }
        else
        {
            LogToFile($"[Skipped Chat Output] Post To Chat disabled. Message: {GPTResponse}", "DEBUG");
        }
        bool logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
        if (logDiscord)
        {
            PostToDiscord(prompt, GPTResponse);
            LogToFile("Posted GPT result to Discord.", "INFO");
        }
        CPH.SetGlobalVar("character", 1, true);
        LogToFile("Reset 'character' global to 1 after AskGPTWebhook.", "DEBUG");
        // Explicitly clear in-memory caches to release memory
        if (allUserProfiles != null)
        {
            allUserProfiles.Clear();
            allUserProfiles = null;
        }
        if (keywordDocs != null)
        {
            keywordDocs.Clear();
            keywordDocs = null;
        }
        LogToFile("==== End AskGPTWebhook Execution ====", "INFO");
        return true;
    }

    private string CleanAIText(string text)
    {
        LogToFile("Entering CleanAIText method.", "DEBUG");
        LogToFile($"Original text: {text}", "DEBUG");

        string mode = CPH.GetGlobalVar<string>("Text Clean Mode", true);
        LogToFile($"Text Clean Mode: {mode}", "DEBUG");

        if (string.IsNullOrWhiteSpace(text))
        {
            LogToFile("Input text is null or whitespace.", "DEBUG");
            return string.Empty;
        }

        // Log guard for very long input
        if (text.Length > 10000)
            LogToFile("Warning: unusually long input to CleanAIText.", "WARN");

        string cleaned = text;
        switch ((mode ?? "").Trim())
        {
            case "Off":
                cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
                LogToFile($"Text Clean Mode is Off. Returning original text: {cleaned}", "DEBUG");
                return cleaned;

            case "StripEmojis":
                var sbEmoji = new System.Text.StringBuilder();
                foreach (var ch in cleaned)
                {
                    var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                    if (uc != UnicodeCategory.OtherSymbol && uc != UnicodeCategory.Surrogate && uc != UnicodeCategory.NonSpacingMark)
                        sbEmoji.Append(ch);
                }
                cleaned = sbEmoji.ToString();
                break;

            case "HumanFriendly":
                string citationPattern = @"\s*\(\[.*?\]\(https?:\/\/[^\)]+\)\)";
                cleaned = Regex.Replace(cleaned, citationPattern, "").Trim();
                LogToFile("Removed markdown-style citations from text.", "DEBUG");

                // Normalize before punctuation fix
                cleaned = cleaned.Normalize(NormalizationForm.FormD);

                // Replace smart punctuation and dash variants with ASCII equivalents
                cleaned = cleaned
                    .Replace("’", "'")
                    .Replace("‘", "'")
                    .Replace("“", "\"")
                    .Replace("”", "\"")
                    .Replace("‒", "-")
                    .Replace("–", "-")
                    .Replace("—", "-")
                    .Replace("―", "-")
                    .Replace("…", "...")
                    .Replace("‐", "-");

                // Remove unwanted characters while preserving normal punctuation and slashes
                var sbHuman = new System.Text.StringBuilder();
                foreach (var ch in cleaned)
                {
                    var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                    if (uc == UnicodeCategory.LowercaseLetter || uc == UnicodeCategory.UppercaseLetter ||
                        uc == UnicodeCategory.TitlecaseLetter || uc == UnicodeCategory.ModifierLetter ||
                        uc == UnicodeCategory.OtherLetter || uc == UnicodeCategory.DecimalDigitNumber ||
                        char.IsWhiteSpace(ch) ||
                        ".,!?;:'\"()-/".Contains(ch))
                    {
                        sbHuman.Append(ch);
                    }
                }

                cleaned = sbHuman.ToString();

                // Collapse multiple dashes and fix spacing issues is now handled in final step
                cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
                break;

            case "Strict":
                string citationPatternStrict = @"\s*\(\[.*?\]\(https?:\/\/[^\)]+\)\)";
                cleaned = Regex.Replace(cleaned, citationPatternStrict, "").Trim();
                LogToFile("Removed markdown-style citations from text.", "DEBUG");

                var sbStrict = new System.Text.StringBuilder();
                foreach (var ch in cleaned)
                {
                    if (char.IsLetterOrDigit(ch) || " .!?,;:'()\"".Contains(ch))
                        sbStrict.Append(ch);
                }
                cleaned = sbStrict.ToString();
                break;

            default:
                LogToFile($"Unknown Text Clean Mode '{mode}'. Defaulting to 'Off'.", "DEBUG");
                cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
                return cleaned;
        }

        string beforeFinal = cleaned;
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        LogToFile($"Text before whitespace normalization: {beforeFinal}", "DEBUG");

        // Final cleanup for HumanFriendly: replace " - " with " ", collapse whitespace, and log
        if ((mode ?? "").Trim() == "HumanFriendly")
        {
            string beforeDashNorm = cleaned;
            cleaned = cleaned.Replace(" - ", " ");
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
            LogToFile("Applied final dash/space normalization in CleanAIText().", "DEBUG");
        }

        LogToFile($"Text after cleaning: {cleaned}", "DEBUG");
        return cleaned;
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

        string completionsUrl = CPH.GetGlobalVar<string>("openai_completions_url", true);
        if (string.IsNullOrWhiteSpace(completionsUrl))
            completionsUrl = "https://api.openai.com/v1/chat/completions";
        LogToFile("All configuration values are valid and present.", "DEBUG");
        LogToFile($"Using completions endpoint: {completionsUrl}", "DEBUG");

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

        WebRequest completionsWebRequest = WebRequest.Create(completionsUrl);
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

        bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
        if (postToChat)
        {
            CPH.SendMessage("!play", true);
            LogToFile("Sent !play command to chat successfully.", "INFO");
        }
        else
        {
            LogToFile("[Skipped Chat Output] Post To Chat disabled. Message: !play", "DEBUG");
        }

        return true;
    }

    public bool SaveSettings()
    {
        try
        {
            LogToFile("Entering SaveSettings method.", "DEBUG");

            var settingsDict = new Dictionary<string, string>
            {
                ["OpenAI API Key"] = EncryptData(CPH.GetGlobalVar<string>("OpenAI API Key", true)),
                ["OpenAI Model"] = CPH.GetGlobalVar<string>("OpenAI Model", true),
                ["Database Path"] = CPH.GetGlobalVar<string>("Database Path", true),
                ["Ignore Bot Usernames"] = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true),
                ["Text Clean Mode"] = CPH.GetGlobalVar<string>("Text Clean Mode", true),
                ["Logging Level"] = CPH.GetGlobalVar<string>("Logging Level", true),
                ["Version"] = CPH.GetGlobalVar<string>("Version", true),
                ["hate_threshold"] = CPH.GetGlobalVar<string>("hate_threshold", true),
                ["hate_threatening_threshold"] = CPH.GetGlobalVar<string>("hate_threatening_threshold", true),
                ["harassment_threshold"] = CPH.GetGlobalVar<string>("harassment_threshold", true),
                ["harassment_threatening_threshold"] = CPH.GetGlobalVar<string>("harassment_threatening_threshold", true),
                ["sexual_threshold"] = CPH.GetGlobalVar<string>("sexual_threshold", true),
                ["violence_threshold"] = CPH.GetGlobalVar<string>("violence_threshold", true),
                ["violence_graphic_threshold"] = CPH.GetGlobalVar<string>("violence_graphic_threshold", true),
                ["self_harm_threshold"] = CPH.GetGlobalVar<string>("self_harm_threshold", true),
                ["self_harm_intent_threshold"] = CPH.GetGlobalVar<string>("self_harm_intent_threshold", true),
                ["self_harm_instructions_threshold"] = CPH.GetGlobalVar<string>("self_harm_instructions_threshold", true),
                ["illicit_threshold"] = CPH.GetGlobalVar<string>("illicit_threshold", true),
                ["illicit_violent_threshold"] = CPH.GetGlobalVar<string>("illicit_violent_threshold", true),
                ["Post To Chat"] = CPH.GetGlobalVar<bool>("Post To Chat", true).ToString(),
                ["Log GPT Questions to Discord"] = CPH.GetGlobalVar<string>("Log GPT Questions to Discord", true),
                ["Discord Webhook URL"] = CPH.GetGlobalVar<string>("Discord Webhook URL", true),
                ["Discord Bot Username"] = CPH.GetGlobalVar<string>("Discord Bot Username", true),
                ["Discord Avatar Url"] = CPH.GetGlobalVar<string>("Discord Avatar Url", true),
                ["character_voice_alias_1"] = CPH.GetGlobalVar<string>("character_voice_alias_1", true),
                ["character_voice_alias_2"] = CPH.GetGlobalVar<string>("character_voice_alias_2", true),
                ["character_voice_alias_3"] = CPH.GetGlobalVar<string>("character_voice_alias_3", true),
                ["character_voice_alias_4"] = CPH.GetGlobalVar<string>("character_voice_alias_4", true),
                ["character_voice_alias_5"] = CPH.GetGlobalVar<string>("character_voice_alias_5", true),
                ["character_file_1"] = CPH.GetGlobalVar<string>("character_file_1", true),
                ["character_file_2"] = CPH.GetGlobalVar<string>("character_file_2", true),
                ["character_file_3"] = CPH.GetGlobalVar<string>("character_file_3", true),
                ["character_file_4"] = CPH.GetGlobalVar<string>("character_file_4", true),
                ["character_file_5"] = CPH.GetGlobalVar<string>("character_file_5", true),
                ["Completions Endpoint"] = CPH.GetGlobalVar<string>("Completions Endpoint", true),
                ["voice_enabled"] = CPH.GetGlobalVar<bool>("voice_enabled", true).ToString(),
                ["outbound_webhook_url"] = CPH.GetGlobalVar<string>("outbound_webhook_url", true),
                ["outbound_webhook_mode"] = CPH.GetGlobalVar<string>("outbound_webhook_mode", true),
                ["moderation_enabled"] = CPH.GetGlobalVar<string>("moderation_enabled", true),
                ["moderation_rebuke_enabled"] = CPH.GetGlobalVar<string>("moderation_rebuke_enabled", true),
                ["max_chat_history"] = CPH.GetGlobalVar<string>("max_chat_history", true),
                ["max_prompt_history"] = CPH.GetGlobalVar<string>("max_prompt_history", true)
            };

            string[] requiredKeys = new string[]
            {
                "OpenAI API Key", "OpenAI Model", "Database Path", "Ignore Bot Usernames", "Logging Level", "Version",
                "Discord Webhook URL", "Discord Bot Username", "Discord Avatar Url",
                "character_voice_alias_1", "character_voice_alias_2", "character_voice_alias_3", "character_voice_alias_4", "character_voice_alias_5",
                "character_file_1", "character_file_2", "character_file_3", "character_file_4", "character_file_5",
                "Completions Endpoint"
            };
            foreach (var key in requiredKeys)
            {
                if (!settingsDict.ContainsKey(key) || string.IsNullOrWhiteSpace(settingsDict[key]))
                {
                    LogToFile($"One or more settings are null or empty: {key}", "WARN");
                    return false;
                }
            }

            var settingsCol = _db.GetCollection<BsonDocument>("settings");
            foreach (var kvp in settingsDict)
            {
                var doc = new LiteDB.BsonDocument
                {
                    ["Key"] = kvp.Key,
                    ["Value"] = kvp.Value
                };

                var existing = settingsCol.FindOne(x => x["Key"] == kvp.Key);
                if (existing != null)
                {
                    existing["Value"] = kvp.Value;
                    settingsCol.Update(existing);
                }
                else
                {
                    settingsCol.Insert(doc);
                }
                LogToFile($"Saved setting: {kvp.Key} = {kvp.Value}", "DEBUG");
            }
            LogToFile("Settings saved successfully to LiteDB.", "INFO");

            CPH.SetGlobalVar("hate_threshold", settingsDict["hate_threshold"], true);
            CPH.SetGlobalVar("hate_threatening_threshold", settingsDict["hate_threatening_threshold"], true);
            CPH.SetGlobalVar("harassment_threshold", settingsDict["harassment_threshold"], true);
            CPH.SetGlobalVar("harassment_threatening_threshold", settingsDict["harassment_threatening_threshold"], true);
            CPH.SetGlobalVar("sexual_threshold", settingsDict["sexual_threshold"], true);
            CPH.SetGlobalVar("violence_threshold", settingsDict["violence_threshold"], true);
            CPH.SetGlobalVar("violence_graphic_threshold", settingsDict["violence_graphic_threshold"], true);
            CPH.SetGlobalVar("self_harm_threshold", settingsDict["self_harm_threshold"], true);
            CPH.SetGlobalVar("self_harm_intent_threshold", settingsDict["self_harm_intent_threshold"], true);
            CPH.SetGlobalVar("self_harm_instructions_threshold", settingsDict["self_harm_instructions_threshold"], true);
            CPH.SetGlobalVar("illicit_threshold", settingsDict["illicit_threshold"], true);
            CPH.SetGlobalVar("illicit_violent_threshold", settingsDict["illicit_violent_threshold"], true);

            CPH.SetGlobalVar("voice_enabled", settingsDict["voice_enabled"], true);
            CPH.SetGlobalVar("outbound_webhook_url", settingsDict["outbound_webhook_url"], true);
            CPH.SetGlobalVar("outbound_webhook_mode", settingsDict["outbound_webhook_mode"], true);

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

            var settingsCol = _db.GetCollection<BsonDocument>("settings");
            var allSettings = settingsCol.FindAll().ToList();
            if (allSettings == null || allSettings.Count == 0)
            {
                LogToFile("Settings record not found in LiteDB.", "WARN");
                return false;
            }

            foreach (var doc in allSettings)
            {
                var key = doc["Key"]?.AsString;
                var value = doc["Value"]?.AsString;
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (key == "OpenAI API Key")
                {
                    value = DecryptData(value);
                }
                CPH.SetGlobalVar(key, value, true);
                LogToFile($"Loaded setting: {key} = {value}", "DEBUG");
            }

            LogToFile("Settings loaded successfully from LiteDB.", "INFO");
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