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
        LogToFile(">>> [Startup] Entry: Initializing LiteDB and collections.", "DEBUG");
        string databasePath = null;
        string dbFilePath = null;
        bool dbFileExists = false;
        try
        {

            LogToFile("[Startup] Checking for existing LiteDB connection.", "DEBUG");
            if (_db != null)
            {
                LogToFile("[Startup] Existing LiteDB connection found. Disposing current instance.", "DEBUG");
                try
                {
                    _db.Dispose();
                    LogToFile("[Startup] Previous LiteDB connection disposed successfully.", "DEBUG");
                }
                catch (Exception exDispose)
                {
                    LogToFile($"[Startup] ERROR disposing previous LiteDB instance: {exDispose.Message}", "ERROR");
                    LogToFile($"[Startup] Dispose Exception stack: {exDispose.StackTrace}", "DEBUG");
                }
                _db = null;
            }
            else
            {
                LogToFile("[Startup] No existing LiteDB connection found.", "DEBUG");
            }

            try
            {
                databasePath = CPH.GetGlobalVar<string>("Database Path", true);
                LogToFile($"[Startup] Retrieved Database Path: '{databasePath}'", "DEBUG");
            }
            catch (Exception exPath)
            {
                LogToFile($"[Startup] Exception while retrieving 'Database Path': {exPath.Message}", "ERROR");
                LogToFile($"[Startup] Stack: {exPath.StackTrace}", "DEBUG");
                LogToFile("<<< [Startup] Exit (failure: could not retrieve database path)", "DEBUG");
                return false;
            }
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("[Startup] ERROR: 'Database Path' global variable is null, empty, or whitespace.", "ERROR");
                LogToFile("<<< [Startup] Exit (failure: invalid database path)", "DEBUG");
                return false;
            }

            try
            {
                dbFilePath = Path.Combine(databasePath, "PNGTuberGPT.db");
                dbFileExists = File.Exists(dbFilePath);
                LogToFile($"[Startup] Database file path: '{dbFilePath}' | Exists: {dbFileExists}", "DEBUG");
            }
            catch (Exception exPath2)
            {
                LogToFile($"[Startup] Exception while composing db file path: {exPath2.Message}", "ERROR");
                LogToFile($"[Startup] Stack: {exPath2.StackTrace}", "DEBUG");
                LogToFile("<<< [Startup] Exit (failure: could not compose db file path)", "DEBUG");
                return false;
            }

            try
            {
                LogToFile($"[Startup] Attempting to open LiteDB at '{dbFilePath}' (file exists: {dbFileExists})", "DEBUG");
                _db = new LiteDatabase(dbFilePath);
                LogToFile("[Startup] LiteDB connection established.", "DEBUG");
            }
            catch (Exception exOpen)
            {
                LogToFile($"[Startup] ERROR: Failed to open LiteDB at '{dbFilePath}': {exOpen.Message}", "ERROR");
                LogToFile($"[Startup] Stack: {exOpen.StackTrace}", "DEBUG");
                LogToFile("<<< [Startup] Exit (failure: could not open LiteDB)", "DEBUG");
                return false;
            }

            try
            {
                LogToFile("[Startup] Setting up collections: settings, user_profiles, keywords.", "DEBUG");
                var settingsCol = _db.GetCollection<AppSettings>("settings");
                var userProfilesCol = _db.GetCollection<UserProfile>("user_profiles");
                var keywordsCol = _db.GetCollection<Keyword>("keywords");

                LogToFile("[Startup] Ensuring indexes on user_profiles: UserName (unique), PreferredName (non-unique).", "DEBUG");
                userProfilesCol.EnsureIndex(x => x.UserName, true);
                userProfilesCol.EnsureIndex(x => x.PreferredName, false);
                LogToFile("[Startup] Collections and indexes ensured successfully.", "DEBUG");
            }
            catch (Exception exCol)
            {
                LogToFile($"[Startup] ERROR: Failed to setup collections or indexes: {exCol.Message}", "ERROR");
                LogToFile($"[Startup] Stack: {exCol.StackTrace}", "DEBUG");
                LogToFile("<<< [Startup] Exit (failure: could not setup collections/indexes)", "DEBUG");
                return false;
            }

            LogToFile($"[Startup] LiteDB initialized successfully at '{dbFilePath}'. File existed: {dbFileExists}. Collections: settings, user_profiles, keywords.", "INFO");
            LogToFile("<<< [Startup] Exit (success)", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[Startup] UNEXPECTED ERROR: {ex.Message}", "ERROR");
            LogToFile($"[Startup] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[Startup] Context: databasePath='{databasePath}', dbFilePath='{dbFilePath}', dbFileExists={dbFileExists}", "ERROR");
            LogToFile("<<< [Startup] Exit (failure: unexpected exception)", "DEBUG");
            return false;
        }
    }

    public void Dispose()
    {
        LogToFile(">>> [Dispose] Entry: Starting disposal of LiteDB connection.", "DEBUG");
        bool dbWasNull = (_db == null);
        try
        {
            LogToFile($"[Dispose] Decision: _db is {(dbWasNull ? "null" : "not null")}.", "DEBUG");
            if (_db != null)
            {
                _db.Dispose();
                LogToFile("[Dispose] LiteDB connection disposed successfully.", "INFO");
            }
            else
            {
                LogToFile("[Dispose] No LiteDB connection to dispose (_db was null).", "DEBUG");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[Dispose] ERROR disposing LiteDB connection: {ex.Message}", "ERROR");
            LogToFile($"[Dispose] Exception stack trace: {ex.StackTrace}", "DEBUG");
            LogToFile($"[Dispose] Context: _db was null: {dbWasNull}", "ERROR");
        }
        LogToFile("<<< [Dispose] Exit: Disposal attempt complete.", "DEBUG");
    }

    public bool Execute()
    {
        LogToFile(">>> [Execute] Entry: Starting initialization sequence.", "DEBUG");
        bool postToChat = false;
        string initializeVersionNumber = null;
        try
        {
            LogToFile("[Execute] INFO: Initializing PNGTuber-GPT application.", "INFO");

            LogToFile("[Execute] INFO: All global variables loaded into memory.", "INFO");

            LogToFile("[Execute] DEBUG: Retrieving version number from global variable 'Version'.", "DEBUG");
            initializeVersionNumber = CPH.GetGlobalVar<string>("Version", true);
            LogToFile("[Execute] DEBUG: GetGlobalVar('Version') completed.", "DEBUG");
            LogToFile($"[Execute] DEBUG: Retrieved version number: {initializeVersionNumber}", "DEBUG");

            if (string.IsNullOrWhiteSpace(initializeVersionNumber))
            {
                LogToFile("[Execute] ERROR: 'Version' global variable is missing or empty. Cannot continue initialization.", "ERROR");
                LogToFile($"[Execute] Context: initializeVersionNumber='{initializeVersionNumber ?? "null"}'", "ERROR");
                LogToFile("[Execute] DEBUG: Condition string.IsNullOrWhiteSpace(initializeVersionNumber) evaluated TRUE.", "DEBUG");
                LogToFile("<<< [Execute] Exit (failure: missing version)", "DEBUG");
                return false;
            }
            else
            {
                LogToFile("[Execute] DEBUG: Condition string.IsNullOrWhiteSpace(initializeVersionNumber) evaluated FALSE.", "DEBUG");
            }

            LogToFile($"[Execute] DEBUG: Preparing to broadcast version. Version='{initializeVersionNumber}'", "DEBUG");
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
            {
                CPH.SendMessage($"{initializeVersionNumber} has been initialized successfully.", true);
                LogToFile($"[Execute] DEBUG: Version message sent to chat. Version='{initializeVersionNumber}', PostToChat={postToChat}", "DEBUG");
            }
            else
            {
                LogToFile($"[Execute] DEBUG: [Skipped Chat Output] Post To Chat disabled. Version='{initializeVersionNumber}', PostToChat={postToChat}", "DEBUG");
            }

            LogToFile($"[Execute] INFO: Version '{initializeVersionNumber}' broadcast completed. PostToChat={postToChat}", "INFO");
            LogToFile("<<< [Execute] Exit (success)", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[Execute] ERROR: Exception during initialization: {ex.Message}", "ERROR");
            LogToFile($"[Execute] DEBUG: Exception stack trace: {ex.StackTrace}", "DEBUG");
            LogToFile($"[Execute] ERROR Context: initializeVersionNumber='{initializeVersionNumber ?? "null"}', PostToChat={postToChat}", "ERROR");
            LogToFile("<<< [Execute] Exit (failure: exception thrown)", "DEBUG");
            return false;
        }
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
        public string ModelInputCost { get; set; }
        public string ModelOutputCost { get; set; }
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

    public class UsageData
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonProperty("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonProperty("total_tokens")]
        public int TotalTokens { get; set; }
    }

    public class ChatCompletionsResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("choices")]
        public List<Choice> Choices { get; set; }

        [JsonProperty("usage")]
        public UsageData Usage { get; set; }
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
        LogToFile($">>> [GetOrCreateUserProfile] Entry: userName='{userName}'", "DEBUG");
        UserProfile profile = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        bool profileExisted = false;
        try
        {
            try
            {
                LogToFile($"[GetOrCreateUserProfile] Retrieving collection 'user_profiles' for userName='{userName}'", "DEBUG");
                userCollection.EnsureIndex(x => x.UserName, true);
            }
            catch (Exception exDb)
            {
                LogToFile($"[GetOrCreateUserProfile] ERROR: Failed to get 'user_profiles' collection for userName='{userName}'. Exception: {exDb.Message}", "ERROR");
                LogToFile($"[GetOrCreateUserProfile] Stack: {exDb.StackTrace}", "DEBUG");
                throw;
            }

            try
            {
                LogToFile($"[GetOrCreateUserProfile] Attempting to find profile for userName='{userName}'", "DEBUG");
                profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                profileExisted = (profile != null);
                LogToFile($"[GetOrCreateUserProfile] Profile {(profileExisted ? "found" : "not found")} for userName='{userName}'", "DEBUG");
                if (!profileExisted)
                {
                    profile = new UserProfile
                    {
                        UserName = userName,
                        PreferredName = userName,
                        Pronouns = ""
                    };
                    userCollection.Insert(profile);
                    LogToFile($"[UserProfile] Created new profile for userName='{userName}'", "DEBUG");
                }
            }
            catch (Exception exFind)
            {
                LogToFile($"[GetOrCreateUserProfile] ERROR: Exception finding/inserting profile for userName='{userName}': {exFind.Message}", "ERROR");
                LogToFile($"[GetOrCreateUserProfile] Stack: {exFind.StackTrace}", "DEBUG");
                throw;
            }

            string pronouns = null;
            string pronounSubject = null;
            string pronounObject = null;
            string pronounPossessive = null;
            string pronounReflexive = null;
            string pronounPronoun = null;
            try
            {
                bool gotPronouns = CPH.TryGetArg("pronouns", out pronouns);
                if (!gotPronouns)
                    pronouns = CPH.GetGlobalVar<string>("pronouns", false);
                bool gotSubject = CPH.TryGetArg("pronounSubject", out pronounSubject);
                if (!gotSubject)
                    pronounSubject = CPH.GetGlobalVar<string>("pronounSubject", false);
                bool gotObject = CPH.TryGetArg("pronounObject", out pronounObject);
                if (!gotObject)
                    pronounObject = CPH.GetGlobalVar<string>("pronounObject", false);
                bool gotPossessive = CPH.TryGetArg("pronounPossessive", out pronounPossessive);
                if (!gotPossessive)
                    pronounPossessive = CPH.GetGlobalVar<string>("pronounPossessive", false);
                bool gotReflexive = CPH.TryGetArg("pronounReflexive", out pronounReflexive);
                if (!gotReflexive)
                    pronounReflexive = CPH.GetGlobalVar<string>("pronounReflexive", false);
                bool gotPronounPronoun = CPH.TryGetArg("pronounPronoun", out pronounPronoun);
                if (!gotPronounPronoun)
                    pronounPronoun = CPH.GetGlobalVar<string>("pronounPronoun", false);
                LogToFile($"[GetOrCreateUserProfile] Pronoun retrieval: userName='{userName}', pronouns='{pronouns}', pronounSubject='{pronounSubject}', pronounObject='{pronounObject}', pronounPossessive='{pronounPossessive}', pronounReflexive='{pronounReflexive}', pronounPronoun='{pronounPronoun}'", "DEBUG");

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
                    {
                        pronouns = $"({string.Join("/", parts)})";
                        LogToFile($"[GetOrCreateUserProfile] Pronouns constructed from parts for userName='{userName}': '{pronouns}'", "DEBUG");
                    }
                    else
                    {
                        LogToFile($"[GetOrCreateUserProfile] WARN: No pronoun data found for userName='{userName}'.", "WARN");
                    }
                }
                else
                {
                    LogToFile($"[GetOrCreateUserProfile] Pronouns found directly for userName='{userName}': '{pronouns}'", "DEBUG");
                }
            }
            catch (Exception exPronoun)
            {
                LogToFile($"[GetOrCreateUserProfile] ERROR: Exception during pronoun retrieval/construction for userName='{userName}': {exPronoun.Message}", "ERROR");
                LogToFile($"[GetOrCreateUserProfile] Stack: {exPronoun.StackTrace}", "DEBUG");

            }

            LogToFile($"[GetOrCreateUserProfile] Final pronouns value for userName='{userName}': '{pronouns}' (profile.Pronouns='{profile.Pronouns}')", "DEBUG");

            try
            {
                if (!string.IsNullOrWhiteSpace(pronouns) && !string.Equals(pronouns, profile.Pronouns, StringComparison.Ordinal))
                {
                    var oldPronouns = profile.Pronouns;
                    profile.Pronouns = pronouns;
                    userCollection.Update(profile);
                    LogToFile($"[UserProfile] Updated pronouns for userName='{userName}' from '{oldPronouns}' to '{pronouns}'.", "DEBUG");
                }
                else if (string.IsNullOrWhiteSpace(pronouns))
                {
                    LogToFile($"[GetOrCreateUserProfile] WARN: No pronoun data found in args or globals for userName='{userName}'; no pronoun update performed.", "WARN");
                }
                else
                {
                    LogToFile($"[GetOrCreateUserProfile] Pronouns found for userName='{userName}' but unchanged; no update needed.", "DEBUG");
                }
            }
            catch (Exception exUpdate)
            {
                LogToFile($"[GetOrCreateUserProfile] ERROR: Exception updating pronouns for userName='{userName}': {exUpdate.Message}", "ERROR");
                LogToFile($"[GetOrCreateUserProfile] Stack: {exUpdate.StackTrace}", "DEBUG");

            }

            LogToFile($"<<< [GetOrCreateUserProfile] Exit: userName='{userName}', profileExisted={profileExisted}, pronouns='{pronouns}'", "DEBUG");
            return profile;
        }
        catch (Exception ex)
        {
            LogToFile($"[GetOrCreateUserProfile] ERROR: Unexpected error for userName='{userName}': {ex.Message}", "ERROR");
            LogToFile($"[GetOrCreateUserProfile] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GetOrCreateUserProfile] Context: userName='{userName}', profileExisted={profileExisted}", "ERROR");
            LogToFile($"<<< [GetOrCreateUserProfile] Exit (failure: exception thrown); returning fallback UserProfile", "DEBUG");
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
        LogToFile(">>> [QueueMessage] Entry: Starting message queue operation.", "DEBUG");
        LogToFile($"[QueueMessage] Received chatMsg: {chatMsg}", "DEBUG");
        try
        {
            if (ChatLog == null)
            {
                LogToFile("[QueueMessage] WARN: ChatLog queue is null. Initializing new queue.", "WARN");
                ChatLog = new Queue<chatMessage>();
            }
            LogToFile("[QueueMessage] ChatLog queue is ready.", "DEBUG");

            ChatLog.Enqueue(chatMsg);
            LogToFile($"[QueueMessage] Chat message enqueued. Message: {chatMsg}", "DEBUG");
            LogToFile($"[QueueMessage] ChatLog.Count after enqueue: {ChatLog.Count}", "DEBUG");

            int maxChatHistory;
            try
            {
                maxChatHistory = CPH.GetGlobalVar<int>("max_chat_history", true);
                LogToFile($"[QueueMessage] Retrieved maxChatHistory: {maxChatHistory}", "DEBUG");
            }
            catch (Exception exLimit)
            {
                LogToFile($"[QueueMessage] WARN: Failed to retrieve 'max_chat_history' global variable: {exLimit.Message}", "WARN");
                maxChatHistory = 20; 
                LogToFile($"[QueueMessage] Using fallback maxChatHistory: {maxChatHistory}", "WARN");
            }

            if (ChatLog.Count > maxChatHistory)
            {
                LogToFile($"[QueueMessage] INFO: ChatLog.Count ({ChatLog.Count}) > maxChatHistory ({maxChatHistory}). Oldest message will be dequeued.", "INFO");
                chatMessage dequeuedMessage = null;
                try
                {
                    dequeuedMessage = ChatLog.Peek();
                    LogToFile($"[QueueMessage] Dequeuing chat message to maintain queue size. Dequeued: {dequeuedMessage}", "DEBUG");
                }
                catch (Exception exPeek)
                {
                    LogToFile($"[QueueMessage] WARN: Exception peeking ChatLog before dequeue: {exPeek.Message}", "WARN");
                }
                try
                {
                    ChatLog.Dequeue();
                    LogToFile($"[QueueMessage] ChatLog.Count after dequeue: {ChatLog.Count}", "DEBUG");
                }
                catch (Exception exDequeue)
                {
                    LogToFile($"[QueueMessage] ERROR: Exception during dequeue: {exDequeue.Message}", "ERROR");
                    LogToFile($"[QueueMessage] ChatLog.Count: {ChatLog.Count}, maxChatHistory: {maxChatHistory}", "ERROR");
                }
                LogToFile($"[QueueMessage] INFO: Oldest message dropped to enforce queue limit. Current ChatLog.Count: {ChatLog.Count}", "INFO");
            }
            else
            {
                LogToFile($"[QueueMessage] DEBUG: ChatLog.Count ({ChatLog.Count}) <= maxChatHistory ({maxChatHistory}). No dequeue needed.", "DEBUG");
                LogToFile($"[QueueMessage] INFO: Message successfully queued. ChatLog.Count: {ChatLog.Count}, maxChatHistory: {maxChatHistory}", "INFO");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[QueueMessage] ERROR: Exception during queue operation: {ex.Message}", "ERROR");
            LogToFile($"[QueueMessage] Exception stack trace: {ex.StackTrace}", "DEBUG");
            LogToFile($"[QueueMessage] Context: ChatLog.Count={(ChatLog != null ? ChatLog.Count.ToString() : "null")}, chatMsg={chatMsg}", "ERROR");
        }
        LogToFile("<<< [QueueMessage] Exit: Message queue operation complete.", "DEBUG");
    }

    private void QueueGPTMessage(string userContent, string assistantContent)
    {

        LogToFile($">>> [QueueGPTMessage] Entry: userContent.Length={userContent?.Length ?? 0}, assistantContent.Length={assistantContent?.Length ?? 0}", "DEBUG");
        bool trimmed = false;
        int priorCount = GPTLog != null ? GPTLog.Count : -1;
        try
        {
            LogToFile("[QueueGPTMessage] Creating chatMessage objects for user and assistant.", "DEBUG");
            chatMessage userMessage = new chatMessage
            {
                role = "user",
                content = userContent
            };
            LogToFile($"[QueueGPTMessage] Created userMessage: role='{userMessage.role}', content.Length={userMessage.content?.Length ?? 0}", "DEBUG");
            chatMessage assistantMessage = new chatMessage
            {
                role = "assistant",
                content = assistantContent
            };
            LogToFile($"[QueueGPTMessage] Created assistantMessage: role='{assistantMessage.role}', content.Length={assistantMessage.content?.Length ?? 0}", "DEBUG");

            if (GPTLog == null)
            {
                LogToFile("[QueueGPTMessage] WARN: GPTLog queue is null. Initializing new queue.", "WARN");
                GPTLog = new Queue<chatMessage>();
            }
            LogToFile("[QueueGPTMessage] GPTLog queue is ready.", "DEBUG");

            GPTLog.Enqueue(userMessage);
            LogToFile($"[QueueGPTMessage] User message enqueued. GPTLog.Count={GPTLog.Count}", "DEBUG");
            GPTLog.Enqueue(assistantMessage);
            LogToFile($"[QueueGPTMessage] Assistant message enqueued. GPTLog.Count={GPTLog.Count}", "DEBUG");

            LogToFile("Queued GPT conversation pair for processing.", "INFO");

            int maxPromptHistory = 10; 
            try
            {
                maxPromptHistory = CPH.GetGlobalVar<int>("max_prompt_history", true);
                LogToFile($"[QueueGPTMessage] Retrieved max_prompt_history: {maxPromptHistory}", "DEBUG");
            }
            catch (Exception exLimit)
            {
                LogToFile($"[QueueGPTMessage] WARN: Failed to retrieve 'max_prompt_history' global variable: {exLimit.Message}", "WARN");
                LogToFile($"[QueueGPTMessage] Using fallback maxPromptHistory: {maxPromptHistory}", "WARN");
            }

            if (GPTLog.Count > maxPromptHistory * 2)
            {
                LogToFile($"[QueueGPTMessage] GPTLog.Count ({GPTLog.Count}) > maxPromptHistory*2 ({maxPromptHistory * 2}). Trimming oldest pair.", "DEBUG");
                trimmed = true;
                chatMessage dequeuedUser = null, dequeuedAssistant = null;
                try
                {
                    dequeuedUser = GPTLog.Peek();
                }
                catch (Exception exPeek1)
                {
                    LogToFile($"[QueueGPTMessage] WARN: Exception peeking GPTLog before dequeue (user): {exPeek1.Message}", "WARN");
                }
                try
                {
                    GPTLog.Dequeue();
                    LogToFile($"[QueueGPTMessage] Dequeued oldest user message. GPTLog.Count={GPTLog.Count}", "DEBUG");
                }
                catch (Exception exDeq1)
                {
                    LogToFile($"[QueueGPTMessage] ERROR: Exception during dequeue (user): {exDeq1.Message}", "ERROR");
                }
                try
                {
                    dequeuedAssistant = GPTLog.Peek();
                }
                catch (Exception exPeek2)
                {
                    LogToFile($"[QueueGPTMessage] WARN: Exception peeking GPTLog before dequeue (assistant): {exPeek2.Message}", "WARN");
                }
                try
                {
                    GPTLog.Dequeue();
                    LogToFile($"[QueueGPTMessage] Dequeued oldest assistant message. GPTLog.Count={GPTLog.Count}", "DEBUG");
                }
                catch (Exception exDeq2)
                {
                    LogToFile($"[QueueGPTMessage] ERROR: Exception during dequeue (assistant): {exDeq2.Message}", "ERROR");
                }
                LogToFile("[QueueGPTMessage] Oldest pair dropped to enforce prompt history limit.", "INFO");
            }
            else
            {
                LogToFile($"[QueueGPTMessage] GPTLog.Count ({GPTLog.Count}) <= maxPromptHistory*2 ({maxPromptHistory * 2}). No trimming needed.", "DEBUG");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[QueueGPTMessage] ERROR: Exception while enqueuing GPT messages: {ex.Message}", "ERROR");
            LogToFile($"[QueueGPTMessage] Context: GPTLog.Count={(GPTLog != null ? GPTLog.Count.ToString() : "null")}, userContent.Snippet='{(userContent?.Length > 32 ? userContent.Substring(0, 32) + "..." : userContent)}', assistantContent.Snippet='{(assistantContent?.Length > 32 ? assistantContent.Substring(0, 32) + "..." : assistantContent)}'", "ERROR");
            LogToFile($"[QueueGPTMessage] Exception stack trace: {ex.StackTrace}", "DEBUG");
        }
        LogToFile($@"<<< [QueueGPTMessage] Exit: GPTLog.Count={(GPTLog != null ? GPTLog.Count.ToString() : "null")}, trimmed={trimmed}", "DEBUG");
    }

    public bool UpdateUserPreferredName()
    {
        LogToFile(">>> [UpdateUserPreferredName] Entry: Starting update of user preferred name.", "DEBUG");
        string userName = null;
        string preferredName = null;
        bool postToChat = false;
        UserProfile profile = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        bool success = false;

        try
        {
            LogToFile("[UpdateUserPreferredName] Attempting to retrieve 'userName' argument via TryGetArg.", "DEBUG");
            if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("[UpdateUserPreferredName] WARN: 'userName' argument missing or empty.", "WARN");
                LogToFile("[UpdateUserPreferredName] Context: userName=null or empty.", "WARN");
                LogToFile("<<< [UpdateUserPreferredName] Exit (failure: missing userName)", "DEBUG");
                return false;
            }
            LogToFile($"[UpdateUserPreferredName] Retrieved userName='{userName}'", "DEBUG");
        }
        catch (Exception exArg)
        {
            LogToFile($"[UpdateUserPreferredName] ERROR retrieving 'userName' argument: {exArg.Message}", "ERROR");
            LogToFile($"[UpdateUserPreferredName] Stack: {exArg.StackTrace}", "DEBUG");
            LogToFile("[UpdateUserPreferredName] Context: retrieving userName", "ERROR");
            LogToFile("<<< [UpdateUserPreferredName] Exit (failure: exception retrieving userName)", "DEBUG");
            return false;
        }

        try
        {
            LogToFile("[UpdateUserPreferredName] Attempting to retrieve 'rawInput' argument via TryGetArg.", "DEBUG");
            if (!CPH.TryGetArg("rawInput", out preferredName) || string.IsNullOrWhiteSpace(preferredName))
            {
                LogToFile("[UpdateUserPreferredName] WARN: 'rawInput' (preferredName) argument missing or empty.", "WARN");
                LogToFile($"[UpdateUserPreferredName] Context: userName='{userName}', preferredName=null or empty.", "WARN");
                LogToFile("<<< [UpdateUserPreferredName] Exit (failure: missing preferredName)", "DEBUG");
                return false;
            }
            LogToFile($"[UpdateUserPreferredName] Retrieved preferredName='{preferredName}'", "DEBUG");
        }
        catch (Exception exArg)
        {
            LogToFile($"[UpdateUserPreferredName] ERROR retrieving 'rawInput' argument: {exArg.Message}", "ERROR");
            LogToFile($"[UpdateUserPreferredName] Stack: {exArg.StackTrace}", "DEBUG");
            LogToFile($"[UpdateUserPreferredName] Context: userName='{userName}', retrieving preferredName", "ERROR");
            LogToFile("<<< [UpdateUserPreferredName] Exit (failure: exception retrieving preferredName)", "DEBUG");
            return false;
        }

        try
        {
            LogToFile("[UpdateUserPreferredName] Retrieving user_profiles collection from LiteDB.", "DEBUG");
            LogToFile("[UpdateUserPreferredName] Searching for existing profile.", "DEBUG");
            profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                profile = new UserProfile { UserName = userName, PreferredName = preferredName, Pronouns = "" };
                userCollection.Insert(profile);
                LogToFile($"[UpdateUserPreferredName] Created new profile: userName='{userName}', preferredName='{preferredName}'.", "DEBUG");
            }
            else
            {
                string oldPreferred = profile.PreferredName;
                profile.PreferredName = preferredName;
                userCollection.Update(profile);
                LogToFile($"[UpdateUserPreferredName] Updated preferred name for userName='{userName}': '{oldPreferred}' => '{preferredName}'.", "DEBUG");
            }
        }
        catch (Exception exDb)
        {
            LogToFile($"[UpdateUserPreferredName] ERROR updating or inserting profile: {exDb.Message}", "ERROR");
            LogToFile($"[UpdateUserPreferredName] Stack: {exDb.StackTrace}", "DEBUG");
            LogToFile($"[UpdateUserPreferredName] Context: userName='{userName}', preferredName='{preferredName}', profile={profile}", "ERROR");
            LogToFile("<<< [UpdateUserPreferredName] Exit (failure: db error)", "DEBUG");
            return false;
        }

        string message = $"{userName}, your nickname has been set to {preferredName}.";
        try
        {
            LogToFile("[UpdateUserPreferredName] Retrieving 'Post To Chat' global variable.", "DEBUG");
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            LogToFile($"[UpdateUserPreferredName] postToChat={postToChat}", "DEBUG");
        }
        catch (Exception exChat)
        {
            LogToFile($"[UpdateUserPreferredName] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
            postToChat = false;
        }
        if (postToChat)
        {
            try
            {
                LogToFile($"[UpdateUserPreferredName] Sending nickname confirmation message to chat: {message}", "DEBUG");
                CPH.SendMessage(message, true);
                LogToFile($"[UpdateUserPreferredName] Nickname confirmation message sent for userName='{userName}'.", "DEBUG");
            }
            catch (Exception exSend)
            {
                LogToFile($"[UpdateUserPreferredName] WARN: Exception sending chat message: {exSend.Message}", "WARN");
            }
        }
        else
        {
            LogToFile($"[UpdateUserPreferredName] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
        }
        success = true;
        LogToFile($@"<<< [UpdateUserPreferredName] Exit: userName='{userName}', preferredName='{preferredName}', postToChat={postToChat}, success={success}", "DEBUG");
        return success;
    }

    public bool DeleteUserProfile()
    {
        LogToFile(">>> [DeleteUserProfile] Entry: Starting delete/reset of user profile.", "DEBUG");
        string userName = null;
        bool postToChat = false;
        bool profileExisted = false;
        bool profileReset = false;
        bool result = false;
        UserProfile profile = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");

        try
        {
            LogToFile("[DeleteUserProfile] Attempting to retrieve 'userName' argument via TryGetArg.", "DEBUG");
            if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("[DeleteUserProfile] WARN: 'userName' argument missing or empty.", "WARN");
                LogToFile("[DeleteUserProfile] Context: userName=null or empty.", "WARN");
                return false;
            }
            LogToFile($"[DeleteUserProfile] Retrieved userName='{userName}'", "DEBUG");
        }
        catch (Exception exArg)
        {
            LogToFile($"[DeleteUserProfile] ERROR retrieving 'userName' argument: {exArg.Message}", "ERROR");
            LogToFile($"[DeleteUserProfile] Stack: {exArg.StackTrace}", "DEBUG");
            LogToFile("[DeleteUserProfile] Context: retrieving userName", "ERROR");
            return false;
        }

        try
        {
            LogToFile("[DeleteUserProfile] Retrieving user_profiles collection from LiteDB.", "DEBUG");
            LogToFile("[DeleteUserProfile] Searching for existing profile.", "DEBUG");
            profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            profileExisted = (profile != null);
            if (!profileExisted)
            {
                string message = $"{userName}, you don't have a custom nickname set.";
                LogToFile($"[DeleteUserProfile] No profile found for userName='{userName}'.", "DEBUG");

                try
                {
                    LogToFile("[DeleteUserProfile] Retrieving 'Post To Chat' global variable.", "DEBUG");
                    postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                    LogToFile($"[DeleteUserProfile] postToChat={postToChat}", "DEBUG");
                }
                catch (Exception exChat)
                {
                    LogToFile($"[DeleteUserProfile] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
                    LogToFile($"[DeleteUserProfile] Stack: {exChat.StackTrace}", "DEBUG");
                    postToChat = false;
                }
                if (postToChat)
                {
                    try
                    {
                        LogToFile($"[DeleteUserProfile] Sending 'no nickname' message to chat: {message}", "DEBUG");
                        CPH.SendMessage(message, true);
                        LogToFile($"[DeleteUserProfile] 'No nickname' message sent for userName='{userName}'.", "DEBUG");
                    }
                    catch (Exception exSend)
                    {
                        LogToFile($"[DeleteUserProfile] WARN: Exception sending chat message: {exSend.Message}", "WARN");
                        LogToFile($"[DeleteUserProfile] Stack: {exSend.StackTrace}", "DEBUG");
                    }
                }
                else
                {
                    LogToFile($"[DeleteUserProfile] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
                }
                result = true;
                LogToFile($@"<<< [DeleteUserProfile] Exit: userName='{userName}', profileExisted={profileExisted}, postToChat={postToChat}", "DEBUG");
                return result;
            }

            string oldPreferredName = profile.PreferredName;
            profile.PreferredName = userName;
            userCollection.Update(profile);
            profileReset = true;
            LogToFile($"[DeleteUserProfile] Reset preferred name for userName='{userName}' from '{oldPreferredName}' to '{userName}'.", "DEBUG");
        }
        catch (Exception exDb)
        {
            LogToFile($"[DeleteUserProfile] ERROR updating or finding profile: {exDb.Message}", "ERROR");
            LogToFile($"[DeleteUserProfile] Stack: {exDb.StackTrace}", "DEBUG");
            LogToFile($"[DeleteUserProfile] Context: userName='{userName}', profileExisted={profileExisted}, profileReset={profileReset}", "ERROR");

            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            }
            catch (Exception exChat)
            {
                LogToFile($"[DeleteUserProfile] WARN: Failed to retrieve 'Post To Chat' after DB error: {exChat.Message}", "WARN");
                LogToFile($"[DeleteUserProfile] Stack: {exChat.StackTrace}", "DEBUG");
                postToChat = false;
            }
            string message = "An error occurred while resetting your nickname.";
            if (postToChat)
            {
                try
                {
                    CPH.SendMessage(message, true);
                }
                catch (Exception exSend)
                {
                    LogToFile($"[DeleteUserProfile] WARN: Exception sending error chat message: {exSend.Message}", "WARN");
                    LogToFile($"[DeleteUserProfile] Stack: {exSend.StackTrace}", "DEBUG");
                }
            }
            else
            {
                LogToFile($"[DeleteUserProfile] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
            }
            LogToFile($@"<<< [DeleteUserProfile] Exit: userName='{userName}', profileExisted={profileExisted}, postToChat={postToChat}", "DEBUG");
            return false;
        }

        string resetMessage = $"{userName}, your nickname has been reset to your username.";
        try
        {
            LogToFile("[DeleteUserProfile] Retrieving 'Post To Chat' global variable.", "DEBUG");
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            LogToFile($"[DeleteUserProfile] postToChat={postToChat}", "DEBUG");
        }
        catch (Exception exChat)
        {
            LogToFile($"[DeleteUserProfile] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
            LogToFile($"[DeleteUserProfile] Stack: {exChat.StackTrace}", "DEBUG");
            postToChat = false;
        }
        if (postToChat)
        {
            try
            {
                LogToFile($"[DeleteUserProfile] Sending nickname reset message to chat: {resetMessage}", "DEBUG");
                CPH.SendMessage(resetMessage, true);
                LogToFile($"[DeleteUserProfile] Nickname reset message sent for userName='{userName}'.", "DEBUG");
            }
            catch (Exception exSend)
            {
                LogToFile($"[DeleteUserProfile] WARN: Exception sending chat message: {exSend.Message}", "WARN");
                LogToFile($"[DeleteUserProfile] Stack: {exSend.StackTrace}", "DEBUG");
            }
        }
        else
        {
            LogToFile($"[DeleteUserProfile] [Skipped Chat Output] Post To Chat disabled. Message: {resetMessage}", "WARN");
        }
        result = true;
        LogToFile($@"<<< [DeleteUserProfile] Exit: userName='{userName}', profileExisted={profileExisted}, postToChat={postToChat}", "DEBUG");
        return result;
    }

    public bool ShowCurrentUserProfile()
    {
        LogToFile(">>> [ShowCurrentUserProfile] Entry", "DEBUG");
        string userName = null;
        bool postToChat = false;
        UserProfile profile = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        string displayName = null;
        string message = null;
        bool profileFound = false;
        try
        {
            try
            {
                LogToFile("[ShowCurrentUserProfile] Attempting to retrieve 'userName' argument via TryGetArg.", "DEBUG");
                if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
                {
                    LogToFile("[ShowCurrentUserProfile] WARN: 'userName' argument missing or empty.", "WARN");
                    LogToFile("[ShowCurrentUserProfile] Context: userName=null or empty.", "WARN");
                    LogToFile("<<< [ShowCurrentUserProfile] Exit (failure: missing userName)", "DEBUG");
                    return false;
                }
                LogToFile($"[ShowCurrentUserProfile] Retrieved userName='{userName}'", "DEBUG");
            }
            catch (Exception exArg)
            {
                LogToFile($"[ShowCurrentUserProfile] ERROR retrieving 'userName' argument: {exArg.Message}", "ERROR");
                LogToFile($"[ShowCurrentUserProfile] Stack: {exArg.StackTrace}", "DEBUG");
                LogToFile("[ShowCurrentUserProfile] Context: retrieving userName", "ERROR");
                LogToFile("<<< [ShowCurrentUserProfile] Exit (failure: exception retrieving userName)", "DEBUG");
                return false;
            }

            try
            {
                LogToFile("[ShowCurrentUserProfile] Retrieving user_profiles collection from LiteDB.", "DEBUG");
                LogToFile("[ShowCurrentUserProfile] Searching for existing profile.", "DEBUG");
                profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                profileFound = (profile != null);
                if (!profileFound)
                {
                    LogToFile($"[ShowCurrentUserProfile] WARN: No profile found for userName='{userName}'.", "WARN");
                }
                else
                {
                    LogToFile($"[ShowCurrentUserProfile] Profile found for userName='{userName}'.", "DEBUG");
                }
            }
            catch (Exception exDb)
            {
                LogToFile($"[ShowCurrentUserProfile] ERROR: Exception retrieving profile: {exDb.Message}", "ERROR");
                LogToFile($"[ShowCurrentUserProfile] Stack: {exDb.StackTrace}", "DEBUG");
                LogToFile($"[ShowCurrentUserProfile] Context: userName='{userName}'", "ERROR");
                LogToFile("<<< [ShowCurrentUserProfile] Exit (failure: exception retrieving profile)", "DEBUG");
                return false;
            }

            if (profileFound)
            {
                displayName = profile.PreferredName;
                message = string.IsNullOrWhiteSpace(profile?.Pronouns)
                    ? $"Your current username is set to {displayName}"
                    : $"Your current username is set to {displayName} ({profile.Pronouns})";
            }
            else
            {
                displayName = userName;
                message = $"No profile found for {userName}.";
            }
            LogToFile($"[ShowCurrentUserProfile] Prepared message: {message}", "DEBUG");

            try
            {
                LogToFile("[ShowCurrentUserProfile] Retrieving 'Post To Chat' global variable.", "DEBUG");
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[ShowCurrentUserProfile] postToChat={postToChat}", "DEBUG");
            }
            catch (Exception exChat)
            {
                LogToFile($"[ShowCurrentUserProfile] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
                postToChat = false;
            }

            if (postToChat)
            {
                try
                {
                    LogToFile($"[ShowCurrentUserProfile] Sending profile message to chat: {message}", "DEBUG");
                    CPH.SendMessage(message, true);
                    if (profileFound)
                        LogToFile($"[ShowCurrentUserProfile] Profile displayed for userName='{userName}': {message}", "DEBUG");
                    else
                        LogToFile($"[ShowCurrentUserProfile] No profile to display for userName='{userName}'. Message sent: {message}", "DEBUG");
                }
                catch (Exception exSend)
                {
                    LogToFile($"[ShowCurrentUserProfile] WARN: Exception sending chat message: {exSend.Message}", "WARN");
                }
            }
            else
            {
                LogToFile($"[ShowCurrentUserProfile] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
            }

            LogToFile($@"<<< [ShowCurrentUserProfile] Exit: userName='{userName}', postToChat={postToChat}, profileFound={profileFound}, message='{message}'", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[ShowCurrentUserProfile] ERROR: Exception: {ex.Message}", "ERROR");
            LogToFile($"[ShowCurrentUserProfile] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[ShowCurrentUserProfile] Context: userName='{userName ?? "null"}', postToChat={postToChat}, profileFound={profileFound}", "ERROR");
            LogToFile("<<< [ShowCurrentUserProfile] Exit (failure: exception thrown)", "DEBUG");
            return false;
        }
    }

    public bool ForgetThis()
    {
        LogToFile(">>> [ForgetThis] Entry: Starting forget keyword operation.", "DEBUG");
        bool postToChat = false;
        bool success = false;
        string rawInput = null;
        try
        {
            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[ForgetThis] Retrieved Post To Chat: {postToChat}", "DEBUG");
            }
            catch (Exception exChat)
            {
                LogToFile($"[ForgetThis] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
                postToChat = false;
            }

            if (!CPH.TryGetArg("rawInput", out rawInput) || string.IsNullOrWhiteSpace(rawInput))
            {
                LogToFile("[ForgetThis] ERROR: 'rawInput' argument missing or empty.", "ERROR");
                if (postToChat)
                {
                    try
                    {
                        CPH.SendMessage("You need to tell me what keyword to forget.", true);
                        LogToFile("[ForgetThis] Chat output: Missing keyword input.", "DEBUG");
                    }
                    catch (Exception exSend)
                    {
                        LogToFile($"[ForgetThis] ERROR: Exception sending missing keyword chat message: {exSend.Message}", "ERROR");
                    }
                }
                else
                {
                    LogToFile("[ForgetThis] [Skipped Chat Output] Post To Chat disabled. Message: You need to tell me what keyword to forget.", "WARN");
                }
                LogToFile("<<< [ForgetThis] Exit (failure: missing keyword input)", "DEBUG");
                success = false;
                return false;
            }

            // Extract the keyword (first word)
            var keyword = rawInput.Trim().Split(' ', '\t', '\r', '\n').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                LogToFile("[ForgetThis] ERROR: Unable to extract keyword from input.", "ERROR");
                if (postToChat)
                {
                    try
                    {
                        CPH.SendMessage("You need to tell me what keyword to forget.", true);
                        LogToFile("[ForgetThis] Chat output: Unable to extract keyword.", "DEBUG");
                    }
                    catch (Exception exSend)
                    {
                        LogToFile($"[ForgetThis] ERROR: Exception sending missing keyword chat message: {exSend.Message}", "ERROR");
                    }
                }
                else
                {
                    LogToFile("[ForgetThis] [Skipped Chat Output] Post To Chat disabled. Message: You need to tell me what keyword to forget.", "WARN");
                }
                LogToFile("<<< [ForgetThis] Exit (failure: empty keyword)", "DEBUG");
                success = false;
                return false;
            }

            LogToFile($"[ForgetThis] Parsed keyword to forget: '{keyword}'", "DEBUG");

            var col = _db.GetCollection<BsonDocument>("keywords");
            BsonDocument foundDoc = null;
            try
            {
                foundDoc = col.FindAll().FirstOrDefault(doc =>
                    string.Equals(doc["Keyword"]?.AsString, keyword, StringComparison.OrdinalIgnoreCase));
                if (foundDoc != null)
                {
                    col.Delete(foundDoc["_id"]);
                    LogToFile($"[ForgetThis] DEBUG: Keyword '{keyword}' deleted.", "DEBUG");
                    if (postToChat)
                    {
                        try
                        {
                            CPH.SendMessage($"Forgot keyword '{keyword}'", true);
                        }
                        catch (Exception exSend)
                        {
                            LogToFile($"[ForgetThis] WARN: Exception sending chat confirmation: {exSend.Message}", "WARN");
                        }
                    }
                }
                else
                {
                    LogToFile($"[ForgetThis] WARN: No keyword found for '{keyword}'.", "WARN");
                    if (postToChat)
                    {
                        try
                        {
                            CPH.SendMessage($"No keyword found for '{keyword}'", true);
                        }
                        catch (Exception exSend)
                        {
                            LogToFile($"[ForgetThis] WARN: Exception sending chat not-found message: {exSend.Message}", "WARN");
                        }
                    }
                }
            }
            catch (Exception exDb)
            {
                LogToFile($"[ForgetThis] ERROR: Exception during keyword deletion: {exDb.Message}", "ERROR");
                if (postToChat)
                {
                    try
                    {
                        CPH.SendMessage("An error occurred while attempting to forget that keyword. Please try again later.", true);
                    }
                    catch (Exception exSend)
                    {
                        LogToFile($"[ForgetThis] ERROR: Exception sending error chat message: {exSend.Message}", "ERROR");
                    }
                }
                else
                {
                    LogToFile("[ForgetThis] [Skipped Chat Output] Post To Chat disabled. Message: An error occurred while attempting to forget that keyword. Please try again later.", "WARN");
                }
                LogToFile("<<< [ForgetThis] Exit (failure: exception during DB operation)", "DEBUG");
                success = false;
                return false;
            }

            success = true;
            LogToFile($@"<<< [ForgetThis] Exit: keyword='{keyword}', postToChat={postToChat}, success={success}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[ForgetThis] ERROR: Unexpected error: {ex.Message}", "ERROR");
            LogToFile($"[ForgetThis] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[ForgetThis] Context: postToChat={postToChat}", "ERROR");
            if (postToChat)
            {
                try
                {
                    CPH.SendMessage("An error occurred while attempting to forget that keyword. Please try again later.", true);
                }
                catch (Exception exSend)
                {
                    LogToFile($"[ForgetThis] ERROR: Exception sending error chat message: {exSend.Message}", "ERROR");
                    LogToFile($"[ForgetThis] Stack: {exSend.StackTrace}", "DEBUG");
                }
            }
            else
            {
                LogToFile("[ForgetThis] [Skipped Chat Output] Post To Chat disabled. Message: An error occurred while attempting to forget that keyword. Please try again later.", "WARN");
            }
            LogToFile("<<< [ForgetThis] Exit (failure: unexpected error)", "DEBUG");
            return false;
        }
    }

    public bool ForgetThisAboutMe()
    {
        LogToFile(">>> [ForgetThisAboutMe] Entry: Starting forget-all-knowledge operation.", "DEBUG");
        string userName = null;
        bool postToChat = false;
        int knowledgeCount = -1;
        UserProfile profile = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        bool success = false;

        try
        {
            LogToFile("[ForgetThisAboutMe] Attempting to retrieve 'userName' argument via TryGetArg.", "DEBUG");
            if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("[ForgetThisAboutMe] WARN: 'userName' argument missing or empty.", "WARN");
                LogToFile("[ForgetThisAboutMe] Context: userName=null or empty.", "WARN");
                LogToFile($@"<<< [ForgetThisAboutMe] Exit: userName='null', postToChat={postToChat}, knowledgeCount={knowledgeCount}, success=false", "DEBUG");
                return false;
            }
            LogToFile($"[ForgetThisAboutMe] Successfully retrieved userName='{userName}'", "DEBUG");
        }
        catch (Exception exArg)
        {
            LogToFile($"[ForgetThisAboutMe] ERROR: Exception retrieving 'userName' argument: {exArg.Message}", "ERROR");
            LogToFile($"[ForgetThisAboutMe] Stack: {exArg.StackTrace}", "DEBUG");
            LogToFile("[ForgetThisAboutMe] Context: retrieving userName", "ERROR");
            LogToFile($@"<<< [ForgetThisAboutMe] Exit: userName='null', postToChat={postToChat}, knowledgeCount={knowledgeCount}, success=false", "DEBUG");
            return false;
        }

        try
        {
            LogToFile("[ForgetThisAboutMe] Retrieving user_profiles collection from LiteDB.", "DEBUG");
            LogToFile("[ForgetThisAboutMe] Searching for existing profile.", "DEBUG");
            profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                LogToFile($"[ForgetThisAboutMe] No profile found for userName='{userName}'.", "DEBUG");
                knowledgeCount = 0;
            }
            else if (profile.Knowledge == null || profile.Knowledge.Count == 0)
            {
                knowledgeCount = 0;
                LogToFile($"[ForgetThisAboutMe] No knowledge found for userName='{userName}'.", "DEBUG");
            }
            else
            {
                knowledgeCount = profile.Knowledge.Count;
                LogToFile($"[ForgetThisAboutMe] Found {knowledgeCount} knowledge items for userName='{userName}'.", "DEBUG");
            }
        }
        catch (Exception exDb)
        {
            LogToFile($"[ForgetThisAboutMe] ERROR: Exception accessing user_profiles: {exDb.Message}", "ERROR");
            LogToFile($"[ForgetThisAboutMe] Stack: {exDb.StackTrace}", "DEBUG");
            LogToFile($"[ForgetThisAboutMe] Context: userName='{userName}'", "ERROR");
            LogToFile($@"<<< [ForgetThisAboutMe] Exit: userName='{userName}', postToChat={postToChat}, knowledgeCount={knowledgeCount}, success=false", "DEBUG");
            return false;
        }

        try
        {
            LogToFile("[ForgetThisAboutMe] Retrieving 'Post To Chat' global variable.", "DEBUG");
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            LogToFile($"[ForgetThisAboutMe] postToChat={postToChat}", "DEBUG");
        }
        catch (Exception exChat)
        {
            LogToFile($"[ForgetThisAboutMe] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
            postToChat = false;
        }

        if (profile == null || knowledgeCount == 0)
        {
            LogToFile($"[ForgetThisAboutMe] No knowledge to clear for userName='{userName}'.", "DEBUG");
            try
            {
                if (postToChat)
                {
                    LogToFile($"[ForgetThisAboutMe] Sending 'no knowledge' message to chat.", "DEBUG");
                    CPH.SendMessage($"{userName}, I don't have any knowledge stored for you.", true);
                    LogToFile($"[ForgetThisAboutMe] Confirmation message sent: no knowledge for '{userName}'.", "DEBUG");
                }
                else
                {
                    LogToFile($"[ForgetThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: {userName}, I don't have any knowledge stored for you.", "WARN");
                }
            }
            catch (Exception exMsg)
            {
                LogToFile($"[ForgetThisAboutMe] ERROR: Exception sending 'no knowledge' chat message: {exMsg.Message}", "ERROR");
            }
            success = true;
            LogToFile($@"<<< [ForgetThisAboutMe] Exit: userName='{userName}', postToChat={postToChat}, knowledgeCount=0, success={success}", "DEBUG");
            return true;
        }

        try
        {
            LogToFile($"[ForgetThisAboutMe] Clearing all {knowledgeCount} knowledge items for userName='{userName}'.", "DEBUG");
            if (profile.Knowledge == null)
                profile.Knowledge = new List<string>();
            profile.Knowledge.Clear();
            userCollection.Update(profile);
            LogToFile($"[ForgetThisAboutMe] Cleared all knowledge for userName='{userName}'.", "DEBUG");
        }
        catch (Exception exUpdate)
        {
            LogToFile($"[ForgetThisAboutMe] ERROR: Exception clearing/updating knowledge: {exUpdate.Message}", "ERROR");
            LogToFile($"[ForgetThisAboutMe] Stack: {exUpdate.StackTrace}", "DEBUG");
            LogToFile($"[ForgetThisAboutMe] Context: userName='{userName}', knowledgeCount={knowledgeCount}", "ERROR");
            try
            {
                if (postToChat)
                    CPH.SendMessage("An error occurred while attempting to clear your knowledge. Please try again later.", true);
                else
                    LogToFile("[ForgetThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: An error occurred while attempting to clear your knowledge. Please try again later.", "WARN");
            }
            catch (Exception exSend)
            {
                LogToFile($"[ForgetThisAboutMe] ERROR: Exception sending error chat message: {exSend.Message}", "ERROR");
            }
            LogToFile($@"<<< [ForgetThisAboutMe] Exit: userName='{userName}', postToChat={postToChat}, knowledgeCount={knowledgeCount}, success=false", "DEBUG");
            return false;
        }

        try
        {
            if (postToChat)
            {
                LogToFile($"[ForgetThisAboutMe] Sending 'knowledge cleared' message to chat.", "DEBUG");
                CPH.SendMessage($"{userName}, all your knowledge has been cleared.", true);
                LogToFile($"[ForgetThisAboutMe] Confirmation message sent: all knowledge cleared for '{userName}'.", "DEBUG");
            }
            else
            {
                LogToFile($"[ForgetThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: {userName}, all your knowledge has been cleared.", "WARN");
            }
        }
        catch (Exception exSend)
        {
            LogToFile($"[ForgetThisAboutMe] ERROR: Exception sending 'knowledge cleared' chat message: {exSend.Message}", "ERROR");
        }

        success = true;
        LogToFile($@"<<< [ForgetThisAboutMe] Exit: userName='{userName}', postToChat={postToChat}, knowledgeCount={knowledgeCount}, success={success}", "DEBUG");
        return true;
    }

    public bool GetMemory()
    {
        LogToFile(">>> [GetMemory] Entry: Starting retrieval of user memory.", "DEBUG");
        string userName = null;
        bool postToChat = false;
        int memoryCount = -1;
        UserProfile profile = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        string combinedMemory = null;
        string message = null;
        bool memoryFound = false;
        try
        {
            try
            {
                LogToFile("[GetMemory] Attempting to retrieve 'userName' argument via TryGetArg.", "DEBUG");
                if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
                {
                    LogToFile("[GetMemory] WARN: 'userName' argument missing or empty.", "WARN");
                    LogToFile("[GetMemory] Context: userName=null or empty.", "WARN");
                    LogToFile($@"<<< [GetMemory] Exit: userName='null', postToChat={postToChat}, memoryFound=false, success=false", "DEBUG");
                    return false;
                }
                LogToFile($"[GetMemory] Retrieved userName='{userName}'", "DEBUG");
            }
            catch (Exception exArg)
            {
                LogToFile($"[GetMemory] ERROR retrieving 'userName' argument: {exArg.Message}", "ERROR");
                LogToFile("[GetMemory] Context: retrieving userName", "ERROR");
                LogToFile($@"<<< [GetMemory] Exit: userName='null', postToChat={postToChat}, memoryFound=false, success=false", "DEBUG");
                return false;
            }

            try
            {
                LogToFile("[GetMemory] Retrieving user_profiles collection from LiteDB.", "DEBUG");
                LogToFile("[GetMemory] Searching for existing profile.", "DEBUG");
                profile = userCollection.FindOne(x => x.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                if (profile == null || profile.Knowledge == null || profile.Knowledge.Count == 0)
                {
                    memoryCount = 0;
                    memoryFound = false;
                    LogToFile($"[GetMemory] No profile or memories found for userName='{userName}'.", "DEBUG");
                }
                else
                {
                    memoryCount = profile.Knowledge.Count;
                    memoryFound = true;
                    LogToFile($"[GetMemory] Found {memoryCount} memories for userName='{userName}'.", "DEBUG");
                }
            }
            catch (Exception exDb)
            {
                LogToFile($"[GetMemory] ERROR: Exception accessing user_profiles: {exDb.Message}", "ERROR");
                LogToFile($"[GetMemory] Context: userName='{userName}'", "ERROR");
                LogToFile($@"<<< [GetMemory] Exit: userName='{userName}', postToChat={postToChat}, memoryFound={memoryFound}, success=false", "DEBUG");
                return false;
            }

            if (memoryCount == 0)
            {
                message = $"I don’t have any saved memories for {userName}.";
                LogToFile($"[GetMemory] Prepared message: {message}", "DEBUG");
            }
            else
            {
                combinedMemory = string.Join(", ", profile.Knowledge);
                message = $"Something I remember about {userName} is: {combinedMemory}.";
                LogToFile($"[GetMemory] Prepared message: {message}", "DEBUG");
            }

            try
            {
                LogToFile("[GetMemory] Retrieving 'Post To Chat' global variable.", "DEBUG");
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[GetMemory] postToChat={postToChat}", "DEBUG");
            }
            catch (Exception exChat)
            {
                LogToFile($"[GetMemory] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
                postToChat = false;
            }

            try
            {
                if (postToChat)
                {
                    LogToFile($"[GetMemory] Sending memory message to chat: {message}", "DEBUG");
                    CPH.SendMessage(message, true);
                    if (memoryCount == 0)
                        LogToFile($"[GetMemory] No memory found for userName='{userName}'. Message sent: {message}", "DEBUG");
                    else
                        LogToFile($"[GetMemory] Memory output sent for userName='{userName}': {combinedMemory}", "DEBUG");
                }
                else
                {
                    LogToFile($"[GetMemory] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
                }
            }
            catch (Exception exSend)
            {
                LogToFile($"[GetMemory] WARN: Exception sending chat message: {exSend.Message}", "WARN");
            }

            LogToFile($@"<<< [GetMemory] Exit: userName='{userName}', postToChat={postToChat}, memoryFound={memoryFound}, success=true", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[GetMemory] ERROR: Unexpected error: {ex.Message}", "ERROR");
            LogToFile($"[GetMemory] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GetMemory] Context: userName='{userName ?? "null"}', postToChat={postToChat}, memoryFound={memoryFound}", "ERROR");
            string errMsg = "Sorry, something went wrong while retrieving your memory.";
            try
            {
                if (postToChat)
                    CPH.SendMessage(errMsg, true);
                else
                    LogToFile($"[GetMemory] [Skipped Chat Output] Post To Chat disabled. Message: {errMsg}", "WARN");
            }
            catch (Exception exSend)
            {
                LogToFile($"[GetMemory] ERROR: Exception sending error chat message: {exSend.Message}", "ERROR");
                LogToFile($"[GetMemory] Stack: {exSend.StackTrace}", "DEBUG");
            }
            LogToFile($@"<<< [GetMemory] Exit: userName='{userName}', postToChat={postToChat}, memoryFound={memoryFound}, success=false", "DEBUG");
            return false;
        }
    }

    public bool SaveMessage()
    {
        LogToFile(">>> [SaveMessage] Entry: Begin SaveMessage operation", "DEBUG");
        string msg = null;
        string userName = null;
        string displayName = null;
        string ignoreNamesString = null;
        List<string> ignoreNamesList = null;
        string messageContent = null;
        int ignoreNamesCount = 0;
        int chatLogCount = -1;
        bool success = false;

        try
        {
            try
            {
                LogToFile("[SaveMessage] DEBUG: TryGetArg('rawInput')", "DEBUG");
                if (!CPH.TryGetArg("rawInput", out msg) || string.IsNullOrWhiteSpace(msg))
                {
                    LogToFile("[SaveMessage] WARN: R=Save message, A=Retrieve arg, P=msg=null, I=Expect valid input, D=Failed.", "WARN");
                    LogToFile("<<< [SaveMessage] Exit: Missing or empty 'rawInput' (fail)", "DEBUG");
                    return false;
                }
                LogToFile($"[SaveMessage] DEBUG: Retrieved 'rawInput': '{msg}'", "DEBUG");
            }
            catch (Exception exArg)
            {
                LogToFile($"[SaveMessage] ERROR: R=Save message, A=Retrieve arg, P=msg=null, I=Expect valid input, D=Exception: {exArg.Message}", "ERROR");
                LogToFile($"[SaveMessage] Stack: {exArg.StackTrace}", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Exception retrieving 'rawInput'", "DEBUG");
                return false;
            }
            try
            {
                LogToFile("[SaveMessage] DEBUG: TryGetArg('userName')", "DEBUG");
                if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
                {
                    LogToFile("[SaveMessage] WARN: R=Save message, A=Retrieve arg, P=userName=null, I=Expect valid username, D=Failed.", "WARN");
                    LogToFile("<<< [SaveMessage] Exit: Missing or empty 'userName' (fail)", "DEBUG");
                    return false;
                }
                LogToFile($"[SaveMessage] DEBUG: Retrieved 'userName': '{userName}'", "DEBUG");
            }
            catch (Exception exArg)
            {
                LogToFile($"[SaveMessage] ERROR: R=Save message, A=Retrieve arg, P=userName=null, I=Expect valid username, D=Exception: {exArg.Message}", "ERROR");
                LogToFile($"[SaveMessage] Stack: {exArg.StackTrace}", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Exception retrieving 'userName'", "DEBUG");
                return false;
            }
            LogToFile($"[SaveMessage] DEBUG: Args OK. userName='{userName}', msg='{msg}'", "DEBUG");

            // Prevent saving chat commands to the queue
            if (msg.StartsWith("!", StringComparison.OrdinalIgnoreCase))
            {
                LogToFile($"[SaveMessage] DEBUG: Message '{msg}' identified as command — skipping queue storage.", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Skipped command message", "DEBUG");
                return false;
            }

            UserProfile profile = null;
            try
            {
                profile = GetOrCreateUserProfile(userName);
                displayName = profile?.PreferredName ?? userName;
                if (!string.IsNullOrWhiteSpace(profile?.Pronouns))
                    displayName += $" {profile.Pronouns}";
                LogToFile($"[SaveMessage] DEBUG: Computed displayName='{displayName}' for userName='{userName}'", "DEBUG");
            }
            catch (Exception exProf)
            {
                LogToFile($"[SaveMessage] ERROR: R=Save message, A=GetOrCreateUserProfile, P=userName='{userName}', I=Profile ready, D=Failed: {exProf.Message}", "ERROR");
                LogToFile($"[SaveMessage] Stack: {exProf.StackTrace}", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Exception retrieving/creating user profile", "DEBUG");
                return false;
            }

            try
            {
                LogToFile("[SaveMessage] DEBUG: Retrieving 'Ignore Bot Usernames' global var", "DEBUG");
                ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
                if (string.IsNullOrWhiteSpace(ignoreNamesString))
                {
                    LogToFile("[SaveMessage] WARN: R=Save message, A=Fetch ignore list, P=string=null, I=Expect valid list, D=Fallback.", "WARN");
                    LogToFile("<<< [SaveMessage] Exit: Ignore list missing/empty (fail)", "DEBUG");
                    return false;
                }
                ignoreNamesList = ignoreNamesString.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                ignoreNamesCount = ignoreNamesList.Count;
                if (ignoreNamesList.Contains(userName, StringComparer.OrdinalIgnoreCase))
                {
                    LogToFile($"[SaveMessage] DEBUG: R=Save message, A=Check ignore list, P=userName='{userName}', I=Skip ignored, D=Skipped.", "DEBUG");
                    LogToFile("<<< [SaveMessage] Exit: userName in ignore list (skip)", "DEBUG");
                    return false;
                }
                LogToFile($"[SaveMessage] DEBUG: Ignore list count={ignoreNamesCount}", "DEBUG");
            }
            catch (Exception exIgnore)
            {
                LogToFile($"[SaveMessage] ERROR: R=Save message, A=Process ignore list, P=userName='{userName}', I=Valid list, D=Failed: {exIgnore.Message}", "ERROR");
                LogToFile($"[SaveMessage] Stack: {exIgnore.StackTrace}", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Exception retrieving ignore list", "DEBUG");
                return false;
            }

            try
            {
                if (ChatLog == null)
                {
                    ChatLog = new Queue<chatMessage>();
                    LogToFile("[SaveMessage] DEBUG: ChatLog initialized (was null)", "DEBUG");
                }
                chatLogCount = ChatLog.Count;
            }
            catch (Exception exChatLog)
            {
                LogToFile($"[SaveMessage] ERROR: R=Save message, A=Access ChatLog, P=userName='{userName}', I=Queue operational, D=Failed: {exChatLog.Message}", "ERROR");
                LogToFile($"[SaveMessage] Stack: {exChatLog.StackTrace}", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Exception accessing ChatLog", "DEBUG");
                return false;
            }

            try
            {
                messageContent = $"{displayName} says: {msg}";
                LogToFile($"[SaveMessage] DEBUG: Constructed message: '{messageContent}'", "DEBUG");
                chatMessage chatMsg = new chatMessage
                {
                    role = "user",
                    content = messageContent
                };
                QueueMessage(chatMsg);
            }
            catch (Exception exQueue)
            {
                LogToFile($"[SaveMessage] ERROR: R=Save message, A=Queue message, P=displayName='{displayName}', I=Add to ChatLog, D=Failed: {exQueue.Message}", "ERROR");
                LogToFile($"[SaveMessage] Stack: {exQueue.StackTrace}", "DEBUG");
                LogToFile("<<< [SaveMessage] Exit: Exception queuing message", "DEBUG");
                return false;
            }

            success = true;
            LogToFile($"Saved message from {userName}: {msg}", "INFO");
            LogToFile($"<<< [SaveMessage] Exit: userName='{userName}', msg='{msg}', chatLogCount={ChatLog?.Count ?? -1}, success={success}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[SaveMessage] ERROR: R=Save message, A=Process pipeline, P=userName='{userName ?? "null"}', I=Persist message, D=Exception: {ex.Message}", "ERROR");
            LogToFile($"[SaveMessage] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("<<< [SaveMessage] Exit: success=false (fatal exception)", "DEBUG");
            return false;
        }
    }

    public bool ClearChatHistory()
    {
        LogToFile(">>> [ClearChatHistory] Entry: Attempting to clear chat history.", "DEBUG");
        bool postToChat = false;
        bool success = false;
        string message = null;
        try
        {
            if (ChatLog == null)
            {
                LogToFile("[ClearChatHistory] WARN: ChatLog is not initialized and cannot be cleared.", "WARN");
                message = "Chat history is already empty.";

                try
                {
                    LogToFile("[ClearChatHistory] Retrieving 'Post To Chat' global variable.", "DEBUG");
                    postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                    LogToFile($"[ClearChatHistory] postToChat={postToChat}", "DEBUG");
                }
                catch (Exception exChat)
                {
                    LogToFile($"[ClearChatHistory] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
                    postToChat = false;
                }

                if (postToChat)
                {
                    try
                    {
                        CPH.SendMessage(message, true);
                        LogToFile($"[ClearChatHistory] User notified chat history is empty via chat.", "DEBUG");
                    }
                    catch (Exception exSend)
                    {
                        LogToFile($"[ClearChatHistory] WARN: Exception sending chat message: {exSend.Message}", "WARN");
                    }
                }
                else
                {
                    LogToFile($"[ClearChatHistory] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
                }

                LogToFile($@"<<< [ClearChatHistory] Exit: success=false, postToChat={postToChat}, message='{message}'", "DEBUG");
                return false;
            }

            try
            {
                ChatLog.Clear();
                LogToFile("[ClearChatHistory] Chat history has been successfully cleared.", "INFO");
                message = "Chat history has been cleared.";
            }
            catch (Exception exClear)
            {
                LogToFile($"[ClearChatHistory] ERROR: Exception clearing ChatLog: {exClear.Message}", "ERROR");
                LogToFile($"[ClearChatHistory] Stack: {exClear.StackTrace}", "DEBUG");
                LogToFile($@"<<< [ClearChatHistory] Exit: success=false, postToChat={postToChat}, message='(exception during clear)'", "DEBUG");

                try
                {
                    LogToFile("[ClearChatHistory] Retrieving 'Post To Chat' global variable (exception path).", "DEBUG");
                    postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                }
                catch (Exception exChat)
                {
                    LogToFile($"[ClearChatHistory] WARN: Failed to retrieve 'Post To Chat' in exception path: {exChat.Message}", "WARN");
                    postToChat = false;
                }

                string errMsg = "I was unable to clear the chat history. Please check the log file for more details.";
                if (postToChat)
                {
                    try
                    {
                        CPH.SendMessage(errMsg, true);
                        LogToFile("[ClearChatHistory] User notified of chat clear failure via chat.", "DEBUG");
                    }
                    catch (Exception exSend)
                    {
                        LogToFile($"[ClearChatHistory] WARN: Exception sending error chat message: {exSend.Message}", "WARN");
                    }
                }
                else
                {
                    LogToFile($"[ClearChatHistory] [Skipped Chat Output] Post To Chat disabled. Message: {errMsg}", "WARN");
                }
                return false;
            }

            try
            {
                LogToFile("[ClearChatHistory] Retrieving 'Post To Chat' global variable.", "DEBUG");
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[ClearChatHistory] postToChat={postToChat}", "DEBUG");
            }
            catch (Exception exChat)
            {
                LogToFile($"[ClearChatHistory] WARN: Failed to retrieve 'Post To Chat': {exChat.Message}", "WARN");
                postToChat = false;
            }

            if (postToChat)
            {
                try
                {
                    CPH.SendMessage(message, true);
                    LogToFile("[ClearChatHistory] User notified chat history has been cleared via chat.", "DEBUG");
                }
                catch (Exception exSend)
                {
                    LogToFile($"[ClearChatHistory] WARN: Exception sending chat message: {exSend.Message}", "WARN");
                }
            }
            else
            {
                LogToFile($"[ClearChatHistory] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
            }

            success = true;
            LogToFile($@"<<< [ClearChatHistory] Exit: success={success}, postToChat={postToChat}, message='{message}'", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[ClearChatHistory] ERROR: Fatal exception: {ex.Message}", "ERROR");
            LogToFile($"[ClearChatHistory] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[ClearChatHistory] Context: postToChat={postToChat}, message='{message ?? "null"}'", "ERROR");

            string errMsg = "I was unable to clear the chat history. Please check the log file for more details.";
            try
            {
                LogToFile("[ClearChatHistory] Retrieving 'Post To Chat' global variable (fatal exception path).", "DEBUG");
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            }
            catch (Exception exChat)
            {
                LogToFile($"[ClearChatHistory] WARN: Failed to retrieve 'Post To Chat' in fatal exception path: {exChat.Message}", "WARN");
                postToChat = false;
            }

            if (postToChat)
            {
                try
                {
                    CPH.SendMessage(errMsg, true);
                    LogToFile("[ClearChatHistory] User notified of chat clear failure via chat (fatal exception).", "DEBUG");
                }
                catch (Exception exSend)
                {
                    LogToFile($"[ClearChatHistory] WARN: Exception sending error chat message: {exSend.Message}", "WARN");
                }
            }
            else
            {
                LogToFile($"[ClearChatHistory] [Skipped Chat Output] Post To Chat disabled. Message: {errMsg}", "WARN");
            }
            LogToFile($@"<<< [ClearChatHistory] Exit: success=false, postToChat={postToChat}, message='{errMsg}'", "DEBUG");
            return false;
        }
    }

    public bool PerformModeration()
    {
        LogToFile(">>> [PerformModeration] Entry: Begin Moderation Evaluation", "DEBUG");
        bool success = false;
        int inputLength = 0;
        int flaggedCount = 0;
        string input = null;
        List<string> flaggedCategories = new List<string>();
        ModerationResponse response = null;
        bool moderationEnabled = false;
        bool rebukeEnabled = false;
        bool postToChat = false;
        try
        {

            moderationEnabled = CPH.GetGlobalVar<bool>("moderation_enabled", true);
            rebukeEnabled = CPH.GetGlobalVar<bool>("moderation_rebuke_enabled", true);
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            LogToFile($"[PerformModeration] Settings: postToChat={postToChat}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}", "DEBUG");

            if (!CPH.TryGetArg("rawInput", out input) || string.IsNullOrWhiteSpace(input))
            {
                LogToFile("[PerformModeration] ERROR: Missing or invalid rawInput.", "ERROR");
                if (postToChat)
                    CPH.SendMessage("Moderation failed — message could not be processed.", true);
                LogToFile("<<< [PerformModeration] Exit (failure: missing input)", "DEBUG");
                LogToFile($@"<<< [PerformModeration] Summary: inputLength=0, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount=0, success={false}", "DEBUG");
                return false;
            }
            inputLength = input.Length;

            if (!moderationEnabled)
            {
                LogToFile("[PerformModeration] Moderation is globally disabled by settings.", "INFO");
                CPH.SetArgument("moderatedMessage", input);
                LogToFile("<<< [PerformModeration] Exit (moderation disabled, input passed through)", "DEBUG");
                LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount=0, success={true}", "DEBUG");
                return true;
            }

            LogToFile($"[PerformModeration] Message for moderation: {input}", "DEBUG");

            try
            {
                response = CallModerationEndpoint(input);
            }
            catch (Exception exApi)
            {
                LogToFile($"[PerformModeration] ERROR: Exception during moderation API call: {exApi.Message}", "ERROR");

                LogToFile("[PerformModeration] WARN: Moderation API call failed (recoverable, will handle as failure).", "WARN");
                response = null;
            }

            if (response?.Results == null || response.Results.Count == 0)
            {
                LogToFile("[PerformModeration] ERROR: Moderation endpoint returned null or empty results.", "ERROR");
                LogToFile("[PerformModeration] Context: API response is null or empty.", "ERROR");
                if (postToChat)
                    CPH.SendMessage("Moderation failed — message could not be processed.", true);
                LogToFile("<<< [PerformModeration] Exit (failure: API error)", "DEBUG");
                LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount=0, success={false}", "DEBUG");
                return false;
            }

            Result result = null;
            Dictionary<string, double> scores = null;
            try
            {
                result = response.Results[0];
                scores = result.Category_scores ?? new Dictionary<string, double>();
                flaggedCategories = new List<string>();

                LogToFile("Moderation Results (using local thresholds)", "INFO");
                LogToFile("--------------------------------------------------", "INFO");
                LogToFile($"{"Category",-25}{"Score",-10}{"Threshold",-12}{"Flagged",-8}", "INFO");
                LogToFile("--------------------------------------------------", "INFO");

                foreach (var kvp in scores)
                {
                    string category = kvp.Key;
                    double score = kvp.Value;
                    double threshold;
                    try
                    {
                        threshold = category switch
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
                    }
                    catch (Exception exT)
                    {
                        LogToFile($"[PerformModeration] WARN: Failed to parse threshold for category '{category}': {exT.Message}", "WARN");
                        threshold = 0.5;
                    }

                    bool flagged = score >= threshold;
                    if (flagged)
                        flaggedCategories.Add(category);
                    LogToFile($"{category,-25}{score,-10:F3}{threshold,-12:F2}{(flagged ? "Yes" : "No"),-8}", "INFO");
                }
                LogToFile("--------------------------------------------------", "INFO");
                flaggedCount = flaggedCategories.Count;
                if (flaggedCategories.Any())
                    LogToFile($"Flagged Categories: {string.Join(", ", flaggedCategories)}", "INFO");
                else
                    LogToFile("Flagged Categories: None", "INFO");
            }
            catch (Exception exCat)
            {
                LogToFile($"[PerformModeration] ERROR: Exception during category evaluation: {exCat.Message}", "ERROR");
                LogToFile("[PerformModeration] Context: Error evaluating moderation categories.", "ERROR");
                LogToFile("<<< [PerformModeration] Exit (failure: category evaluation error)", "DEBUG");
                LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount=0, success={false}", "DEBUG");
                return false;
            }

            bool passed = !flaggedCategories.Any();
            try
            {
                if (!passed)
                {
                    bool handled = HandleModerationResponse(flaggedCategories, input, rebukeEnabled);
                    LogToFile($"[PerformModeration] Message failed moderation. Handled: {handled}", "INFO");
                    LogToFile("<<< [PerformModeration] Exit (message flagged)", "DEBUG");
                    LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount={flaggedCount}, success={false}", "DEBUG");
                    return false;
                }
                else
                {
                    CPH.SetArgument("moderatedMessage", input);
                    LogToFile("[PerformModeration] Message passed moderation.", "INFO");
                }
            }
            catch (Exception exChat)
            {
                LogToFile($"[PerformModeration] ERROR: Exception during chat output/rebuke: {exChat.Message}", "ERROR");
                LogToFile("[PerformModeration] Context: Error during chat output or rebuke.", "ERROR");
                LogToFile("<<< [PerformModeration] Exit (failure: chat output/rebuke error)", "DEBUG");
                LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount={flaggedCount}, success={false}", "DEBUG");
                return false;
            }

            success = true;
            LogToFile("<<< [PerformModeration] Exit (success: message passed)", "DEBUG");
            LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount={flaggedCount}, success={success}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[PerformModeration] ERROR: Fatal exception: {ex.Message}", "ERROR");
            LogToFile($"[PerformModeration] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[PerformModeration] Context: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount={flaggedCount}", "ERROR");
            if (postToChat)
                CPH.SendMessage("Moderation failed — message could not be processed.", true);
            LogToFile("<<< [PerformModeration] Exit (failure: fatal exception)", "DEBUG");
            LogToFile($@"<<< [PerformModeration] Summary: inputLength={inputLength}, moderationEnabled={moderationEnabled}, rebukeEnabled={rebukeEnabled}, postToChat={postToChat}, flaggedCount={flaggedCount}, success={false}", "DEBUG");
            return false;
        }
    }

    private bool HandleModerationResponse(List<string> flaggedCategories, string input, bool rebukeEnabled)
    {
        LogToFile(">>> [HandleModerationResponse] Entry: Handling moderation response.", "DEBUG");
        bool postToChat = false;
        bool voiceEnabled = false;
        string voiceAlias = null;
        string flaggedCategoriesString = null;
        string outputMessage = null;

        try
        {

            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);
                voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
                LogToFile($"[HandleModerationResponse] Settings: postToChat={postToChat}, voiceEnabled={voiceEnabled}, rebukeEnabled={rebukeEnabled}, voiceAlias='{voiceAlias ?? "null"}'", "DEBUG");
            }
            catch (Exception exSettings)
            {
                LogToFile($"[HandleModerationResponse] ERROR: Failed to retrieve one or more global variables: {exSettings.Message}", "ERROR");
                LogToFile($"[HandleModerationResponse] Stack: {exSettings.StackTrace}", "DEBUG");
                return false;
            }

            if (!flaggedCategories.Any())
            {
                LogToFile("[HandleModerationResponse] Message passed moderation cleanly. Setting moderatedMessage and returning.", "INFO");
                CPH.SetArgument("moderatedMessage", input);
                LogToFile("<<< [HandleModerationResponse] Exit (no flagged categories).", "DEBUG");
                return true;
            }

            if (!postToChat)
            {
                LogToFile("[HandleModerationResponse] INFO: Post To Chat disabled; skipping moderation output, TTS, and chat.", "INFO");
                LogToFile("<<< [HandleModerationResponse] Exit (PostToChat disabled).", "DEBUG");
                return false;
            }

            if (!rebukeEnabled)
            {
                LogToFile("[HandleModerationResponse] INFO: Moderation rebuke disabled; skipping TTS and chat output.", "INFO");
                LogToFile("<<< [HandleModerationResponse] Exit (Rebuke disabled).", "DEBUG");
                return false;
            }

            flaggedCategoriesString = string.Join(", ", flaggedCategories);
            outputMessage = $"This message was flagged in the following categories: {flaggedCategoriesString}. Repeated attempts at abuse may result in a ban.";
            LogToFile($"[HandleModerationResponse] Moderation summary prepared: {outputMessage}", "INFO");

            try
            {
                if (postToChat && rebukeEnabled && voiceEnabled && !string.IsNullOrWhiteSpace(voiceAlias))
                {
                    LogToFile($"[HandleModerationResponse] Attempting TTS speak using alias '{voiceAlias}'.", "DEBUG");
                    int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
                    LogToFile($"[HandleModerationResponse] TTS speak result: {speakResult}", "INFO");
                }
                else
                {
                    LogToFile($"[HandleModerationResponse] [Skipped TTS] Conditions unmet. PostToChat={postToChat}, RebukeEnabled={rebukeEnabled}, VoiceEnabled={voiceEnabled}, VoiceAliasPresent={!string.IsNullOrWhiteSpace(voiceAlias)}", "WARN");
                }
            }
            catch (Exception exTTS)
            {
                LogToFile($"[HandleModerationResponse] ERROR: Exception during TTS speak: {exTTS.Message}", "ERROR");
                LogToFile($"[HandleModerationResponse] Stack: {exTTS.StackTrace}", "DEBUG");
            }

            try
            {
                if (postToChat && rebukeEnabled)
                {
                    LogToFile("[HandleModerationResponse] Sending flagged message output to chat.", "DEBUG");
                    CPH.SendMessage(outputMessage, true);
                    LogToFile($"[HandleModerationResponse] Sent moderation notice to chat: {outputMessage}", "INFO");
                }
                else
                {
                    LogToFile($"[HandleModerationResponse] [Skipped Chat Output] Conditions unmet. PostToChat={postToChat}, RebukeEnabled={rebukeEnabled}. Message: {outputMessage}", "WARN");
                }
            }
            catch (Exception exChat)
            {
                LogToFile($"[HandleModerationResponse] ERROR: Exception sending moderation chat message: {exChat.Message}", "ERROR");
                LogToFile($"[HandleModerationResponse] Stack: {exChat.StackTrace}", "DEBUG");
            }

            LogToFile($@"<<< [HandleModerationResponse] Exit: flaggedCount={flaggedCategories.Count}, postToChat={postToChat}, rebukeEnabled={rebukeEnabled}, voiceEnabled={voiceEnabled}, success=false", "DEBUG");
            return false;
        }
        catch (Exception ex)
        {
            LogToFile($"[HandleModerationResponse] ERROR: Fatal exception: {ex.Message}", "ERROR");
            LogToFile($"[HandleModerationResponse] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[HandleModerationResponse] Context: postToChat={postToChat}, voiceEnabled={voiceEnabled}, rebukeEnabled={rebukeEnabled}, flaggedCount={flaggedCategories?.Count ?? -1}", "ERROR");
            LogToFile("<<< [HandleModerationResponse] Exit: success=false (fatal exception).", "DEBUG");
            return false;
        }
    }

    private ModerationResponse CallModerationEndpoint(string prompt)
    {
        LogToFile(">>> [CallModerationEndpoint] Entry: Begin moderation API call.", "DEBUG");

        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogToFile("[CallModerationEndpoint] ERROR: The OpenAI API Key is missing or invalid.", "ERROR");
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

            LogToFile("[CallModerationEndpoint] Preparing WebRequest.", "DEBUG");
            WebRequest moderationWebRequest = WebRequest.Create(moderationEndpoint);
            moderationWebRequest.Method = "POST";
            moderationWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            moderationWebRequest.ContentType = "application/json";
            moderationWebRequest.ContentLength = moderationContentBytes.Length;

            LogToFile("[CallModerationEndpoint] Sending moderation request to OpenAI API.", "DEBUG");
            using (Stream requestStream = moderationWebRequest.GetRequestStream())
            {
                requestStream.Write(moderationContentBytes, 0, moderationContentBytes.Length);
            }

            using (WebResponse moderationWebResponse = moderationWebRequest.GetResponse())
            using (Stream responseStream = moderationWebResponse.GetResponseStream())
            using (StreamReader responseReader = new StreamReader(responseStream))
            {
                string moderationResponseContent = responseReader.ReadToEnd();
                LogToFile("[CallModerationEndpoint] Response received successfully.", "DEBUG");

                var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);
                if (moderationJsonResponse?.Results == null || !moderationJsonResponse.Results.Any())
                {
                    LogToFile("[CallModerationEndpoint] ERROR: No moderation results returned from API.", "ERROR");
                    return null;
                }

                LogToFile("[CallModerationEndpoint] Moderation response parsed successfully.", "DEBUG");
                LogToFile($@"<<< [CallModerationEndpoint] Exit: success=true, resultsCount={moderationJsonResponse.Results.Count}", "DEBUG");
                return moderationJsonResponse;
            }
        }
        catch (WebException webEx)
        {
            LogToFile($"[CallModerationEndpoint] ERROR: WebException occurred: {webEx.Message}", "ERROR");
            if (webEx.Response != null)
            {
                try
                {
                    var httpResp = webEx.Response as HttpWebResponse;
                    if (httpResp != null)
                        LogToFile($"[CallModerationEndpoint] HTTP Status: {httpResp.StatusCode}", "ERROR");

                    using (var stream = webEx.Response.GetResponseStream())
                    using (var reader = new StreamReader(stream ?? new MemoryStream()))
                    {
                        string responseContent = reader.ReadToEnd();
                        LogToFile($"[CallModerationEndpoint] Response Content: {responseContent}", "ERROR");
                    }
                }
                catch (Exception ex2)
                {
                    LogToFile($"[CallModerationEndpoint] WARN: Failed to read WebException response: {ex2.Message}", "WARN");
                }
            }
            else
            {
                LogToFile($"[CallModerationEndpoint] WARN: No response body available. Status: {webEx.Status}", "WARN");
            }

            LogToFile("<<< [CallModerationEndpoint] Exit: success=false (WebException)", "DEBUG");
            return null;
        }
        catch (Exception ex)
        {
            LogToFile($"[CallModerationEndpoint] ERROR: Exception during moderation call: {ex.Message}", "ERROR");
            if (ex.InnerException != null)
                LogToFile($"[CallModerationEndpoint] Inner Exception: {ex.InnerException.Message}", "ERROR");
            LogToFile("<<< [CallModerationEndpoint] Exit: success=false (Unhandled Exception)", "DEBUG");
            return null;
        }
    }

    private double ParseThreshold(string raw, double fallback)
    {
        LogToFile(">>> [ParseThreshold] Entry: Parsing threshold value.", "DEBUG");
        LogToFile($"[ParseThreshold] Input raw='{raw ?? "null"}', fallback={fallback}", "DEBUG");

        double threshold;
        try
        {
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold))
            {
                LogToFile($"[ParseThreshold] WARN: Could not parse threshold from '{raw}', using fallback {fallback}.", "WARN");
                threshold = fallback;
            }

            if (threshold < 0.0)
            {
                LogToFile($"[ParseThreshold] WARN: Threshold below 0.0 detected ({threshold}), clamping to 0.0.", "WARN");
                threshold = 0.0;
            }
            else if (threshold > 1.0)
            {
                LogToFile($"[ParseThreshold] WARN: Threshold above 1.0 detected ({threshold}), clamping to 1.0.", "WARN");
                threshold = 1.0;
            }

            LogToFile($"[ParseThreshold] Parsed threshold={threshold}.", "DEBUG");
            LogToFile($@"<<< [ParseThreshold] Exit: success=true, finalThreshold={threshold}", "DEBUG");
            return threshold;
        }
        catch (Exception ex)
        {
            LogToFile($"[ParseThreshold] ERROR: Exception while parsing threshold: {ex.Message}", "ERROR");
            LogToFile($"[ParseThreshold] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[ParseThreshold] Context: raw='{raw ?? "null"}', fallback={fallback}", "ERROR");
            LogToFile("<<< [ParseThreshold] Exit: success=false (fatal exception)", "DEBUG");
            return fallback;
        }
    }

    public bool Speak()
    {
        LogToFile(">>> [Speak] Entry: Begin speech synthesis pipeline.", "DEBUG");
        bool success = false;
        int characterNumber = 1;
        string voiceAlias = null;
        string messageToSpeak = null;
        string userName = null;
        try
        {

            try
            {
                characterNumber = CPH.GetGlobalVar<int>("character", true);
                LogToFile($"[Speak] Active character set to {characterNumber}.", "DEBUG");
            }
            catch
            {
                LogToFile("[Speak] WARN: No active 'character' variable found, defaulting to 1.", "WARN");
            }

            voiceAlias = CPH.GetGlobalVar<string>($"character_voice_alias_{characterNumber}", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                string err = $"[Speak] ERROR: No voice alias configured for Character {characterNumber}. Please set 'character_voice_alias_{characterNumber}'.";
                LogToFile(err, "ERROR");
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage(err, true);
                else
                    LogToFile($"[Speak] [Skipped Chat Output] Post To Chat disabled. Message: {err}", "WARN");
                LogToFile("<<< [Speak] Exit: success=false (missing voice alias)", "DEBUG");
                return false;
            }

            if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
            {
                messageToSpeak = moderatedMessage;
                LogToFile("[Speak] Retrieved moderatedMessage via TryGetArg.", "DEBUG");
            }
            else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
            {
                messageToSpeak = rawInput;
                LogToFile("[Speak] Retrieved rawInput via TryGetArg.", "DEBUG");
            }
            else
            {
                messageToSpeak = "";
                LogToFile("[Speak] WARN: No valid input found for speech (moderatedMessage or rawInput missing).", "WARN");
            }

            if (string.IsNullOrWhiteSpace(messageToSpeak))
            {
                string err = "[Speak] ERROR: No text provided to speak.";
                LogToFile(err, "ERROR");
                bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                if (postToChat)
                    CPH.SendMessage("No text provided to speak.", true);
                else
                    LogToFile($"[Speak] [Skipped Chat Output] Post To Chat disabled. Message: No text provided to speak.", "WARN");
                LogToFile("<<< [Speak] Exit: success=false (no text to speak)", "DEBUG");
                return false;
            }

            if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
            {
                LogToFile("[Speak] ERROR: 'userName' argument is missing or empty.", "ERROR");
                LogToFile("<<< [Speak] Exit: success=false (missing username)", "DEBUG");
                return false;
            }

            var profile = GetOrCreateUserProfile(userName);
            string formattedUser = profile?.PreferredName ?? userName;
            LogToFile($"[Speak] Using PreferredName='{formattedUser}' for user '{userName}'.", "DEBUG");

            string outputMessage = $"{formattedUser} said: {messageToSpeak}";
            LogToFile($"[Speak] Constructed speech output: {outputMessage}", "DEBUG");

            bool postToChatFlag = CPH.GetGlobalVar<bool>("Post To Chat", true);
            bool voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);

            try
            {
                if (postToChatFlag)
                {
                    CPH.SendMessage(outputMessage, true);
                    LogToFile($"[Speak] Sent message to chat: {outputMessage}", "INFO");
                }
                else
                {
                    LogToFile($"[Speak] [Skipped Chat Output] Post To Chat disabled. Message: {outputMessage}", "WARN");
                }
            }
            catch (Exception exChat)
            {
                LogToFile($"[Speak] ERROR: Exception sending chat message: {exChat.Message}", "ERROR");
            }

            try
            {
                if (voiceEnabled)
                {
                    LogToFile($"[Speak] Attempting TTS speak using alias '{voiceAlias}'.", "DEBUG");
                    int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
                    if (speakResult != 0)
                    {
                        LogToFile($"[Speak] WARN: TTS returned non-zero result code: {speakResult}", "WARN");
                        LogToFile("<<< [Speak] Exit: success=false (TTS error)", "DEBUG");
                        return false;
                    }
                    LogToFile("[Speak] TTS completed successfully.", "INFO");
                }
                else
                {
                    LogToFile($"[Speak] [Skipped TTS Output] Voice disabled. Message: {outputMessage}", "WARN");
                }
            }
            catch (Exception exTTS)
            {
                LogToFile($"[Speak] ERROR: Exception during TTS speak: {exTTS.Message}", "ERROR");
                LogToFile($"[Speak] Stack: {exTTS.StackTrace}", "DEBUG");
                LogToFile("<<< [Speak] Exit: success=false (TTS exception)", "DEBUG");
                return false;
            }

            success = true;
            LogToFile($@"<<< [Speak] Exit: success={success}, character={characterNumber}, voiceAlias='{voiceAlias}', postToChat={postToChatFlag}, voiceEnabled={voiceEnabled}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[Speak] ERROR: Fatal exception encountered: {ex.Message}", "ERROR");
            LogToFile($"[Speak] Stack: {ex.StackTrace}", "DEBUG");
            bool postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            if (postToChat)
                CPH.SendMessage("An internal error occurred while speaking.", true);
            else
                LogToFile("[Speak] [Skipped Chat Output] Post To Chat disabled. Message: An internal error occurred while speaking.", "WARN");
            LogToFile("<<< [Speak] Exit: success=false (fatal exception)", "DEBUG");
            return false;
        }
    }

    public bool RememberThis()
    {
        LogToFile(">>> [RememberThis] Entry: Starting glossary keyword save operation.", "DEBUG");
        bool postToChat = false;
        string rawInput = null;
        try
        {
            // Retrieve rawInput argument
            try
            {
                if (!CPH.TryGetArg("rawInput", out rawInput) || string.IsNullOrWhiteSpace(rawInput))
                {
                    LogToFile("[RememberThis] ERROR: Missing or empty 'rawInput' argument.", "ERROR");
                    LogToFile("<<< [RememberThis] Exit: success=false (missing rawInput)", "DEBUG");
                    return false;
                }
                LogToFile($"[RememberThis] Retrieved rawInput: '{rawInput}'", "DEBUG");
            }
            catch (Exception exArgs)
            {
                LogToFile($"[RememberThis] ERROR: Failed to retrieve arguments. {exArgs.Message}", "ERROR");
                LogToFile($"[RememberThis] Stack: {exArgs.StackTrace}", "DEBUG");
                LogToFile("<<< [RememberThis] Exit: success=false (argument retrieval failure)", "DEBUG");
                return false;
            }

            // Parse keyword and definition
            string keyword = null;
            string definition = null;
            try
            {
                var parts = rawInput.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    LogToFile("[RememberThis] ERROR: Input must include a keyword and a definition.", "ERROR");
                    LogToFile("<<< [RememberThis] Exit: success=false (not enough words)", "DEBUG");
                    return false;
                }
                keyword = parts[0].Trim();
                definition = parts[1].Trim();
                LogToFile($"[RememberThis] Parsed keyword='{keyword}', definition='{definition}'", "DEBUG");
            }
            catch (Exception exParse)
            {
                LogToFile($"[RememberThis] ERROR: Failed to parse keyword/definition: {exParse.Message}", "ERROR");
                LogToFile($"[RememberThis] Stack: {exParse.StackTrace}", "DEBUG");
                LogToFile("<<< [RememberThis] Exit: success=false (parse failure)", "DEBUG");
                return false;
            }

            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[RememberThis] Global 'Post To Chat' = {postToChat}", "DEBUG");
            }
            catch (Exception exGlobal)
            {
                LogToFile($"[RememberThis] WARN: Failed to retrieve 'Post To Chat' global variable: {exGlobal.Message}", "WARN");
                postToChat = false;
            }

            // Database upsert for keywords collection
            try
            {
                var col = _db.GetCollection<BsonDocument>("keywords");
                var existing = col.FindOne(Query.EQ("Keyword", keyword));
                if (existing != null)
                {
                    existing["Definition"] = definition;
                    col.Update(existing);
                    LogToFile($"[RememberThis] DEBUG: Updated definition for keyword '{keyword}'.", "DEBUG");
                }
                else
                {
                    var doc = new BsonDocument
                    {
                        ["Keyword"] = keyword,
                        ["Definition"] = definition
                    };
                    col.Insert(doc);
                    LogToFile($"[RememberThis] DEBUG: Inserted new keyword '{keyword}' with definition.", "DEBUG");
                }
            }
            catch (Exception exDb)
            {
                LogToFile($"[RememberThis] ERROR: Database operation failed: {exDb.Message}", "ERROR");
                LogToFile($"[RememberThis] Stack: {exDb.StackTrace}", "DEBUG");
                LogToFile($"[RememberThis] Context: keyword='{keyword}', definition='{definition}'", "ERROR");
                LogToFile("<<< [RememberThis] Exit: success=false (DB error)", "DEBUG");
                return false;
            }

            try
            {
                if (postToChat)
                {
                    string confirmation = $"Added keyword '{keyword}' with definition: {definition}";
                    CPH.SendMessage(confirmation, true);
                    LogToFile($"[RememberThis] DEBUG: Sent confirmation to chat: {confirmation}", "DEBUG");
                }
                else
                {
                    LogToFile($"[RememberThis] [Skipped Chat Output] Post To Chat disabled. Keyword='{keyword}', Definition='{definition}'", "WARN");
                }
            }
            catch (Exception exChat)
            {
                LogToFile($"[RememberThis] ERROR: Exception while sending chat message: {exChat.Message}", "ERROR");
            }

            LogToFile($@"<<< [RememberThis] Exit: success=true, keyword='{keyword}', postToChat={postToChat}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[RememberThis] ERROR: Fatal exception encountered: {ex.Message}", "ERROR");
            LogToFile($"[RememberThis] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("<<< [RememberThis] Exit: success=false (fatal exception)", "DEBUG");
            return false;
        }
    }

    public bool RememberThisAboutMe()
    {
        LogToFile(">>> [RememberThisAboutMe] Entry: Starting 'remember about me' operation.", "DEBUG");
        string userName = null;
        string messageToRemember = null;
        bool postToChat = false;

        try
        {
            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[RememberThisAboutMe] Global 'Post To Chat' = {postToChat}", "DEBUG");
            }
            catch (Exception exGlobal)
            {
                LogToFile($"[RememberThisAboutMe] WARN: Failed to retrieve 'Post To Chat' global variable: {exGlobal.Message}", "WARN");
                postToChat = false;
            }

            try
            {
                if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
                {
                    LogToFile("[RememberThisAboutMe] ERROR: 'userName' argument is missing or invalid.", "ERROR");
                    string errMsg = "I'm sorry, but I can't remember that right now. Please try again later.";
                    if (postToChat)
                    {
                        CPH.SendMessage(errMsg, true);
                    }
                    else
                    {
                        LogToFile($"[RememberThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: {errMsg}", "WARN");
                    }
                    LogToFile("<<< [RememberThisAboutMe] Exit: success=false (missing username)", "DEBUG");
                    return false;
                }
                LogToFile($"[RememberThisAboutMe] Retrieved userName='{userName}'", "DEBUG");
            }
            catch (Exception exUser)
            {
                LogToFile($"[RememberThisAboutMe] ERROR: Exception retrieving userName: {exUser.Message}", "ERROR");
                LogToFile($"[RememberThisAboutMe] Stack: {exUser.StackTrace}", "DEBUG");
                return false;
            }

            try
            {
                if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
                {
                    messageToRemember = moderatedMessage;
                    LogToFile("[RememberThisAboutMe] Retrieved 'moderatedMessage' argument.", "DEBUG");
                }
                else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
                {
                    messageToRemember = rawInput;
                    LogToFile("[RememberThisAboutMe] Retrieved 'rawInput' argument.", "DEBUG");
                }
                else
                {
                    LogToFile("[RememberThisAboutMe] ERROR: No valid input provided to remember.", "ERROR");
                    string errMsg = "I'm sorry, but I can't remember that right now. Please try again later.";
                    if (postToChat)
                    {
                        CPH.SendMessage(errMsg, true);
                    }
                    else
                    {
                        LogToFile($"[RememberThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: {errMsg}", "WARN");
                    }
                    LogToFile("<<< [RememberThisAboutMe] Exit: success=false (missing message)", "DEBUG");
                    return false;
                }
            }
            catch (Exception exMsg)
            {
                LogToFile($"[RememberThisAboutMe] ERROR: Exception retrieving message: {exMsg.Message}", "ERROR");
                LogToFile($"[RememberThisAboutMe] Stack: {exMsg.StackTrace}", "DEBUG");
                return false;
            }

            UserProfile profile = null;
            try
            {
                profile = GetOrCreateUserProfile(userName);
                if (profile == null)
                {
                    LogToFile($"[RememberThisAboutMe] ERROR: Failed to retrieve or create profile for '{userName}'.", "ERROR");
                    string errMsg = "I'm sorry, but I can't remember that right now. Please try again later.";
                    if (postToChat)
                    {
                        CPH.SendMessage(errMsg, true);
                    }
                    else
                    {
                        LogToFile($"[RememberThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: {errMsg}", "WARN");
                    }
                    LogToFile("<<< [RememberThisAboutMe] Exit: success=false (no profile)", "DEBUG");
                    return false;
                }

                if (profile.Knowledge == null)
                {
                    profile.Knowledge = new List<string>();
                }

                if (!profile.Knowledge.Contains(messageToRemember))
                {
                    profile.Knowledge.Add(messageToRemember);
                    LogToFile($"[RememberThisAboutMe] DEBUG: Added new memory for '{userName}': {messageToRemember}", "DEBUG");
                }
                else
                {
                    LogToFile($"[RememberThisAboutMe] DEBUG: Duplicate memory ignored for '{userName}': {messageToRemember}", "DEBUG");
                }
            }
            catch (Exception exProfile)
            {
                LogToFile($"[RememberThisAboutMe] ERROR: Exception retrieving/updating profile: {exProfile.Message}", "ERROR");
                LogToFile($"[RememberThisAboutMe] Stack: {exProfile.StackTrace}", "DEBUG");
                return false;
            }

            try
            {
                var userCollection = _db.GetCollection<UserProfile>("user_profiles");
                userCollection.Update(profile);
                LogToFile($"[RememberThisAboutMe] DEBUG: Updated user profile for '{userName}' in LiteDB.", "DEBUG");
            }
            catch (Exception exDb)
            {
                LogToFile($"[RememberThisAboutMe] ERROR: Database operation failed: {exDb.Message}", "ERROR");
                LogToFile($"[RememberThisAboutMe] Stack: {exDb.StackTrace}", "DEBUG");
                return false;
            }

            try
            {
                string displayName = !string.IsNullOrWhiteSpace(profile.PreferredName) ? profile.PreferredName : userName;
                string confirmation = $"OK, {displayName}, I will remember {messageToRemember} about you.";
                if (postToChat)
                {
                    CPH.SendMessage(confirmation, true);
                    LogToFile($"[RememberThisAboutMe] DEBUG: Sent confirmation to chat: {confirmation}", "DEBUG");
                }
                else
                {
                    LogToFile($"[RememberThisAboutMe] [Skipped Chat Output] Post To Chat disabled. Message: {confirmation}", "WARN");
                }
            }
            catch (Exception exChat)
            {
                LogToFile($"[RememberThisAboutMe] ERROR: Exception during chat output: {exChat.Message}", "ERROR");
                LogToFile($"[RememberThisAboutMe] Stack: {exChat.StackTrace}", "DEBUG");
            }

            LogToFile($@"<<< [RememberThisAboutMe] Exit: success=true, userName='{userName}', postToChat={postToChat}", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[RememberThisAboutMe] ERROR: Fatal exception encountered: {ex.Message}", "ERROR");
            LogToFile($"[RememberThisAboutMe] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[RememberThisAboutMe] Context: userName='{userName}', message='{messageToRemember}', postToChat={postToChat}", "ERROR");
            LogToFile("<<< [RememberThisAboutMe] Exit: success=false (fatal exception)", "DEBUG");
            return false;
        }
    }

    public bool ClearPromptHistory()
    {
        LogToFile(">>> [ClearPromptHistory] Entry: Attempting to clear prompt history.", "DEBUG");
        bool postToChat = false;

        try
        {

            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                LogToFile($"[ClearPromptHistory] Global 'Post To Chat' = {postToChat}", "DEBUG");
            }
            catch (Exception exGlobal)
            {
                LogToFile($"[ClearPromptHistory] WARN: Failed to retrieve 'Post To Chat' global variable: {exGlobal.Message}", "WARN");
                postToChat = false;
            }

            if (GPTLog == null)
            {
                LogToFile("[ClearPromptHistory] WARN: GPTLog is not initialized and cannot be cleared.", "WARN");
                string message = "Prompt history is already empty.";
                if (postToChat)
                {
                    CPH.SendMessage(message, true);
                    LogToFile($"[ClearPromptHistory] INFO: Sent message to chat: {message}", "INFO");
                }
                else
                {
                    LogToFile($"[ClearPromptHistory] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
                }
                LogToFile("<<< [ClearPromptHistory] Exit: success=false (GPTLog null)", "DEBUG");
                return false;
            }

            try
            {
                GPTLog.Clear();
                LogToFile("[ClearPromptHistory] INFO: Prompt history successfully cleared.", "INFO");

                string message = "Prompt history has been cleared.";
                if (postToChat)
                {
                    CPH.SendMessage(message, true);
                    LogToFile($"[ClearPromptHistory] INFO: Sent confirmation to chat: {message}", "INFO");
                }
                else
                {
                    LogToFile($"[ClearPromptHistory] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
                }

                LogToFile("<<< [ClearPromptHistory] Exit: success=true", "DEBUG");
                return true;
            }
            catch (Exception exClear)
            {
                LogToFile($"[ClearPromptHistory] ERROR: Exception while clearing prompt history: {exClear.Message}", "ERROR");
                LogToFile($"[ClearPromptHistory] Stack: {exClear.StackTrace}", "DEBUG");
                string message = "I was unable to clear the prompt history. Please check the log file for more details.";
                if (postToChat)
                {
                    CPH.SendMessage(message, true);
                    LogToFile($"[ClearPromptHistory] INFO: Sent failure message to chat: {message}", "INFO");
                }
                else
                {
                    LogToFile($"[ClearPromptHistory] [Skipped Chat Output] Post To Chat disabled. Message: {message}", "WARN");
                }
                LogToFile("<<< [ClearPromptHistory] Exit: success=false (exception during clear)", "DEBUG");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[ClearPromptHistory] ERROR: Fatal exception encountered: {ex.Message}", "ERROR");
            LogToFile($"[ClearPromptHistory] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[ClearPromptHistory] Context: postToChat={postToChat}, GPTLogNull={(GPTLog == null)}", "ERROR");
            LogToFile("<<< [ClearPromptHistory] Exit: success=false (fatal exception)", "DEBUG");
            return false;
        }
    }

    private void LogPromptScorecard(string methodName, string model, UsageData usage)
    {
        LogToFile($">>> [LogPromptScorecard] Entry: Logging token usage for method '{methodName}', model '{model}'.", "DEBUG");
        try
        {

            if (usage == null)
            {
                LogToFile($"[LogPromptScorecard] WARN: No usage data available for {methodName}. Skipping scorecard logging.", "WARN");
                LogToFile("<<< [LogPromptScorecard] Exit: usage=null", "DEBUG");
                return;
            }

            long promptTokens = 0, completionTokens = 0, totalTokens = 0;
            try { promptTokens = usage?.PromptTokens ?? 0; } catch { promptTokens = 0; }
            try { completionTokens = usage?.CompletionTokens ?? 0; } catch { completionTokens = 0; }
            try { totalTokens = usage?.TotalTokens ?? 0; } catch { totalTokens = 0; }
            LogToFile($"[LogPromptScorecard] Token counts extracted: prompt={promptTokens}, completion={completionTokens}, total={totalTokens}", "DEBUG");

            long rollingPromptTokens = 0;
            long rollingCompletionTokens = 0;
            long rollingTotalTokens = 0;
            try
            {
                LogToFile("[LogPromptScorecard] Updating rolling 30-day totals in LiteDB.", "DEBUG");
                var usageCollection = _db.GetCollection<BsonDocument>("token_usage");

                var entry = new BsonDocument
                {
                    ["Timestamp"] = DateTime.UtcNow,
                    ["PromptTokens"] = promptTokens,
                    ["CompletionTokens"] = completionTokens,
                    ["TotalTokens"] = totalTokens
                };
                usageCollection.Insert(entry);

                var cutoff = DateTime.UtcNow.AddDays(-30);
                usageCollection.DeleteMany(Query.LT("Timestamp", cutoff));

                var recentDocs = usageCollection.Find(Query.GTE("Timestamp", cutoff)).ToList();
                rollingPromptTokens = recentDocs.Sum(x => { try { return x["PromptTokens"].AsInt64; } catch { return 0L; } });
                rollingCompletionTokens = recentDocs.Sum(x => { try { return x["CompletionTokens"].AsInt64; } catch { return 0L; } });
                rollingTotalTokens = recentDocs.Sum(x => { try { return x["TotalTokens"].AsInt64; } catch { return 0L; } });

                LogToFile($"[LogPromptScorecard] Rolling totals: prompt={rollingPromptTokens}, completion={rollingCompletionTokens}, total={rollingTotalTokens}", "DEBUG");
            }
            catch (Exception exLiteDb)
            {
                LogToFile($"[LogPromptScorecard] ERROR: LiteDB token_usage integration failed: {exLiteDb.Message}", "ERROR");
                LogToFile($"[LogPromptScorecard] Stack: {exLiteDb.StackTrace}", "DEBUG");
            }

            double inputRate = 0, outputRate = 0;
            try
            {
                inputRate = CPH.GetGlobalVar<double>("model_input_cost", true);
                outputRate = CPH.GetGlobalVar<double>("model_output_cost", true);
                if (inputRate == 0) inputRate = 2.50;
                if (outputRate == 0) outputRate = 10.00;
                LogToFile($"[LogPromptScorecard] Model cost rates: input={inputRate}/M, output={outputRate}/M", "DEBUG");
            }
            catch (Exception exRates)
            {
                LogToFile($"[LogPromptScorecard] WARN: Could not retrieve model rates: {exRates.Message}. Using defaults input=2.50, output=10.00", "WARN");
                inputRate = 2.50;
                outputRate = 10.00;
            }

            double promptCost = Math.Round((promptTokens / 1_000_000.0) * inputRate, 6);
            double completionCost = Math.Round((completionTokens / 1_000_000.0) * outputRate, 6);
            double totalPromptCost = Math.Round(promptCost + completionCost, 6);
            double rollingCost = Math.Round(((rollingPromptTokens / 1_000_000.0) * inputRate) + ((rollingCompletionTokens / 1_000_000.0) * outputRate), 4);
            LogToFile($"[LogPromptScorecard] Calculated costs: prompt={promptCost:C4}, completion={completionCost:C4}, total={totalPromptCost:C4}, rolling={rollingCost:C2}", "DEBUG");

            LogToFile($"[LogPromptScorecard] INFO: Prompt Scorecard ({methodName})", "INFO");
            LogToFile("--------------------------------------------------", "INFO");
            LogToFile($"{"Metric",-30}{"Value",-10}", "INFO");
            LogToFile("--------------------------------------------------", "INFO");
            LogToFile($"{"Model",-30}{model,-10}", "INFO");
            LogToFile($"{"Prompt Tokens",-30}{promptTokens,-10}", "INFO");
            LogToFile($"{"Completion Tokens",-30}{completionTokens,-10}", "INFO");
            LogToFile($"{"Total Tokens",-30}{totalTokens,-10}", "INFO");
            LogToFile($"{"30-Day Rolling Tokens",-30}{rollingTotalTokens,-10}", "INFO");
            LogToFile($"{"Prompt Cost",-30}{totalPromptCost.ToString("C4"),-10}", "INFO");
            LogToFile($"{"30-Day Rolling Cost",-30}{rollingCost.ToString("C2"),-10}", "INFO");
            LogToFile("--------------------------------------------------", "INFO");

            LogToFile($@"<<< [LogPromptScorecard] Exit: success=true, methodName='{methodName}', model='{model}'", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[LogPromptScorecard] ERROR: Fatal exception while logging prompt scorecard: {ex.Message}", "ERROR");
            LogToFile($"[LogPromptScorecard] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[LogPromptScorecard] Context: methodName='{methodName}', model='{model}', usageNull={(usage == null)}", "ERROR");
            LogToFile("<<< [LogPromptScorecard] Exit: success=false (fatal exception)", "DEBUG");
        }
    }

    public bool AskGPT()
    {
        LogToFile("==== Begin AskGPT Execution ====", "DEBUG");
        LogToFile("Entering AskGPT method (streamer.bot pronouns, LiteDB context, webhook/discord sync).", "DEBUG");

        bool postToChat = false;
        bool voiceEnabled = false;
        int characterNumber = 1;
        string voiceAlias = null;
        string userName = null;
        string pronounSubject = null, pronounObject = null, pronounPossessive = null, pronounReflexive = null, pronounDescription = null;
        string userToSpeak = null;
        string fullMessage = null;
        string prompt = null;
        string databasePath = null;
        string characterFileName = null;
        string ContextFilePath = null;
        string context = null;
        string broadcaster = null, currentTitle = null, currentGame = null;
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        var allUserProfiles = (List<UserProfile>)null;
        var keywordsCol = _db.GetCollection<BsonDocument>("keywords");
        var keywordDocs = (List<BsonDocument>)null;
        List<string> mentionedUsers = null;
        var pronounContextEntries = (List<string>)null;
        var enrichmentSections = (List<string>)null;
        // Use StringBuilder for contextBody to support AppendLine()
        var contextBody = new System.Text.StringBuilder();
        string contextBodyString = null;
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        string completionsRequestJSON = null;
        string completionsResponseContent = null;
        string GPTResponse = null;
        int maxChatHistory = 0;
        int maxPromptHistory = 0;
        string completionsUrl = null;
        string apiKey = null;
        string AIModel = null;
        var messages = (List<chatMessage>)null;
        int maxAttempts = 3;
        int attempt = 0;
        bool apiSuccess = false;
        Exception lastException = null;
        int[] backoffSeconds = { 1, 2, 4 };
        ChatCompletionsResponse completionsJsonResponse = null;
        try
        {

            try
            {
                postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
                voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);
                LogToFile($"[AskGPT] DEBUG: postToChat={postToChat}, voiceEnabled={voiceEnabled}", "DEBUG");
            }
            catch (Exception exInit)
            {
                LogToFile($"[AskGPT] ERROR: Initialization failed: {exInit.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exInit.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: postToChat={postToChat}, voiceEnabled={voiceEnabled}", "ERROR");
                return false;
            }

            try
            {
                try
                {
                    characterNumber = CPH.GetGlobalVar<int>("character", true);
                    LogToFile($"Active character number set to {characterNumber}.", "DEBUG");
                }
                catch
                {
                    LogToFile("No active 'character' variable found. Defaulting to 1.", "WARN");
                    characterNumber = 1;
                }
                voiceAlias = CPH.GetGlobalVar<string>($"character_voice_alias_{characterNumber}", true);
                if (string.IsNullOrWhiteSpace(voiceAlias))
                {
                    string err = $"No voice alias configured for Character {characterNumber}. Please set 'character_voice_alias_{characterNumber}'.";
                    LogToFile(err, "ERROR");
                    if (postToChat)
                        CPH.SendMessage(err, true);
                    else
                        LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {err}", "DEBUG");
                    LogToFile("==== End AskGPT Execution ====", "DEBUG");
                    return false;
                }
            }
            catch (Exception exChar)
            {
                LogToFile($"[AskGPT] ERROR: Character/voice argument parsing failed: {exChar.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exChar.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: characterNumber={characterNumber}, voiceAlias='{voiceAlias}'", "ERROR");
                return false;
            }

            try
            {
                if (ChatLog == null)
                {
                    ChatLog = new Queue<chatMessage>();
                    LogToFile("ChatLog queue has been initialized for the first time.", "DEBUG");
                }
                else
                {
                    string chatLogAsString = string.Join(Environment.NewLine, ChatLog.Select(m => m.content ?? "null"));
                    LogToFile($"ChatLog Content before asking GPT: {Environment.NewLine}{chatLogAsString}", "DEBUG");
                }
            }
            catch (Exception exCL)
            {
                LogToFile($"[AskGPT] ERROR: ChatLog state error: {exCL.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exCL.StackTrace}", "DEBUG");
            }

            try
            {
                if (!CPH.TryGetArg("userName", out userName) || string.IsNullOrWhiteSpace(userName))
                {
                    LogToFile("'userName' argument is not found or not a valid string.", "ERROR");
                    string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
                    if (postToChat)
                        CPH.SendMessage(msg, true);
                    else
                        LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
                    LogToFile("==== End AskGPT Execution ====", "DEBUG");
                    return false;
                }
                LogToFile("Retrieved and validated 'userName' argument.", "DEBUG");
                pronounSubject = CPH.GetGlobalVar<string>("pronounSubject", false);
                pronounObject = CPH.GetGlobalVar<string>("pronounObject", false);
                pronounPossessive = CPH.GetGlobalVar<string>("pronounPossessive", false);
                pronounReflexive = CPH.GetGlobalVar<string>("pronounReflexive", false);
                pronounDescription = "";
                if (!string.IsNullOrWhiteSpace(pronounSubject) && !string.IsNullOrWhiteSpace(pronounObject))
                {
                    pronounDescription = $"({pronounSubject}/{pronounObject}";
                    if (!string.IsNullOrWhiteSpace(pronounPossessive)) pronounDescription += $"/{pronounPossessive}";
                    if (!string.IsNullOrWhiteSpace(pronounReflexive)) pronounDescription += $"/{pronounReflexive}";
                    pronounDescription += ")";
                }
                userToSpeak = userName;
                if (!string.IsNullOrWhiteSpace(pronounDescription))
                    userToSpeak = $"{userName} {pronounDescription}";
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
                        LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
                    LogToFile("==== End AskGPT Execution ====", "DEBUG");
                    return false;
                }
                prompt = $"{userToSpeak} asks: {fullMessage}";
                LogToFile($"[AskGPT] INFO: Prompt input: {prompt}", "INFO");
                LogToFile($"Constructed prompt for GPT: {prompt}", "DEBUG");
            }
            catch (Exception exArgs)
            {
                LogToFile($"[AskGPT] ERROR: Argument parsing failed: {exArgs.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exArgs.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: userName='{userName}', fullMessage='{fullMessage}'", "ERROR");
                return false;
            }

            try
            {
                databasePath = CPH.GetGlobalVar<string>("Database Path", true);
                if (string.IsNullOrWhiteSpace(databasePath))
                {
                    LogToFile("'Database Path' global variable is not found or not a valid string.", "ERROR");
                    string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
                    if (postToChat)
                        CPH.SendMessage(msg, true);
                    else
                        LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
                    LogToFile("==== End AskGPT Execution ====", "INFO");
                    return false;
                }
                characterFileName = CPH.GetGlobalVar<string>($"character_file_{characterNumber}", true);
                if (string.IsNullOrWhiteSpace(characterFileName))
                {
                    characterFileName = "context.txt";
                    LogToFile($"Character file not set for {characterNumber}, defaulting to context.txt", "WARN");
                }
                ContextFilePath = Path.Combine(databasePath, characterFileName);
                context = File.Exists(ContextFilePath) ? File.ReadAllText(ContextFilePath) : "";
                broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
                currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
                currentGame = CPH.GetGlobalVar<string>("currentGame", false);
                allUserProfiles = userCollection.FindAll().ToList();
                keywordDocs = keywordsCol.FindAll().ToList();
                mentionedUsers = new List<string>();
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
                pronounContextEntries = new List<string>();
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
                enrichmentSections = new List<string>();
                if (pronounContextEntries.Count > 0)
                {
                    string pronounContext = "Known pronouns for participants: " + string.Join(" ", pronounContextEntries);
                    enrichmentSections.Add(pronounContext);
                    LogToFile($"Added pronoun context system message: {pronounContext}", "DEBUG");

                    // --- Dynamic Context Assembly (Keywords + User Knowledge) ---
                    try
                    {
                        LogToFile("[AskGPT] DEBUG: Starting dynamic context assembly.", "DEBUG");

                        List<BsonDocument> profileDocs = null;
                        Dictionary<string, string> keywordDict = null;
                        Dictionary<string, BsonDocument> userDict = null;
                        // Use the existing keywordDocs variable already declared
                        profileDocs = _db.GetCollection<BsonDocument>("user_profiles").FindAll().ToList();

                        // Null safety for ToDictionary()
                        keywordDict = keywordDocs
                            .Where(k => k.ContainsKey("Keyword") && k.ContainsKey("Definition"))
                            .ToDictionary(k => k["Keyword"].AsString.ToLowerInvariant(), k => k["Definition"].AsString);

                        userDict = profileDocs
                            .Where(u => u.ContainsKey("UserName"))
                            .ToDictionary(u => u["UserName"].AsString.ToLowerInvariant(), u => u);

                        var words = prompt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(w => w.TrimStart('@').ToLowerInvariant())
                                          .Distinct()
                                          .ToList();

                        var insertedCount = 0;

                        foreach (var word in words)
                        {
                            if (keywordDict.TryGetValue(word, out var def))
                            {
                                var line = $"Something you remember about {word} is {def}.";
                                contextBody.AppendLine(line);
                                insertedCount++;
                                LogToFile($"[AskGPT] INFO: Inserted context for keyword '{word}' -> \"{def}\"", "INFO");
                            }

                            if (userDict.TryGetValue(word, out var profile))
                            {
                                if (profile["Knowledge"].IsArray && profile["Knowledge"].AsArray.Count > 0)
                                {
                                    var know = string.Join(", ", profile["Knowledge"].AsArray.Select(k => k.AsString));
                                    var line = $"Something I remember about {word} is {know}.";
                                    contextBody.AppendLine(line);
                                    insertedCount++;
                                    LogToFile($"[AskGPT] INFO: Inserted context for user '{word}' -> \"{know}\"", "INFO");
                                }
                            }
                        }

                        LogToFile($"[AskGPT] DEBUG: Dynamic context assembly complete. Inserted {insertedCount} entries.", "DEBUG");
                        // No finally block here; cleanup moved to end of AskGPT
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[AskGPT] ERROR: Context assembly failed: {ex.Message}", "ERROR");
                        LogToFile($"[AskGPT] Stack: {ex.StackTrace}", "DEBUG");
                    }
                    // --- End Dynamic Context Assembly ---
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
                // Null safety for contextBody
                if (contextBody == null)
                    contextBody = new System.Text.StringBuilder();
                // Append enrichment sections to contextBody
                foreach (var section in enrichmentSections)
                {
                    contextBody.AppendLine(section);
                }
                contextBodyString = contextBody.ToString();
                LogToFile("Assembled dynamic context body for GPT prompt (LiteDB):\n" + contextBodyString, "DEBUG");
            }
            catch (Exception exDB)
            {
                LogToFile($"[AskGPT] ERROR: Database/context gathering failed: {exDB.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exDB.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: databasePath='{databasePath}', characterFileName='{characterFileName}', userName='{userName}'", "ERROR");
                return false;
            }

            sw.Start();
            try
            {
                maxChatHistory = CPH.GetGlobalVar<int>("max_chat_history", true);
                maxPromptHistory = CPH.GetGlobalVar<int>("max_prompt_history", true);
                completionsUrl = CPH.GetGlobalVar<string>("openai_completions_url", true);
                if (string.IsNullOrWhiteSpace(completionsUrl))
                    completionsUrl = "https://api.openai.com/v1/chat/completions";
                apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
                AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);
                messages = new List<chatMessage>();
                messages.Add(new chatMessage { role = "system", content = contextBodyString });
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
                LogToFile($"[AskGPT] DEBUG: Request JSON: {completionsRequestJSON}", "DEBUG");
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
                            // Log raw model output before any parsing or cleaning
                            LogToFile($"[AskGPT] INFO: Raw model output: {completionsResponseContent}", "INFO");
                            LogToFile($"[AskGPT] DEBUG: Response JSON: {completionsResponseContent}", "DEBUG");
                            completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                            GPTResponse = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                            // Log parsed model output (uncleaned)
                            LogToFile($"[AskGPT] INFO: Parsed model output (uncleaned): {GPTResponse}", "INFO");
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
                        LogToFile($"[AskGPT] ERROR: OpenAI API request failed (attempt {attempt + 1}/{maxAttempts}). HTTP Status: {statusCode}. Response: {respBody}", "ERROR");
                        LogToFile($"[AskGPT] Context: completionsUrl='{completionsUrl}', userName='{userName}', characterNumber={characterNumber}, attempt={attempt + 1}", "ERROR");
                        if (attempt < maxAttempts - 1)
                        {
                            LogToFile($"[AskGPT] WARN: Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
                            System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        LogToFile($"[AskGPT] ERROR: An error occurred during OpenAI API call (attempt {attempt + 1}/{maxAttempts}): {ex.Message}", "ERROR");
                        LogToFile($"[AskGPT] Context: completionsUrl='{completionsUrl}', userName='{userName}', characterNumber={characterNumber}, attempt={attempt + 1}", "ERROR");
                        if (attempt < maxAttempts - 1)
                        {
                            LogToFile($"[AskGPT] WARN: Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
                            System.Threading.Thread.Sleep(backoffSeconds[attempt] * 1000);
                        }
                    }
                }
            }
            catch (Exception exAPI)
            {
                LogToFile($"[AskGPT] ERROR: API request/response failed: {exAPI.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exAPI.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: completionsUrl='{completionsUrl}', userName='{userName}', characterNumber={characterNumber}", "ERROR");
                return false;
            }
            finally
            {
                sw.Stop();
                LogToFile($"[AskGPT] INFO: OpenAI API call completed in {sw.ElapsedMilliseconds} ms.", "INFO");
            }

            try
            {
                if (apiSuccess && completionsJsonResponse?.Usage != null)
                {
                LogPromptScorecard(
                    "AskGPT",
                    AIModel,
                    completionsJsonResponse.Usage
                );
                }
            }
            catch (Exception exScore)
            {
                LogToFile($"[AskGPT] ERROR: Scorecard logging failed: {exScore.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exScore.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: AIModel='{AIModel}', userName='{userName}'", "ERROR");
            }
            
            GPTResponse = CleanAIText(GPTResponse);
            LogToFile("[AskGPT] DEBUG: Applied CleanAIText() to GPT response.", "DEBUG");
            // Log cleaned model output
            LogToFile($"[AskGPT] INFO: Cleaned model output: {GPTResponse}", "INFO");
            LogToFile($"[AskGPT] INFO: Model response: {GPTResponse}", "INFO");
            if (!apiSuccess || string.IsNullOrWhiteSpace(GPTResponse))
            {
                if (!apiSuccess)
                {
                    string apiFailMsg = "OpenAI API request failed after multiple attempts.";
                    LogToFile($"[AskGPT] ERROR: {apiFailMsg}", "ERROR");
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
                        LogToFile($"[AskGPT] ERROR: Final OpenAI API failure. HTTP Status: {status}. Response: {failBody}", "ERROR");
                    }
                    if (postToChat)
                        CPH.SendMessage(apiFailMsg, true);
                    else
                        LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {apiFailMsg}", "DEBUG");
                }
                else
                {
                    LogToFile("[AskGPT] ERROR: GPT model did not return a response.", "ERROR");
                    string msg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
                    if (postToChat)
                        CPH.SendMessage(msg, true);
                    else
                        LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
                }
                if (allUserProfiles != null) { allUserProfiles.Clear(); allUserProfiles = null; }
                if (keywordDocs != null) { keywordDocs.Clear(); keywordDocs = null; }
                LogToFile("==== End AskGPT Execution ====", "INFO");
                return false;
            }

            try
            {
                LogToFile($"[AskGPT] DEBUG: GPT model response: {GPTResponse}", "DEBUG");
                CPH.SetGlobalVar("Response", GPTResponse, true);
                LogToFile("[AskGPT] DEBUG: Stored GPT response in global variable 'Response'.", "DEBUG");

                string outboundWebhookUrl = CPH.GetGlobalVar<string>("outbound_webhook_url", true);
                if (string.IsNullOrWhiteSpace(outboundWebhookUrl))
                    outboundWebhookUrl = "https://api.openai.com/v1/chat/completions";
                string outboundWebhookMode = CPH.GetGlobalVar<string>("outbound_webhook_mode", true);
                if ((outboundWebhookMode ?? "").Equals("Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    LogToFile("[AskGPT] DEBUG: Outbound webhook mode is set to 'Disabled'. Skipping webhook.", "DEBUG");
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
                            contextBody = contextBodyString,
                            completionsRequestJSON = completionsRequestJSON,
                            completionsResponseContent = completionsResponseContent
                        };
                        payload = JsonConvert.SerializeObject(fullPayloadObj);
                    }
                    else
                    {
                        payload = JsonConvert.SerializeObject(new { response = GPTResponse });
                    }
                    LogToFile($"[AskGPT] DEBUG: Sending outbound webhook payload: {payload}", "DEBUG");
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
                            LogToFile("[AskGPT] DEBUG: Outbound webhook POST successful.", "DEBUG");
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
                        LogToFile($"[AskGPT] ERROR: Failed to POST outbound webhook. HTTP Status: {status}. Response: {respBody}", "ERROR");
                        LogToFile($"[AskGPT] Context: outboundWebhookUrl='{outboundWebhookUrl}', outboundWebhookMode='{outboundWebhookMode}'", "ERROR");
                        string msg = "Outbound webhook failed to deliver response.";
                        if (postToChat)
                            CPH.SendMessage(msg, true);
                        else
                            LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[AskGPT] ERROR: Failed to POST outbound webhook: {ex.Message}", "ERROR");
                        LogToFile($"[AskGPT] Stack: {ex.StackTrace}", "DEBUG");
                        LogToFile($"[AskGPT] Context: outboundWebhookUrl='{outboundWebhookUrl}', outboundWebhookMode='{outboundWebhookMode}'", "ERROR");
                        string msg = "Outbound webhook failed to deliver response.";
                        if (postToChat)
                            CPH.SendMessage(msg, true);
                        else
                            LogToFile($"[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: {msg}", "DEBUG");
                    }
                }
                if (voiceEnabled)
                {
                    CPH.TtsSpeak(voiceAlias, GPTResponse, false);
                    LogToFile($"[AskGPT] DEBUG: Character {characterNumber} spoke GPT's response.", "DEBUG");
                }
                else
                {
                    LogToFile($"[AskGPT] DEBUG: [Skipped TTS Output] Voice disabled. Message: {GPTResponse}", "DEBUG");
                }
                if (postToChat)
                {
                    CPH.SendMessage(GPTResponse, true);
                    LogToFile("[AskGPT] DEBUG: Sent GPT response to chat.", "DEBUG");
                }
                else
                {
                    LogToFile($"[AskGPT] DEBUG: [Skipped Chat Output] Post To Chat disabled. Message: {GPTResponse}", "DEBUG");
                }
                bool logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
                if (logDiscord)
                {
                    PostToDiscord(prompt, GPTResponse);
                    LogToFile("[AskGPT] DEBUG: Posted GPT result to Discord.", "DEBUG");
                }
            }
            catch (Exception exOut)
            {
                LogToFile($"[AskGPT] ERROR: Output (chat/webhook/TTS) failed: {exOut.Message}", "ERROR");
                LogToFile($"[AskGPT] Stack: {exOut.StackTrace}", "DEBUG");
                LogToFile($"[AskGPT] Context: voiceEnabled={voiceEnabled}, postToChat={postToChat}, characterNumber={characterNumber}, voiceAlias='{voiceAlias}'", "ERROR");
                return false;
            }
            // Cleanup keywordDocs and allUserProfiles at the end
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
            LogToFile("==== End AskGPT Execution ====", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPT] ERROR: Fatal exception encountered: {ex.Message}", "ERROR");
            LogToFile($"[AskGPT] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[AskGPT] Context: userName='{userName}', characterNumber={characterNumber}, completionsUrl='{completionsUrl}', voiceAlias='{voiceAlias}', postToChat={postToChat}, voiceEnabled={voiceEnabled}", "ERROR");
            bool postToChatFatal = false;
            try { postToChatFatal = CPH.GetGlobalVar<bool>("Post To Chat", true); } catch { }
            if (postToChatFatal)
                CPH.SendMessage("An internal error occurred in AskGPT.", true);
            else
                LogToFile("[AskGPT] [Skipped Chat Output] Post To Chat disabled. Message: An internal error occurred in AskGPT.", "WARN");
            // Cleanup keywordDocs and allUserProfiles at the end (also on fatal error)
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
            LogToFile("==== End AskGPT Execution ====", "DEBUG");
            return false;
        }
    }

    public bool AskGPTWebhook()
    {
        LogToFile("==== Begin AskGPTWebhook Execution ====", "DEBUG");
        LogToFile("Entering AskGPTWebhook (LiteDB context enrichment, outbound webhook, pronoun support, TTS/chat/discord parity).", "DEBUG");

        bool postToChat = false;
        bool voiceEnabled = false;
        try
        {
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            voiceEnabled = CPH.GetGlobalVar<bool>("voice_enabled", true);
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Failed to retrieve global vars Post To Chat/voice_enabled: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
            return false;
        }

        string fullMessage = null;
        try
        {
            if (CPH.TryGetArg("moderatedMessage", out string moderatedMessage) && !string.IsNullOrWhiteSpace(moderatedMessage))
            {
                fullMessage = moderatedMessage;
                LogToFile("[AskGPTWebhook] INFO: Moderation applied. Using 'moderatedMessage' as prompt input.", "INFO");
            }
            else if (CPH.TryGetArg("rawInput", out string rawInput) && !string.IsNullOrWhiteSpace(rawInput))
            {
                fullMessage = rawInput;
                LogToFile("[AskGPTWebhook] INFO: Using 'rawInput' as prompt input.", "INFO");
            }
            if (string.IsNullOrWhiteSpace(fullMessage))
            {
                LogToFile("Both 'moderatedMessage' and 'rawInput' are missing or empty.", "ERROR");
                LogToFile("==== End AskGPTWebhook Execution ====", "INFO");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Failed to parse arguments: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
            return false;
        }

        int maxChatHistory = 10;
        int maxPromptHistory = 10;
        try
        {
            maxChatHistory = CPH.GetGlobalVar<int>("max_chat_history", true);
            maxPromptHistory = CPH.GetGlobalVar<int>("max_prompt_history", true);
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Failed to retrieve chat/prompt history globals: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
        }

        int characterNumber = 1;
        try
        {
            characterNumber = CPH.GetGlobalVar<int>("character", true);
            LogToFile($"[AskGPTWebhook] DEBUG: Active character number set to {characterNumber}.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile("[AskGPTWebhook] DEBUG: No active 'character' variable found. Defaulting to 1.", "DEBUG");
            LogToFile($"[AskGPTWebhook] Context: exception={ex.Message}", "DEBUG");
        }

        string voiceAlias = null;
        try
        {
            voiceAlias = CPH.GetGlobalVar<string>($"character_voice_alias_{characterNumber}", true);
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Could not get voice alias for Character {characterNumber}: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
        }
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            string err = $"No voice alias configured for Character {characterNumber}. Please set 'character_voice_alias_{characterNumber}'.";
            LogToFile(err, "ERROR");
            if (postToChat)
                CPH.SendMessage(err, true);
            else
                LogToFile($"[AskGPTWebhook] [Skipped Chat Output] Post To Chat disabled. Message: {err}", "DEBUG");
            LogToFile("==== End AskGPTWebhook Execution ====", "DEBUG");
            return false;
        }

        string databasePath = null, characterFileName = null, ContextFilePath = null, context = null, broadcaster = null, currentTitle = null, currentGame = null;
        var allUserProfiles = new List<UserProfile>();
        var keywordDocs = new List<BsonDocument>();
        var userCollection = _db.GetCollection<UserProfile>("user_profiles");
        var keywordsCol = _db.GetCollection<BsonDocument>("keywords");
        string pronounSubject = null, pronounObject = null, pronounPossessive = null, pronounReflexive = null, pronounDescription = "";
        string userToSpeak = "User";
        List<string> mentionedUsers = new List<string>();
        List<string> pronounContextEntries = new List<string>();
        List<string> enrichmentSections = new List<string>();
        string contextBody = null;
        string prompt = null;
        try
        {
            databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' global variable is not found or not a valid string.", "ERROR");
                LogToFile("==== End AskGPTWebhook Execution ====", "DEBUG");
                return false;
            }
            characterFileName = CPH.GetGlobalVar<string>($"character_file_{characterNumber}", true);
            if (string.IsNullOrWhiteSpace(characterFileName))
            {
                characterFileName = "context.txt";
                LogToFile($"[AskGPTWebhook] DEBUG: Character file not set for {characterNumber}, defaulting to context.txt", "DEBUG");
            }
            ContextFilePath = Path.Combine(databasePath, characterFileName);
            context = File.Exists(ContextFilePath) ? File.ReadAllText(ContextFilePath) : "";
            broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
            currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
            currentGame = CPH.GetGlobalVar<string>("currentGame", false);
            allUserProfiles = userCollection.FindAll().ToList();
            keywordDocs = keywordsCol.FindAll().ToList();
            pronounSubject = CPH.GetGlobalVar<string>("pronounSubject", false);
            pronounObject = CPH.GetGlobalVar<string>("pronounObject", false);
            pronounPossessive = CPH.GetGlobalVar<string>("pronounPossessive", false);
            pronounReflexive = CPH.GetGlobalVar<string>("pronounReflexive", false);
            if (!string.IsNullOrWhiteSpace(pronounSubject) && !string.IsNullOrWhiteSpace(pronounObject))
            {
                pronounDescription = $"({pronounSubject}/{pronounObject}";
                if (!string.IsNullOrWhiteSpace(pronounPossessive)) pronounDescription += $"/{pronounPossessive}";
                if (!string.IsNullOrWhiteSpace(pronounReflexive)) pronounDescription += $"/{pronounReflexive}";
                pronounDescription += ")";
            }
            if (!string.IsNullOrWhiteSpace(pronounDescription))
                userToSpeak = $"User {pronounDescription}";
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
            var broadcasterProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(broadcaster, StringComparison.OrdinalIgnoreCase));
            if (broadcasterProfile != null && !string.IsNullOrWhiteSpace(broadcasterProfile.Pronouns))
                pronounContextEntries.Add($"{broadcasterProfile.PreferredName} uses pronouns {broadcasterProfile.Pronouns}.");
            foreach (var uname in mentionedUsers.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var mentionedProfile = allUserProfiles.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(uname, StringComparison.OrdinalIgnoreCase));
                if (mentionedProfile != null && !string.IsNullOrWhiteSpace(mentionedProfile.Pronouns))
                    pronounContextEntries.Add($"{mentionedProfile.PreferredName} uses pronouns {mentionedProfile.Pronouns}.");
            }
            if (pronounContextEntries.Count > 0)
            {
                string pronounContext = "Known pronouns for participants: " + string.Join(" ", pronounContextEntries);
                enrichmentSections.Add(pronounContext);
                LogToFile($"[AskGPTWebhook] DEBUG: Pronoun context: {pronounContext}", "DEBUG");
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
            contextBody = string.Join("\n", enrichmentSections);
            prompt = $"{userToSpeak} asks: {fullMessage}";
            LogToFile($"[AskGPTWebhook] DEBUG: Assembled enriched context for webhook:\n{contextBody}", "DEBUG");
            LogToFile($"[AskGPTWebhook] INFO: Prompt input: {prompt}", "INFO");
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Context/database setup failed: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[AskGPTWebhook] Context: databasePath='{databasePath}', characterFileName='{characterFileName}', characterNumber={characterNumber}", "ERROR");
            return false;
        }

        // --- Dynamic Context Assembly (Keywords + User Knowledge) ---
        // --- Dynamic Context Assembly (Keywords + User Knowledge) ---
        List<BsonDocument> keywordDocs_web = null;
        List<BsonDocument> profileDocs_web = null;
        Dictionary<string, string> keywordDict_web = null;
        Dictionary<string, BsonDocument> userDict_web = null;
        try
        {
            LogToFile("[AskGPTWebhook] DEBUG: Starting dynamic context assembly.", "DEBUG");

            keywordDocs_web = _db.GetCollection<BsonDocument>("keywords").FindAll().ToList();
            profileDocs_web = _db.GetCollection<BsonDocument>("user_profiles").FindAll().ToList();

            // Null safety for ToDictionary
            keywordDict_web = keywordDocs_web
                .Where(k => k.ContainsKey("Keyword") && k.ContainsKey("Definition"))
                .ToDictionary(k => k["Keyword"].AsString.ToLowerInvariant(), k => k["Definition"].AsString);

            userDict_web = profileDocs_web
                .Where(u => u.ContainsKey("UserName"))
                .ToDictionary(u => u["UserName"].AsString.ToLowerInvariant(), u => u);

            var contextSb_web = new StringBuilder();
            if (!string.IsNullOrEmpty(contextBody))
                contextSb_web.AppendLine(contextBody);

            var words = prompt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(w => w.TrimStart('@').ToLowerInvariant())
                              .Distinct()
                              .ToList();

            int insertedCount = 0;
            foreach (var w in words)
            {
                if (keywordDict_web.TryGetValue(w, out var def))
                {
                    var line = $"Something you remember about {w} is {def}.";
                    contextSb_web.AppendLine(line);
                    insertedCount++;
                    LogToFile($"[AskGPTWebhook] INFO: Inserted context for keyword '{w}' -> \"{def}\"", "INFO");
                }

                if (userDict_web.TryGetValue(w, out var profile))
                {
                    if (profile["Knowledge"].IsArray && profile["Knowledge"].AsArray.Count > 0)
                    {
                        var know = string.Join(", ", profile["Knowledge"].AsArray.Select(k => k.AsString));
                        var line = $"Something I remember about {w} is {know}.";
                        contextSb_web.AppendLine(line);
                        insertedCount++;
                        LogToFile($"[AskGPTWebhook] INFO: Inserted context for user '{w}' -> \"{know}\"", "INFO");
                    }
                }
            }

            contextBody = contextSb_web.ToString();
            LogToFile($"[AskGPTWebhook] DEBUG: Dynamic context assembly complete. Inserted {insertedCount} entries.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Context assembly failed: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
        }
        // --- End Dynamic Context Assembly ---
        // --- End Dynamic Context Assembly ---

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
            LogToFile($"[AskGPTWebhook] DEBUG: Using completions endpoint: {completionsUrl}", "DEBUG");
            var messages = new List<chatMessage>();
            messages.Add(new chatMessage { role = "system", content = contextBody });
            messages.Add(new chatMessage { role = "user", content = $"{prompt} You must respond in less than 500 characters." });
            completionsRequestJSON = JsonConvert.SerializeObject(new { model = AIModel, messages = messages }, Formatting.Indented);
            LogToFile($"[AskGPTWebhook] DEBUG: Request JSON: {completionsRequestJSON}", "DEBUG");
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
                        // Log raw model output before any parsing or cleaning
                        LogToFile($"[AskGPTWebhook] INFO: Raw model output: {completionsResponseContent}", "INFO");
                        LogToFile($"[AskGPTWebhook] DEBUG: Response JSON: {completionsResponseContent}", "DEBUG");
                        var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                        GPTResponse = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                        // Log parsed model output (uncleaned)
                        LogToFile($"[AskGPTWebhook] INFO: Parsed model output (uncleaned): {GPTResponse}", "INFO");
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
                    LogToFile($"[AskGPTWebhook] ERROR: OpenAI API request failed (attempt {attempt + 1}/{maxAttempts}). HTTP Status: {statusCode}. Response: {respBody}", "ERROR");
                    LogToFile($"[AskGPTWebhook] Context: completionsUrl='{completionsUrl}', characterNumber={characterNumber}, attempt={attempt + 1}", "ERROR");
                    if (attempt < maxAttempts - 1)
                    {
                        LogToFile($"[AskGPTWebhook] WARN: Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
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
                    LogToFile($"[AskGPTWebhook] ERROR: An error occurred during OpenAI API call (attempt {attempt + 1}/{maxAttempts}): {ex.Message}", "ERROR");
                    LogToFile($"[AskGPTWebhook] Context: completionsUrl='{completionsUrl}', characterNumber={characterNumber}, attempt={attempt + 1}", "ERROR");
                    if (attempt < maxAttempts - 1)
                    {
                        LogToFile($"[AskGPTWebhook] WARN: Retrying OpenAI API request after {backoffSeconds[attempt]}s...", "WARN");
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
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: API request/response failed: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[AskGPTWebhook] Context: characterNumber={characterNumber}", "ERROR");
            return false;
        }
        finally
        {
            sw.Stop();
            LogToFile($"[AskGPTWebhook] DEBUG: OpenAI API call completed in {sw.ElapsedMilliseconds} ms.", "DEBUG");
        }

        GPTResponse = CleanAIText(GPTResponse);
        LogToFile("[AskGPTWebhook] DEBUG: Applied CleanAIText() to GPT response.", "DEBUG");
        // Log cleaned model output before any output occurs
        LogToFile($"[AskGPTWebhook] INFO: Cleaned model output: {GPTResponse}", "INFO");
        if (!apiSuccess || string.IsNullOrWhiteSpace(GPTResponse))
        {
            if (!apiSuccess)
            {
                string msg = apiFailMsg ?? "OpenAI API request failed after multiple attempts.";
                LogToFile($"[AskGPTWebhook] ERROR: {msg}", "ERROR");
                if (apiFailStatus != -1 || !string.IsNullOrEmpty(apiFailRespBody))
                    LogToFile($"[AskGPTWebhook] ERROR: Final OpenAI API failure. HTTP Status: {apiFailStatus}. Response: {(apiFailRespBody != null && apiFailRespBody.Length > 300 ? apiFailRespBody.Substring(0, 300) + "..." : apiFailRespBody)}", "ERROR");
                string chatMsg = "GPT API unavailable";
                if (postToChat)
                    CPH.SendMessage(chatMsg, true);
                else
                    LogToFile($"[AskGPTWebhook] [Skipped Chat Output] Post To Chat disabled. Message: {chatMsg}", "DEBUG");
            }
            else
            {
                LogToFile("[AskGPTWebhook] ERROR: GPT model did not return a response.", "ERROR");
                string chatMsg = "I'm sorry, but I can't answer that question right now. Please check the log for details.";
                if (postToChat)
                    CPH.SendMessage(chatMsg, true);
                else
                    LogToFile($"[AskGPTWebhook] [Skipped Chat Output] Post To Chat disabled. Message: {chatMsg}", "DEBUG");
            }

            if (allUserProfiles != null) { allUserProfiles.Clear(); allUserProfiles = null; }
            if (keywordDocs != null) { keywordDocs.Clear(); keywordDocs = null; }
            LogToFile("==== End AskGPTWebhook Execution ====", "DEBUG");
            return false;
        }
        LogToFile($"[AskGPTWebhook] INFO: Model response: {GPTResponse}", "INFO");
        try
        {
            CPH.SetGlobalVar("Response", GPTResponse, true);
            LogToFile("[AskGPTWebhook] DEBUG: Stored GPT response in global variable 'Response'.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Failed to store GPT response in global variable: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
        }

        try
        {
            var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
            if (apiSuccess && completionsJsonResponse?.Usage != null)
            {
                LogPromptScorecard(
                    "AskGPTWebhook",
                    CPH.GetGlobalVar<string>("OpenAI Model", true),
                    completionsJsonResponse.Usage
                );
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Failed to log prompt scorecard: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
        }

        string outboundWebhookUrl = null;
        string outboundWebhookMode = null;
        bool webhookSuccess = true;
        string webhookFailMsg = null;
        string webhookFailRespBody = null;
        int webhookFailStatus = -1;
        System.Diagnostics.Stopwatch swWebhook = new System.Diagnostics.Stopwatch();
        try
        {
            outboundWebhookUrl = CPH.GetGlobalVar<string>("outbound_webhook_url", true);
            if (string.IsNullOrWhiteSpace(outboundWebhookUrl))
                outboundWebhookUrl = "https://api.openai.com/v1/chat/completions";
            outboundWebhookMode = CPH.GetGlobalVar<string>("outbound_webhook_mode", true);
            swWebhook.Start();
            if ((outboundWebhookMode ?? "").Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                LogToFile("[AskGPTWebhook] DEBUG: Outbound webhook mode is set to 'Disabled'. Skipping webhook.", "DEBUG");
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
                LogToFile($"[AskGPTWebhook] DEBUG: Sending outbound webhook payload: {payload}", "DEBUG");
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
                            LogToFile("[AskGPTWebhook] DEBUG: Outbound webhook POST successful.", "DEBUG");
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
                        LogToFile($"[AskGPTWebhook] ERROR: Failed to POST outbound webhook (attempt {attempt + 1}/{maxWebhookAttempts}). HTTP Status: {status}. Response: {respBody}", "ERROR");
                        LogToFile($"[AskGPTWebhook] Context: outboundWebhookUrl='{outboundWebhookUrl}', outboundWebhookMode='{outboundWebhookMode}', attempt={attempt + 1}", "ERROR");
                        if (attempt < maxWebhookAttempts - 1)
                        {
                            LogToFile($"[AskGPTWebhook] WARN: Retrying outbound webhook POST after {backoffSeconds[attempt]}s...", "WARN");
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
                        LogToFile($"[AskGPTWebhook] ERROR: Failed to POST outbound webhook (attempt {attempt + 1}/{maxWebhookAttempts}): {ex.Message}", "ERROR");
                        LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
                        LogToFile($"[AskGPTWebhook] Context: outboundWebhookUrl='{outboundWebhookUrl}', outboundWebhookMode='{outboundWebhookMode}', attempt={attempt + 1}", "ERROR");
                        if (attempt < maxWebhookAttempts - 1)
                        {
                            LogToFile($"[AskGPTWebhook] WARN: Retrying outbound webhook POST after {backoffSeconds[attempt]}s...", "WARN");
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
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Webhook handling failed: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[AskGPTWebhook] Context: outboundWebhookUrl='{outboundWebhookUrl}', outboundWebhookMode='{outboundWebhookMode}'", "ERROR");
        }
        finally
        {
            swWebhook.Stop();
            LogToFile($"[AskGPTWebhook] DEBUG: Webhook POST (if attempted) completed in {swWebhook.ElapsedMilliseconds} ms.", "DEBUG");
        }
        if (!webhookSuccess)
        {
            if (webhookFailMsg != null)
            {
                LogToFile($"[AskGPTWebhook] ERROR: {webhookFailMsg}", "ERROR");
                if (webhookFailStatus != -1 || !string.IsNullOrEmpty(webhookFailRespBody))
                    LogToFile($"[AskGPTWebhook] ERROR: Final webhook failure. HTTP Status: {webhookFailStatus}. Response: {(webhookFailRespBody != null && webhookFailRespBody.Length > 300 ? webhookFailRespBody.Substring(0, 300) + "..." : webhookFailRespBody)}", "ERROR");
                string chatMsg = "Webhook delivery failed after 3 attempts";
                if (postToChat)
                    CPH.SendMessage(chatMsg, true);
                else
                    LogToFile($"[AskGPTWebhook] [Skipped Chat Output] Post To Chat disabled. Message: {chatMsg}", "DEBUG");
            }
        }

        try
        {
            if (voiceEnabled)
            {
                CPH.TtsSpeak(voiceAlias, GPTResponse, false);
                LogToFile($"[AskGPTWebhook] DEBUG: Character {characterNumber} spoke GPT's response.", "DEBUG");
            }
            else
            {
                LogToFile($"[AskGPTWebhook] DEBUG: [Skipped TTS Output] Voice disabled. Message: {GPTResponse}", "DEBUG");
            }

            if (postToChat)
            {
                CPH.SendMessage(GPTResponse, true);
                LogToFile("[AskGPTWebhook] DEBUG: Sent GPT response to chat.", "DEBUG");
            }
            else
            {
                LogToFile($"[AskGPTWebhook] DEBUG: [Skipped Chat Output] Post To Chat disabled. Message: {GPTResponse}", "DEBUG");
            }
            bool logDiscord = false;
            try
            {
                logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
            }
            catch (Exception ex)
            {
                LogToFile($"[AskGPTWebhook] DEBUG: Could not get 'Log GPT Questions to Discord' global: {ex.Message}", "DEBUG");
            }
            if (logDiscord)
            {
                PostToDiscord(prompt, GPTResponse);
                LogToFile("[AskGPTWebhook] DEBUG: Posted GPT result to Discord.", "DEBUG");
            }
            CPH.SetGlobalVar("character", 1, true);
            LogToFile("[AskGPTWebhook] DEBUG: Reset 'character' global to 1 after AskGPTWebhook.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[AskGPTWebhook] ERROR: Output (chat/webhook/TTS) failed: {ex.Message}", "ERROR");
            LogToFile($"[AskGPTWebhook] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[AskGPTWebhook] Context: voiceEnabled={voiceEnabled}, postToChat={postToChat}, characterNumber={characterNumber}, voiceAlias='{voiceAlias}'", "ERROR");
            return false;
        }

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
        // Cleanup for dynamic context assembly collections
        if (profileDocs_web != null)
        {
            profileDocs_web.Clear();
            profileDocs_web = null;
        }
        if (keywordDocs_web != null)
        {
            keywordDocs_web.Clear();
            keywordDocs_web = null;
        }
        LogToFile("[AskGPTWebhook] DEBUG: Cleared user and keyword collections from memory.", "DEBUG");
        LogToFile("==== End AskGPTWebhook Execution ====", "DEBUG");
        return true;
    }

    private string CleanAIText(string text)
    {
        LogToFile("Entering CleanAIText method.", "DEBUG");
        LogToFile($"Original text: {text}", "DEBUG");

        string mode = null;
        try
        {
            mode = CPH.GetGlobalVar<string>("Text Clean Mode", true);
            LogToFile($"Text Clean Mode: {mode}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[CleanAIText] ERROR: Exception retrieving Text Clean Mode: {ex.Message}", "ERROR");
            LogToFile($"[CleanAIText] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[CleanAIText] Context: inputLength={text?.Length ?? 0}", "ERROR");
            return text ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            LogToFile("Input text is null or whitespace.", "DEBUG");
            return string.Empty;
        }

        if (text.Length > 10000)
            LogToFile("Warning: unusually long input to CleanAIText.", "WARN");

        string cleaned = text;
        try
        {
            switch ((mode ?? "").Trim())
            {
                case "Off":
                    try
                    {
                        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
                        LogToFile($"Text Clean Mode is Off. Returning original text: {cleaned}", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during whitespace normalization (Off): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    return cleaned;

                case "StripEmojis":
                    try
                    {
                        var sbEmoji = new System.Text.StringBuilder();
                        foreach (var ch in cleaned)
                        {
                            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                            if (uc != UnicodeCategory.OtherSymbol && uc != UnicodeCategory.Surrogate && uc != UnicodeCategory.NonSpacingMark)
                                sbEmoji.Append(ch);
                        }
                        cleaned = sbEmoji.ToString();
                        LogToFile("Stripped emojis from text.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during emoji stripping: {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    break;

                case "HumanFriendly":
                    try
                    {
                        string citationPattern = @"\s*\(\[.*?\]\(https?:\/\/[^\)]+\)\)";
                        cleaned = Regex.Replace(cleaned, citationPattern, "").Trim();
                        LogToFile("Removed markdown-style citations from text.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during citation removal (HumanFriendly): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    try
                    {
                        cleaned = cleaned.Normalize(NormalizationForm.FormD);
                        LogToFile("Applied Unicode normalization (FormD).", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during normalization (HumanFriendly): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    try
                    {
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
                        LogToFile("Replaced common typographic punctuation.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during punctuation normalization (HumanFriendly): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    try
                    {
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
                        LogToFile("Filtered text to human-friendly characters.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during human character filtering (HumanFriendly): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    try
                    {
                        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
                        LogToFile("Collapsed multiple spaces after human-friendly filtering.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during whitespace collapse (HumanFriendly): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    break;

                case "Strict":
                    try
                    {
                        string citationPatternStrict = @"\s*\(\[.*?\]\(https?:\/\/[^\)]+\)\)";
                        cleaned = Regex.Replace(cleaned, citationPatternStrict, "").Trim();
                        LogToFile("Removed markdown-style citations from text.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during citation removal (Strict): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    try
                    {
                        var sbStrict = new System.Text.StringBuilder();
                        foreach (var ch in cleaned)
                        {
                            if (char.IsLetterOrDigit(ch) || " .!?,;:'()\"".Contains(ch))
                                sbStrict.Append(ch);
                        }
                        cleaned = sbStrict.ToString();
                        LogToFile("Filtered text to strict character set.", "DEBUG");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during strict filtering: {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    break;

                default:
                    LogToFile($"Unknown Text Clean Mode '{mode}'. Defaulting to 'Off'.", "DEBUG");
                    try
                    {
                        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[CleanAIText] ERROR: Exception during whitespace normalization (default): {ex.Message}", "ERROR");
                        LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
                        return text ?? string.Empty;
                    }
                    return cleaned;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[CleanAIText] ERROR: Exception during text cleaning: {ex.Message}", "ERROR");
            LogToFile($"[CleanAIText] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
            return text ?? string.Empty;
        }

        string beforeFinal = cleaned;
        try
        {
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            LogToFile($"Text before whitespace normalization: {beforeFinal}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[CleanAIText] ERROR: Exception during final whitespace normalization: {ex.Message}", "ERROR");
            LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");
            return beforeFinal ?? text ?? string.Empty;
        }

        if ((mode ?? "").Trim() == "HumanFriendly")
        {
            try
            {
                string beforeDashNorm = cleaned;
                cleaned = cleaned.Replace(" - ", " ");
                cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
                LogToFile("Applied final dash/space normalization in CleanAIText().", "DEBUG");
            }
            catch (Exception ex)
            {
                LogToFile($"[CleanAIText] ERROR: Exception during dash/space normalization (HumanFriendly): {ex.Message}", "ERROR");
                LogToFile($"[CleanAIText] Context: mode='{mode}', inputLength={text?.Length ?? 0}", "ERROR");

            }
        }

        LogToFile($"Text after cleaning: {cleaned}", "DEBUG");
        LogToFile("Exiting CleanAIText method.", "DEBUG");
        return cleaned;
    }

    public string GenerateChatCompletion(string prompt, string contextBody)
    {
        LogToFile("[GenerateChatCompletion] DEBUG: Entering method.", "DEBUG");
        string generatedText = string.Empty;
        string completionsRequestJSON = null;
        string completionsResponseContent = null;
        string apiKey = null;
        string voiceAlias = null;
        string AIModel = null;
        string completionsUrl = null;
        try
        {

            apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
            voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);
            LogToFile($"[GenerateChatCompletion] DEBUG: Voice Alias: {voiceAlias}, AI Model: {AIModel}", "DEBUG");
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(AIModel))
            {
                LogToFile("[GenerateChatCompletion] ERROR: One or more configuration values are missing or invalid. Please check the OpenAI API Key, Voice Alias, and AI Model settings.", "ERROR");
                LogToFile($"[GenerateChatCompletion] Context: apiKey='{apiKey}', voiceAlias='{voiceAlias}', AIModel='{AIModel}'", "ERROR");
                return "Configuration error. Please check the log for details.";
            }
            LogToFile("[GenerateChatCompletion] DEBUG: All configuration values are valid and present.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: Exception retrieving configuration: {ex.Message}", "ERROR");
            LogToFile($"[GenerateChatCompletion] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GenerateChatCompletion] Context: apiKey='{apiKey}', voiceAlias='{voiceAlias}', AIModel='{AIModel}'", "ERROR");
            return "Configuration error. Please check the log for details.";
        }
        try
        {
            completionsUrl = CPH.GetGlobalVar<string>("openai_completions_url", true);
            if (string.IsNullOrWhiteSpace(completionsUrl))
                completionsUrl = "https://api.openai.com/v1/chat/completions";
            LogToFile("[GenerateChatCompletion] DEBUG: All configuration values are valid and present.", "DEBUG");
            LogToFile($"[GenerateChatCompletion] DEBUG: Using completions endpoint: {completionsUrl}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: Exception retrieving completionsUrl: {ex.Message}", "ERROR");
            LogToFile($"[GenerateChatCompletion] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GenerateChatCompletion] Context: completionsUrl='{completionsUrl}'", "ERROR");
            return "Configuration error. Please check the log for details.";
        }
        List<chatMessage> messages = null;
        try
        {
            messages = new List<chatMessage>
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
            completionsRequestJSON = JsonConvert.SerializeObject(new { model = AIModel, messages = messages }, Formatting.Indented);
            LogToFile($"[GenerateChatCompletion] DEBUG: Request JSON: {completionsRequestJSON}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: Exception assembling messages/request JSON: {ex.Message}", "ERROR");
            LogToFile($"[GenerateChatCompletion] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GenerateChatCompletion] Context: AIModel='{AIModel}', prompt='{prompt}', contextBody.length={contextBody?.Length ?? 0}", "ERROR");
            return "Internal error preparing request. See log.";
        }
        WebRequest completionsWebRequest = null;
        try
        {
            completionsWebRequest = WebRequest.Create(completionsUrl);
            completionsWebRequest.Method = "POST";
            completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            completionsWebRequest.ContentType = "application/json";
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: Exception creating WebRequest: {ex.Message}", "ERROR");
            LogToFile($"[GenerateChatCompletion] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GenerateChatCompletion] Context: completionsUrl='{completionsUrl}'", "ERROR");
            return "Internal error preparing request. See log.";
        }
        try
        {
            LogToFile("[GenerateChatCompletion] DEBUG: Writing request stream...", "DEBUG");
            using (Stream requestStream = completionsWebRequest.GetRequestStream())
            {
                byte[] completionsContentBytes = Encoding.UTF8.GetBytes(completionsRequestJSON);
                requestStream.Write(completionsContentBytes, 0, completionsContentBytes.Length);
            }
            LogToFile("[GenerateChatCompletion] DEBUG: Request stream written.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: Exception writing request stream: {ex.Message}", "ERROR");
            LogToFile($"[GenerateChatCompletion] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GenerateChatCompletion] Context: completionsUrl='{completionsUrl}', completionsRequestJSON.length={completionsRequestJSON?.Length ?? 0}", "ERROR");
            return "Internal error sending request. See log.";
        }
        try
        {
            LogToFile("[GenerateChatCompletion] DEBUG: Awaiting API response...", "DEBUG");
            using (WebResponse completionsWebResponse = completionsWebRequest.GetResponse())
            {
                using (StreamReader responseReader = new StreamReader(completionsWebResponse.GetResponseStream()))
                {
                    completionsResponseContent = responseReader.ReadToEnd();
                    LogToFile($"[GenerateChatCompletion] DEBUG: Response JSON: {completionsResponseContent}", "DEBUG");
                    var completionsJsonResponse = JsonConvert.DeserializeObject<ChatCompletionsResponse>(completionsResponseContent);
                    generatedText = completionsJsonResponse?.Choices?.FirstOrDefault()?.Message?.content ?? string.Empty;
                    LogToFile("[GenerateChatCompletion] INFO: Successfully received and parsed response.", "INFO");
                }
            }
        }
        catch (WebException webEx)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: WebException during API request: {webEx.Message}", "ERROR");
            if (webEx.Response != null)
            {
                try
                {
                    using (var reader = new StreamReader(webEx.Response.GetResponseStream()))
                    {
                        string errorResp = reader.ReadToEnd();
                        LogToFile($"[GenerateChatCompletion] ERROR: WebException Response: {errorResp}", "ERROR");
                        LogToFile($"[GenerateChatCompletion] Context: completionsUrl='{completionsUrl}', AIModel='{AIModel}', completionsRequestJSON.length={completionsRequestJSON?.Length ?? 0}", "ERROR");
                    }
                }
                catch (Exception ex2)
                {
                    LogToFile($"[GenerateChatCompletion] WARN: Failed to read WebException response stream: {ex2.Message}", "WARN");
                }
            }
            LogToFile($"[GenerateChatCompletion] Stack: {webEx.StackTrace}", "DEBUG");
            return "ChatGPT did not return a response.";
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] ERROR: Exception during API request: {ex.Message}", "ERROR");
            LogToFile($"[GenerateChatCompletion] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[GenerateChatCompletion] Context: completionsUrl='{completionsUrl}', AIModel='{AIModel}', completionsRequestJSON.length={completionsRequestJSON?.Length ?? 0}", "ERROR");
            return "ChatGPT did not return a response.";
        }
        if (string.IsNullOrEmpty(generatedText))
        {
            generatedText = "ChatGPT did not return a response.";
            LogToFile("[GenerateChatCompletion] ERROR: The GPT model did not return any text.", "ERROR");
            LogToFile($"[GenerateChatCompletion] Context: completionsUrl='{completionsUrl}', AIModel='{AIModel}', completionsResponseContent.length={completionsResponseContent?.Length ?? 0}", "ERROR");
        }
        else
        {
            generatedText = generatedText.Replace("\r\n", " ").Replace("\n", " ");
            LogToFile($"[GenerateChatCompletion] INFO: Prompt: {prompt}", "INFO");
            LogToFile($"[GenerateChatCompletion] INFO: Response: {generatedText}", "INFO");
        }
        try
        {
            QueueGPTMessage(prompt, generatedText);
            LogToFile("[GenerateChatCompletion] DEBUG: Queued GPT message for logging/history.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] WARN: Exception in QueueGPTMessage: {ex.Message}", "WARN");
            LogToFile($"[GenerateChatCompletion] Context: prompt.length={prompt?.Length ?? 0}, generatedText.length={generatedText?.Length ?? 0}", "WARN");
        }
        try
        {
            PostToDiscord(prompt, generatedText);
            LogToFile("[GenerateChatCompletion] DEBUG: Posted to Discord.", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[GenerateChatCompletion] WARN: Exception posting to Discord: {ex.Message}", "WARN");
            LogToFile($"[GenerateChatCompletion] Context: prompt.length={prompt?.Length ?? 0}, generatedText.length={generatedText?.Length ?? 0}", "WARN");
        }
        LogToFile("[GenerateChatCompletion] DEBUG: Exiting method.", "DEBUG");
        return generatedText;
    }

    private void PostToDiscord(string question, string answer)
    {
        LogToFile(">>> Entering PostToDiscord() – Preparing to post GPT question/answer pair to Discord webhook.", "DEBUG");

        bool logDiscord = false;
        try
        {
            logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
            LogToFile($"[PostToDiscord] DEBUG: Retrieved 'Log GPT Questions to Discord'={logDiscord}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[PostToDiscord] ERROR: Failed to retrieve 'Log GPT Questions to Discord' global: {ex.Message}", "ERROR");
            LogToFile($"[PostToDiscord] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[PostToDiscord] Context: questionLength={question?.Length ?? 0}, answerLength={answer?.Length ?? 0}", "ERROR");
            return;
        }

        if (!logDiscord)
        {
            LogToFile("[PostToDiscord] INFO: Posting to Discord is disabled by global setting. Skipping Discord webhook post.", "INFO");
            LogToFile("<<< Exiting PostToDiscord() – Discord logging disabled.", "DEBUG");
            return;
        }

        string discordWebhookUrl = null;
        string discordUsername = null;
        string discordAvatarUrl = null;
        try
        {
            discordWebhookUrl = CPH.GetGlobalVar<string>("Discord Webhook URL", true);
            discordUsername = CPH.GetGlobalVar<string>("Discord Bot Username", true);
            discordAvatarUrl = CPH.GetGlobalVar<string>("Discord Avatar Url", true);
            LogToFile("[PostToDiscord] DEBUG: Retrieved Discord webhook configuration (URL, Username, Avatar).", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[PostToDiscord] ERROR: Failed to retrieve Discord webhook configuration: {ex.Message}", "ERROR");
            LogToFile($"[PostToDiscord] Stack: {ex.StackTrace}", "DEBUG");
            return;
        }

        string discordOutput = $"Question: {question}\nAnswer: {answer}";
        LogToFile($"[PostToDiscord] INFO: Preparing to post message to Discord. Payload length={discordOutput.Length}.", "INFO");

        try
        {
            LogToFile("[PostToDiscord] DEBUG: Sending message to Discord webhook.", "DEBUG");
            CPH.DiscordPostTextToWebhook(discordWebhookUrl, discordOutput, discordUsername, discordAvatarUrl, false);
            LogToFile("[PostToDiscord] INFO: Successfully posted GPT Q/A message to Discord webhook.", "INFO");
        }
        catch (Exception ex)
        {
            LogToFile($"[PostToDiscord] ERROR: Failed to post GPT Q/A message to Discord webhook: {ex.Message}", "ERROR");
            LogToFile($"[PostToDiscord] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[PostToDiscord] Context: discordWebhookUrl='{discordWebhookUrl}', username='{discordUsername}', avatarUrl='{discordAvatarUrl}'", "ERROR");
        }

        LogToFile("<<< Exiting PostToDiscord() – Operation complete.", "DEBUG");
    }

    public bool GetStreamInfo()
    {
        LogToFile(">>> Entering GetStreamInfo() – Retrieving Twitch stream information.", "DEBUG");
        try
        {
            Task<AllDatas> getAllDatasTask = FunctionGetAllDatas();
            getAllDatasTask.Wait();
            AllDatas datas = getAllDatasTask.Result;

            if (datas != null)
            {
                CPH.SetGlobalVar("broadcaster", datas.UserName, false);
                CPH.SetGlobalVar("currentGame", datas.gameName, false);
                CPH.SetGlobalVar("currentTitle", datas.titleName, false);

                LogToFile($"[GetStreamInfo] INFO: Retrieved stream data successfully. R=Retrieve stream info, A=Set globals, P=User='{datas.UserName}', I=Globals updated, D=Success.", "INFO");
                LogToFile("<<< Exiting GetStreamInfo() – Completed successfully.", "DEBUG");
                return true;
            }
            else
            {
                LogToFile("[GetStreamInfo] ERROR: Twitch API returned no stream data. R=Retrieve stream info, A=Process FunctionGetAllDatas result, P=None, I=Should have valid AllDatas, D=Null result.", "ERROR");
                LogToFile("<<< Exiting GetStreamInfo() – Failure.", "DEBUG");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[GetStreamInfo] ERROR: Failed to retrieve stream information: {ex.Message}", "ERROR");
            LogToFile($"[GetStreamInfo] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("[GetStreamInfo] Context: Unexpected exception during FunctionGetAllDatas() or global variable assignment.", "ERROR");
            LogToFile("<<< Exiting GetStreamInfo() – Fatal error encountered.", "DEBUG");
            return false;
        }
    }

    public async Task<AllDatas> FunctionGetAllDatas()
    {
        LogToFile(">>> Entering FunctionGetAllDatas() – Preparing Twitch API call to retrieve channel metadata.", "DEBUG");
        string broadcasterId = null;
        string twitchApiEndpoint = null;
        string clientIdValue = null;
        string tokenValue = null;

        try
        {
            broadcasterId = args["broadcastUserId"]?.ToString();
            twitchApiEndpoint = $"https://api.twitch.tv/helix/channels?broadcaster_id={broadcasterId}";
            clientIdValue = CPH.TwitchClientId;
            tokenValue = CPH.TwitchOAuthToken;

            LogToFile($"[FunctionGetAllDatas] DEBUG: Constructed Twitch API endpoint: {twitchApiEndpoint}, ClientID={clientIdValue?.Substring(0, Math.Min(clientIdValue.Length, 6))}****", "DEBUG");

            WebRequest request = WebRequest.Create(twitchApiEndpoint);
            request.Method = "GET";
            request.Headers.Add("Client-ID", clientIdValue);
            request.Headers.Add("Authorization", "Bearer " + tokenValue);
            request.ContentType = "application/json";

            try
            {
                using (WebResponse response = await request.GetResponseAsync())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseBody = await reader.ReadToEndAsync();
                    LogToFile($"[FunctionGetAllDatas] DEBUG: Twitch API response body: {responseBody}", "DEBUG");

                    Root root = JsonConvert.DeserializeObject<Root>(responseBody);
                    var result = new AllDatas
                    {
                        UserName = root.data[0].broadcaster_name,
                        gameName = root.data[0].game_name,
                        titleName = root.data[0].title,
                    };

                    LogToFile($"[FunctionGetAllDatas] INFO: Successfully parsed Twitch API response. R=Fetch Twitch channel info, A=Deserialize JSON, P=BroadcasterId={broadcasterId}, I=Expect valid data, D=Received.", "INFO");
                    LogToFile("<<< Exiting FunctionGetAllDatas() – Success.", "DEBUG");
                    return result;
                }
            }
            catch (WebException webEx)
            {
                LogToFile($"[FunctionGetAllDatas] ERROR: WebException during Twitch API request: {webEx.Message}", "ERROR");
                LogToFile($"[FunctionGetAllDatas] Stack: {webEx.StackTrace}", "DEBUG");

                if (webEx.Response != null)
                {
                    using (StreamReader reader = new StreamReader(webEx.Response.GetResponseStream()))
                    {
                        string errorBody = reader.ReadToEnd();
                        LogToFile($"[FunctionGetAllDatas] ERROR: Twitch API response body on failure: {errorBody}", "ERROR");
                    }
                }
                LogToFile($"[FunctionGetAllDatas] Context: Endpoint={twitchApiEndpoint}, BroadcasterId={broadcasterId}", "ERROR");
                LogToFile("<<< Exiting FunctionGetAllDatas() – WebException occurred.", "DEBUG");
                return null;
            }
            catch (Exception ex)
            {
                LogToFile($"[FunctionGetAllDatas] ERROR: General exception during Twitch API call: {ex.Message}", "ERROR");
                LogToFile($"[FunctionGetAllDatas] Stack: {ex.StackTrace}", "DEBUG");
                LogToFile($"[FunctionGetAllDatas] Context: Endpoint={twitchApiEndpoint}, BroadcasterId={broadcasterId}", "ERROR");
                LogToFile("<<< Exiting FunctionGetAllDatas() – General failure.", "DEBUG");
                return null;
            }
        }
        catch (Exception exOuter)
        {
            LogToFile($"[FunctionGetAllDatas] ERROR: Failed to prepare Twitch API request: {exOuter.Message}", "ERROR");
            LogToFile($"[FunctionGetAllDatas] Stack: {exOuter.StackTrace}", "DEBUG");
            LogToFile($"[FunctionGetAllDatas] Context: broadcasterId='{broadcasterId}', endpoint='{twitchApiEndpoint}'", "ERROR");
            LogToFile("<<< Exiting FunctionGetAllDatas() – Fatal preparation error.", "DEBUG");
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
            { "ERROR", 1 },
            { "WARN", 2 },
            { "INFO", 3 },
            { "DEBUG", 4 }
        };

        if (logLevelPriority[logLevel] <= logLevelPriority[globalLogLevel])
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
        LogToFile(">>> Entering ClearLogFile() – Attempting to clear today's log file.", "DEBUG");
        string databasePath = null;
        string logDirectoryPath = null;
        string logFilePath = null;
        string logFileName = null;

        try
        {
            databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrEmpty(databasePath))
            {
                LogToFile("[ClearLogFile] ERROR: The 'Database Path' global variable is missing or empty. R=Get Database Path, A=Validate variable, P=Database Path=null, I=Path required, D=Failed.", "ERROR");
                LogToFile("<<< Exiting ClearLogFile() – Failure (missing database path).", "DEBUG");
                return false;
            }

            logDirectoryPath = Path.Combine(databasePath, "logs");
            if (!Directory.Exists(logDirectoryPath))
            {
                LogToFile("[ClearLogFile] INFO: The 'logs' directory does not exist. No log file to clear. R=Verify directory, A=Check Directory.Exists, P=Path=" + logDirectoryPath + ", I=Should exist, D=Not found.", "INFO");
                LogToFile("<<< Exiting ClearLogFile() – No logs directory present.", "DEBUG");
                return false;
            }

            logFileName = DateTime.Now.ToString("PNGTuber-GPT_yyyyMMdd") + ".log";
            logFilePath = Path.Combine(logDirectoryPath, logFileName);

            if (!File.Exists(logFilePath))
            {
                LogToFile("[ClearLogFile] INFO: No log file exists for the current day to clear. R=Check File.Exists, A=Verify file presence, P=FilePath=" + logFilePath + ", I=File exists, D=Not found.", "INFO");
                CPH.SendMessage("No log file exists for the current day to clear.", true);
                LogToFile("<<< Exiting ClearLogFile() – No log file found.", "DEBUG");
                return false;
            }

            try
            {
                File.WriteAllText(logFilePath, string.Empty);
                LogToFile($"[ClearLogFile] INFO: Cleared the log file successfully. R=Clear file, A=Overwrite with empty content, P=File={logFileName}, I=File cleared, D=Success.", "INFO");
                CPH.SendMessage($"Cleared the log file.", true);
                LogToFile("<<< Exiting ClearLogFile() – Completed successfully.", "DEBUG");
                return true;
            }
            catch (Exception exFile)
            {
                LogToFile($"[ClearLogFile] ERROR: Failed to clear log file: {exFile.Message}", "ERROR");
                LogToFile($"[ClearLogFile] Stack: {exFile.StackTrace}", "DEBUG");
                LogToFile($"[ClearLogFile] Context: logFilePath='{logFilePath}', databasePath='{databasePath}'", "ERROR");
                CPH.SendMessage("An error occurred while clearing the log file.", true);
                LogToFile("<<< Exiting ClearLogFile() – File write failure.", "DEBUG");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"[ClearLogFile] ERROR: Unexpected exception: {ex.Message}", "ERROR");
            LogToFile($"[ClearLogFile] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[ClearLogFile] Context: databasePath='{databasePath}', logDirectoryPath='{logDirectoryPath}', logFilePath='{logFilePath}'", "ERROR");
            CPH.SendMessage("An unexpected error occurred while clearing the log file.", true);
            LogToFile("<<< Exiting ClearLogFile() – Fatal exception encountered.", "DEBUG");
            return false;
        }
    }

    public bool Version()
    {
        LogToFile(">>> Entering Version() – Attempting to retrieve and display version number.", "DEBUG");
        string versionNumber = null;

        try
        {
            versionNumber = CPH.GetGlobalVar<string>("Version", true);
            LogToFile($"[Version] DEBUG: Retrieved global variable 'Version' with value: {versionNumber}", "DEBUG");

            if (string.IsNullOrWhiteSpace(versionNumber))
            {
                LogToFile("[Version] ERROR: The 'Version' global variable is missing or empty. R=Retrieve version, A=Validate variable, P=Version=null, I=Version must exist, D=Failed.", "ERROR");
                LogToFile("<<< Exiting Version() – Failure (version missing).", "DEBUG");
                return false;
            }

            LogToFile($"[Version] INFO: Sending version number '{versionNumber}' to chat. R=Display version, A=SendMessage, P=Version={versionNumber}, I=User informed, D=Success.", "INFO");
            CPH.SendMessage(versionNumber, true);

            LogToFile($"[Version] INFO: Version number '{versionNumber}' sent to chat successfully.", "INFO");
            LogToFile("<<< Exiting Version() – Completed successfully.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[Version] ERROR: Failed to retrieve or send version: {ex.Message}", "ERROR");
            LogToFile($"[Version] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[Version] Context: versionNumber='{versionNumber ?? "null"}'", "ERROR");
            LogToFile("<<< Exiting Version() – Fatal error encountered.", "DEBUG");
            return false;
        }
    }

    public bool SayPlay()
    {
        LogToFile(">>> Entering SayPlay() – Preparing to trigger '!play' command in chat.", "DEBUG");

        bool postToChat = false;
        try
        {
            postToChat = CPH.GetGlobalVar<bool>("Post To Chat", true);
            LogToFile($"[SayPlay] DEBUG: Retrieved global variable 'Post To Chat'={postToChat}", "DEBUG");
        }
        catch (Exception ex)
        {
            LogToFile($"[SayPlay] ERROR: Failed to retrieve 'Post To Chat' global variable: {ex.Message}", "ERROR");
            LogToFile($"[SayPlay] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[SayPlay] Context: postToChat={postToChat}", "ERROR");
            LogToFile("<<< Exiting SayPlay() – Fatal error retrieving global variable.", "DEBUG");
            return false;
        }

        try
        {
            if (postToChat)
            {
                CPH.SendMessage("!play", true);
                LogToFile("[SayPlay] INFO: Sent '!play' command to chat. R=Trigger chat play, A=SendMessage, P=Message='!play', I=User sees play command, D=Success.", "INFO");
            }
            else
            {
                LogToFile("[SayPlay] INFO: 'Post To Chat' is disabled. Skipping '!play' message. R=Check PostToChat, A=Skip message, P=postToChat=false, I=Prevent unwanted chat post, D=Skipped.", "INFO");
                LogToFile("[SayPlay] DEBUG: [Skipped Chat Output] Post To Chat disabled. Message: !play", "DEBUG");
            }

            LogToFile("<<< Exiting SayPlay() – Completed successfully.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[SayPlay] ERROR: Exception occurred while attempting to send '!play' message: {ex.Message}", "ERROR");
            LogToFile($"[SayPlay] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile($"[SayPlay] Context: postToChat={postToChat}", "ERROR");
            LogToFile("<<< Exiting SayPlay() – Failure during message send.", "DEBUG");
            return false;
        }
    }

    public bool SaveSettings()
    {
        LogToFile(">>> Entering SaveSettings() – Persisting all global variables into LiteDB settings collection.", "DEBUG");
        try
        {
            var settingsDict = new Dictionary<string, string>();
            try
            {
                LogToFile("[SaveSettings] DEBUG: Collecting configuration from global variables.", "DEBUG");
                settingsDict = new Dictionary<string, string>
                {
                    ["OpenAI API Key"] = EncryptData(CPH.GetGlobalVar<string>("OpenAI API Key", true)),
                    ["OpenAI Model"] = CPH.GetGlobalVar<string>("OpenAI Model", true),
                    ["Model Input Cost"] = CPH.GetGlobalVar<string>("model_input_cost", true),
                    ["Model Output Cost"] = CPH.GetGlobalVar<string>("model_output_cost", true),
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
            }
            catch (Exception exCollect)
            {
                LogToFile($"[SaveSettings] ERROR: Failed to collect one or more global variables: {exCollect.Message}", "ERROR");
                LogToFile($"[SaveSettings] Stack: {exCollect.StackTrace}", "DEBUG");
                LogToFile("[SaveSettings] Context: error occurred during global variable collection.", "ERROR");
                LogToFile("<<< Exiting SaveSettings() – Collection failure.", "DEBUG");
                return false;
            }

            string[] requiredKeys = {
                "OpenAI API Key", "OpenAI Model", "Model Input Cost", "Model Output Cost", "Database Path", "Ignore Bot Usernames",
                "Logging Level", "Version", "Discord Webhook URL", "Discord Bot Username", "Discord Avatar Url",
                "character_voice_alias_1", "character_voice_alias_2", "character_voice_alias_3", "character_voice_alias_4", "character_voice_alias_5",
                "character_file_1", "character_file_2", "character_file_3", "character_file_4", "character_file_5", "Completions Endpoint"
            };

            foreach (var key in requiredKeys)
            {
                if (!settingsDict.ContainsKey(key) || string.IsNullOrWhiteSpace(settingsDict[key]))
                {
                    LogToFile($"[SaveSettings] WARN: One or more required settings are missing or empty. R=Validate settings, A=Check requiredKeys, P=Key={key}, I=All required keys present, D=Missing.", "WARN");
                    LogToFile("<<< Exiting SaveSettings() – Required key missing.", "DEBUG");
                    return false;
                }
            }

            try
            {
                var settingsCol = _db.GetCollection<BsonDocument>("settings");
                foreach (var kvp in settingsDict)
                {
                    var existing = settingsCol.FindOne(x => x["Key"] == kvp.Key);
                    if (existing != null)
                    {
                        existing["Value"] = kvp.Value;
                        settingsCol.Update(existing);
                    }
                    else
                    {
                        settingsCol.Insert(new BsonDocument { ["Key"] = kvp.Key, ["Value"] = kvp.Value });
                    }
                    LogToFile($"[SaveSettings] DEBUG: Saved setting to DB: {kvp.Key} = {kvp.Value}", "DEBUG");
                }

                LogToFile("[SaveSettings] INFO: All settings persisted to LiteDB successfully. R=Save settings, A=Write to LiteDB, P=settingsDict.Count=" + settingsDict.Count + ", I=All settings stored, D=Success.", "INFO");
            }
            catch (Exception exDb)
            {
                LogToFile($"[SaveSettings] ERROR: Failed to write settings to LiteDB: {exDb.Message}", "ERROR");
                LogToFile($"[SaveSettings] Stack: {exDb.StackTrace}", "DEBUG");
                LogToFile("[SaveSettings] Context: settingsDict.Count=" + settingsDict.Count, "ERROR");
                LogToFile("<<< Exiting SaveSettings() – Database write failure.", "DEBUG");
                return false;
            }

            try
            {

                CPH.SetGlobalVar("voice_enabled", settingsDict["voice_enabled"], true);
                CPH.SetGlobalVar("outbound_webhook_url", settingsDict["outbound_webhook_url"], true);
                CPH.SetGlobalVar("outbound_webhook_mode", settingsDict["outbound_webhook_mode"], true);
                LogToFile("[SaveSettings] INFO: Reapplied dynamic runtime globals successfully. R=Reapply key vars, A=SetGlobalVar, I=Ensure runtime consistency, D=Success.", "INFO");
            }
            catch (Exception exSet)
            {
                LogToFile($"[SaveSettings] ERROR: Failed to reapply globals: {exSet.Message}", "ERROR");
                LogToFile($"[SaveSettings] Stack: {exSet.StackTrace}", "DEBUG");
                LogToFile("[SaveSettings] Context: key reapplication failure.", "ERROR");
            }

            LogToFile("<<< Exiting SaveSettings() – Completed successfully.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            LogToFile($"[SaveSettings] ERROR: Unexpected failure while saving settings: {ex.Message}", "ERROR");
            LogToFile($"[SaveSettings] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("[SaveSettings] Context: unexpected top-level exception in SaveSettings.", "ERROR");
            LogToFile("<<< Exiting SaveSettings() – Fatal exception encountered.", "DEBUG");
            return false;
        }
    }

    public bool ReadSettings()
    {
        LogToFile(">>> Entering ReadSettings() – Loading settings from LiteDB into global variables.", "DEBUG");

        try
        {
            var settingsCol = _db.GetCollection<BsonDocument>("settings");
            LogToFile("[ReadSettings] DEBUG: Opened LiteDB 'settings' collection successfully.", "DEBUG");

            var settings = settingsCol.FindAll().ToList();
            LogToFile($"[ReadSettings] DEBUG: Retrieved {settings.Count} settings entries from database.", "DEBUG");

            if (settings.Count == 0)
            {
                LogToFile("[ReadSettings] WARN: No settings found in LiteDB. R=Read settings, A=FindAll(), P=settings.Count=0, I=Expect populated DB, D=Empty result.", "WARN");
                LogToFile("<<< Exiting ReadSettings() – No data available.", "DEBUG");
                return false;
            }

            foreach (var setting in settings)
            {
                string key = setting["Key"];
                string value = setting["Value"];

                try
                {
                    if (key == "OpenAI API Key")
                    {
                        value = DecryptData(value);
                    }

                    bool success = false;
                    try
                    {
                        CPH.SetGlobalVar(key, value, true);
                        success = true;
                    }
                    catch
                    {

                        if (bool.TryParse(value, out bool boolValue))
                        {
                            CPH.SetGlobalVar(key, boolValue, true);
                            success = true;
                        }
                    }

                    if (success)
                        LogToFile($"[ReadSettings] DEBUG: Restored global var '{key}' with value='{value}'.", "DEBUG");
                    else
                        LogToFile($"[ReadSettings] WARN: Unable to restore key='{key}' due to unsupported type. R=SetGlobalVar, A=Assign value, P=key={key}, I=Restore all globals, D=Partial.", "WARN");
                }
                catch (Exception exKey)
                {
                    LogToFile($"[ReadSettings] ERROR: Failed to restore setting '{setting["Key"]}': {exKey.Message}", "ERROR");
                    LogToFile($"[ReadSettings] Stack: {exKey.StackTrace}", "DEBUG");
                    LogToFile($"[ReadSettings] Context: key='{setting["Key"]}', value='{setting["Value"]}'", "ERROR");
                }
            }

            LogToFile($"[ReadSettings] INFO: Successfully loaded and applied {settings.Count} settings from LiteDB. R=Load settings, A=Restore globals, I=App ready, D=Success.", "INFO");
            LogToFile("<<< Exiting ReadSettings() – Completed successfully.", "DEBUG");
            return true;
        }
        catch (LiteException liteEx)
        {
            LogToFile($"[ReadSettings] ERROR: LiteDB operation failed: {liteEx.Message}", "ERROR");
            LogToFile($"[ReadSettings] Stack: {liteEx.StackTrace}", "DEBUG");
            LogToFile("[ReadSettings] Context: LiteDB read failure during FindAll()", "ERROR");
            LogToFile("<<< Exiting ReadSettings() – LiteDB error.", "DEBUG");
            return false;
        }
        catch (Exception ex)
        {
            LogToFile($"[ReadSettings] ERROR: Unexpected exception during settings load: {ex.Message}", "ERROR");
            LogToFile($"[ReadSettings] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("[ReadSettings] Context: Unexpected failure reading settings.", "ERROR");
            LogToFile("<<< Exiting ReadSettings() – Fatal exception encountered.", "DEBUG");
            return false;
        }
    }

    private string EncryptData(string data)
    {
        LogToFile("[EncryptData] Entry: R=Encrypt data, A=Protect bytes, P=Input validation, I=Begin encryption, D=Start.", "DEBUG");

        if (string.IsNullOrWhiteSpace(data))
        {
            LogToFile("[EncryptData] ERROR: Input data is null or empty. R=Encrypt data, A=Validate input, P=data=null, I=Require valid input, D=Failed.", "ERROR");
            LogToFile("[EncryptData] Context: dataLength=0", "ERROR");
            LogToFile("<<< [EncryptData] Exit: R=Encrypt data, A=Validate input, P=data=null, I=Input invalid, D=Return empty.", "DEBUG");
            return string.Empty;
        }

        try
        {
            LogToFile("[EncryptData] DEBUG: Preparing to encrypt data using DataProtectionScope.CurrentUser. R=Encrypt data, A=Prepare bytes, P=Data present, I=Ready for encryption, D=Continue.", "DEBUG");
            byte[] inputBytes = Encoding.UTF8.GetBytes(data);
            LogToFile($"[EncryptData] DEBUG: Converted input string to {inputBytes.Length} bytes. R=Encrypt data, A=Convert to bytes, P=dataLength={inputBytes.Length}, I=Bytes ready, D=Continue.", "DEBUG");

            byte[] encryptedData = null;
            try
            {
                encryptedData = ProtectedData.Protect(inputBytes, null, DataProtectionScope.CurrentUser);
            }
            catch (Exception exProtect)
            {
                LogToFile($"[EncryptData] ERROR: Exception during ProtectedData.Protect: {exProtect.Message} R=Encrypt data, A=Protect bytes, P=dataLength={inputBytes.Length}, I=DPAPI error, D=Failed.", "ERROR");
                LogToFile($"[EncryptData] Context: dataLength={data?.Length ?? 0}", "ERROR");
                LogToFile($"[EncryptData] Stack: {exProtect.StackTrace}", "DEBUG");
                LogToFile("<<< [EncryptData] Exit: R=Encrypt data, A=Protect bytes, P=dataLength={inputBytes.Length}, I=DPAPI error, D=Return empty.", "DEBUG");
                return string.Empty;
            }
            string base64EncryptedData = Convert.ToBase64String(encryptedData);

            LogToFile("[EncryptData] DEBUG: Data encryption successful. R=Encrypt data, A=Protect bytes, P=DataLength=" + inputBytes.Length + ", I=Return base64 string, D=Success.", "DEBUG");
            LogToFile("<<< [EncryptData] Exit: R=Encrypt data, A=Protect bytes, P=DataLength=" + inputBytes.Length + ", I=Encryption complete, D=Return encrypted.", "DEBUG");
            return base64EncryptedData;
        }
        catch (CryptographicException cryptoEx)
        {
            LogToFile($"[EncryptData] ERROR: Cryptographic exception during encryption: {cryptoEx.Message} R=Encrypt data, A=Protect bytes, P=dataLength={(data?.Length ?? 0)}, I=Crypto error, D=Failed.", "ERROR");
            LogToFile($"[EncryptData] Context: dataLength={(data?.Length ?? 0)}", "ERROR");
            LogToFile($"[EncryptData] Stack: {cryptoEx.StackTrace}", "DEBUG");
            LogToFile("<<< [EncryptData] Exit: R=Encrypt data, A=Protect bytes, P=dataLength={(data?.Length ?? 0)}, I=Crypto error, D=Return empty.", "DEBUG");
            return string.Empty;
        }
        catch (Exception ex)
        {
            LogToFile($"[EncryptData] ERROR: Unexpected exception during encryption: {ex.Message} R=Encrypt data, A=Protect bytes, P=dataLength={(data?.Length ?? 0)}, I=Exception, D=Failed.", "ERROR");
            LogToFile($"[EncryptData] Context: dataLength={(data?.Length ?? 0)}", "ERROR");
            LogToFile($"[EncryptData] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("<<< [EncryptData] Exit: R=Encrypt data, A=Protect bytes, P=dataLength={(data?.Length ?? 0)}, I=Exception, D=Return empty.", "DEBUG");
            return string.Empty;
        }
    }

    private string DecryptData(string encryptedData)
    {
        LogToFile("[DecryptData] Entry: R=Decrypt data, A=Unprotect bytes, P=Input validation, I=Begin decryption, D=Start.", "DEBUG");

        if (string.IsNullOrWhiteSpace(encryptedData))
        {
            LogToFile("[DecryptData] ERROR: Input encryptedData is null or empty. R=Decrypt data, A=Validate input, P=encryptedData=null, I=Require valid input, D=Failed.", "ERROR");
            LogToFile("[DecryptData] Context: encryptedDataLength=0", "ERROR");
            LogToFile("<<< [DecryptData] Exit: R=Decrypt data, A=Validate input, P=encryptedData=null, I=Input invalid, D=Return empty.", "DEBUG");
            return string.Empty;
        }

        try
        {
            LogToFile("[DecryptData] DEBUG: Converting Base64 input to byte array. R=Decrypt data, A=Convert base64, P=Input present, I=Ready for decode, D=Continue.", "DEBUG");
            byte[] encryptedDataBytes = Convert.FromBase64String(encryptedData);
            LogToFile($"[DecryptData] DEBUG: Converted Base64 to {encryptedDataBytes.Length} bytes. R=Decrypt data, A=Base64 decode, P=encryptedDataLength={encryptedData.Length}, I=Bytes ready, D=Continue.", "DEBUG");

            LogToFile("[DecryptData] DEBUG: Beginning DPAPI decryption using DataProtectionScope.CurrentUser. R=Decrypt data, A=Unprotect bytes, P=Bytes present, I=Ready for unprotect, D=Continue.", "DEBUG");
            byte[] decryptedData = null;
            try
            {
                decryptedData = ProtectedData.Unprotect(encryptedDataBytes, null, DataProtectionScope.CurrentUser);
            }
            catch (Exception exUnprotect)
            {
                LogToFile($"[DecryptData] ERROR: Exception during ProtectedData.Unprotect: {exUnprotect.Message} R=Decrypt data, A=Unprotect bytes, P=encryptedDataLength={encryptedData.Length}, I=DPAPI error, D=Failed.", "ERROR");
                LogToFile($"[DecryptData] Context: encryptedDataLength={(encryptedData?.Length ?? 0)}", "ERROR");
                LogToFile($"[DecryptData] Stack: {exUnprotect.StackTrace}", "DEBUG");
                LogToFile("<<< [DecryptData] Exit: R=Decrypt data, A=Unprotect bytes, P=encryptedDataLength={encryptedData.Length}, I=DPAPI error, D=Return empty.", "DEBUG");
                return string.Empty;
            }
            string decryptedString = Encoding.UTF8.GetString(decryptedData);
            LogToFile($"[DecryptData] DEBUG: Data decryption successful. R=Decrypt data, A=Unprotect bytes, P=InputLength={encryptedData.Length}, I=Return plaintext string, D=Success.", "DEBUG");
            LogToFile("<<< [DecryptData] Exit: R=Decrypt data, A=Unprotect bytes, P=InputLength=" + encryptedData.Length + ", I=Decryption complete, D=Return decrypted.", "DEBUG");
            return decryptedString;
        }
        catch (FormatException fmtEx)
        {
            LogToFile($"[DecryptData] ERROR: Encrypted data was not valid Base64: {fmtEx.Message} R=Decrypt data, A=Base64 decode, P=encryptedDataLength={(encryptedData?.Length ?? 0)}, I=Format error, D=Failed.", "ERROR");
            LogToFile($"[DecryptData] Context: encryptedDataLength={(encryptedData?.Length ?? 0)}", "ERROR");
            LogToFile($"[DecryptData] Stack: {fmtEx.StackTrace}", "DEBUG");
            LogToFile("<<< [DecryptData] Exit: R=Decrypt data, A=Base64 decode, P=encryptedDataLength={(encryptedData?.Length ?? 0)}, I=Format error, D=Return empty.", "DEBUG");
            return string.Empty;
        }
        catch (CryptographicException cryptoEx)
        {
            LogToFile($"[DecryptData] ERROR: Cryptographic exception during decryption: {cryptoEx.Message} R=Decrypt data, A=Unprotect bytes, P=encryptedDataLength={(encryptedData?.Length ?? 0)}, I=Crypto error, D=Failed.", "ERROR");
            LogToFile($"[DecryptData] Context: encryptedDataLength={(encryptedData?.Length ?? 0)}", "ERROR");
            LogToFile($"[DecryptData] Stack: {cryptoEx.StackTrace}", "DEBUG");
            LogToFile("<<< [DecryptData] Exit: R=Decrypt data, A=Unprotect bytes, P=encryptedDataLength={(encryptedData?.Length ?? 0)}, I=Crypto error, D=Return empty.", "DEBUG");
            return string.Empty;
        }
        catch (Exception ex)
        {
            LogToFile($"[DecryptData] ERROR: Unexpected exception during decryption: {ex.Message} R=Decrypt data, A=Unprotect bytes, P=encryptedDataLength={(encryptedData?.Length ?? 0)}, I=Exception, D=Failed.", "ERROR");
            LogToFile($"[DecryptData] Context: encryptedDataLength={(encryptedData?.Length ?? 0)}", "ERROR");
            LogToFile($"[DecryptData] Stack: {ex.StackTrace}", "DEBUG");
            LogToFile("<<< [DecryptData] Exit: R=Decrypt data, A=Unprotect bytes, P=encryptedDataLength={(encryptedData?.Length ?? 0)}, I=Exception, D=Return empty.", "DEBUG");
            return string.Empty;
        }
    }
}