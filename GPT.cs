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
    public Queue<chatMessage> GPTLog { get; set; } = new Queue<chatMessage>(); // Store previous prompts and responses in a queue
    public Queue<chatMessage> ChatLog { get; set; } = new Queue<chatMessage>(); // Store the chat log in a queue

    /// <summary>
    /// Represents application settings used to configure various aspects of the application.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the API key used for OpenAI services.
        /// </summary>
        public string OpenApiKey { get; set; }
        /// <summary>
        /// Gets or sets the model used by OpenAI for generating responses.
        /// </summary>
        public string OpenAiModel { get; set; }
        /// <summary>
        /// Gets or sets the path where the database is stored.
        /// </summary>
        public string DatabasePath { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the application should ignore bot usernames.
        /// </summary>
        public string IgnoreBotUsernames { get; set; }
        /// <summary>
        /// Gets or sets an alias or identifier for a voice.
        /// </summary>
        public string VoiceAlias { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether emojis should be stripped from generated responses.
        /// </summary>
        public string StripEmojisFromResponse { get; set; }
        /// <summary>
        /// Gets or sets the level of logging used by the application.
        /// </summary>
        public string LoggingLevel { get; set; }
        /// <summary>
        /// Gets or sets the version of the application.
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether hate content is allowed.
        /// </summary>
        public string HateAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether threatening hate content is allowed.
        /// </summary>
        public string HateThreateningAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether self-harm content is allowed.
        /// </summary>
        public string SelfHarmAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether violent content is allowed.
        /// </summary>
        public string ViolenceAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether content related to self-harm intent is allowed.
        /// </summary>
        public string SelfHarmIntentAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether content containing self-harm instructions is allowed.
        /// </summary>
        public string SelfHarmInstructionsAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether harassment content is allowed.
        /// </summary>
        public string HarassmentAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether threatening harassment content is allowed.
        /// </summary>
        public string HarassmentThreateningAllowed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether GPT questions should be logged to Discord.
        /// </summary>
        public string LogGptQuestionsToDiscord { get; set; }
        /// <summary>
        /// Gets or sets the URL of the Discord webhook.
        /// </summary>
        public string DiscordWebhookUrl { get; set; }
        /// <summary>
        /// Gets or sets the username of the Discord bot.
        /// </summary>
        public string DiscordBotUsername { get; set; }
        /// <summary>
        /// Gets or sets the URL of the Discord bot's avatar.
        /// </summary>
        public string DiscordAvatarUrl { get; set; }
    }

    /// <summary>
    /// Represents the response structure for ChatGPT completions API.
    /// This class is designed to capture the data returned from the API,
    /// which includes a list of choices representing the possible completions
    /// for the input provided to the chat model.
    /// </summary>
    public class ChatCompletionsResponse
    {
        /// <summary>
        /// Gets or sets the list of choices provided by the API.
        /// Each choice represents a different possible completion
        /// returned by ChatGPT based on the input prompt.
        /// </summary>
        public List<Choice> Choices { get; set; }
    }

    /// <summary>
    /// Represents a single choice in the completions response.
    /// Each choice contains the reason for the completion's finish state
    /// and the chat message which includes the generated text.
    /// </summary>
    public class Choice
    {
        /// <summary>
        /// Gets or sets the reason why the completion ended.
        /// The 'finish_reason' provides information about why the model
        /// stopped generating further text, such as reaching the maximum length
        /// or the model determining it has completed the content.
        /// </summary>
        public string finish_reason { get; set; }
        /// <summary>
        /// Gets or sets the chat message which is the generated completion
        /// from the ChatGPT model. This contains the actual text that was
        /// generated in response to the input prompt.
        /// </summary>
        public chatMessage Message { get; set; }
    }

    /// <summary>
    /// Represents the response structure for the ChatGPT Moderation API.
    /// This class captures the data returned from the moderation service,
    /// which includes a list of results. Each result indicates whether
    /// the content was flagged and provides details about various moderation categories.
    /// </summary>
    public class ModerationResponse
    {
        /// <summary>
        /// Gets or sets the list of moderation results.
        /// Each 'Result' object contains detailed information about the moderation
        /// analysis, including whether the message was flagged and specific category
        /// scores that indicate the likelihood of the content falling into those categories.
        /// </summary>
        public List<Result> Results { get; set; }
    }

    /// <summary>
    /// Represents a single result in the moderation response.
    /// This contains the outcome of the moderation check for a piece of content,
    /// indicating if it was flagged based on certain criteria.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Gets or sets a value indicating whether the content was flagged
        /// by the moderation system. A 'true' value means the content was flagged
        /// as potentially problematic, while 'false' means it was not flagged.
        /// </summary>
        public bool Flagged { get; set; }
        /// <summary>
        /// Gets or sets a dictionary where each key represents a moderation category,
        /// and the corresponding boolean value indicates whether the content falls
        /// into that category (true for yes, false for no).
        /// </summary>
        public Dictionary<string, bool> Categories { get; set; }
        /// <summary>
        /// Gets or sets a dictionary where each key is a moderation category,
        /// and the corresponding double value represents the score or likelihood
        /// that the content belongs to that category, according to the moderation model.
        /// </summary>
        public Dictionary<string, double> Category_scores { get; set; }
    }

    /// <summary>
    /// Represents a user record from the https://pronouns.alejo.io/ API.
    /// This model captures the pronoun preferences for a Twitch user as specified
    /// in their profile, which can be processed and displayed alongside their username.
    /// </summary>
    public class PronounUser
    {
        /// <summary>
        /// Gets or sets the Twitch ID of the user. This is a unique identifier
        /// for the user on the Twitch platform.
        /// </summary>
        public string id { get; set; }
        /// <summary>
        /// Gets or sets the Twitch username (login) of the user. This is the user's
        /// handle on Twitch, which is used for logging in and is displayed in the chat.
        /// </summary>
        public string login { get; set; }
        /// <summary>
        /// Gets or sets the pronoun set identifier for the user. This is a compact,
        /// concatenated string like "hehim" that represents the user's pronoun preferences.
        /// It will be processed in later methods to format into a more readable form such as "He/Him."
        /// </summary>
        public string pronoun_id { get; set; }
    }

    /// <summary>
    /// Represents an individual data record for a broadcaster's stream.
    /// This class holds information about the stream, including details about
    /// the broadcaster, the game being played, and the stream's metadata.
    /// </summary>
    public class Datum
    {
        /// <summary>
        /// Gets or sets the Twitch ID of the broadcaster.
        /// </summary>
        public string broadcaster_id { get; set; }
        /// <summary>
        /// Gets or sets the login name of the broadcaster on Twitch.
        /// </summary>
        public string broadcaster_login { get; set; }
        /// <summary>
        /// Gets or sets the display name of the broadcaster on Twitch.
        /// </summary>
        public string broadcaster_name { get; set; }
        /// <summary>
        /// Gets or sets the language of the broadcaster's stream.
        /// </summary>
        public string broadcaster_language { get; set; }
        /// <summary>
        /// Gets or sets the game ID for the game being played on the stream.
        /// </summary>
        public string game_id { get; set; }
        /// <summary>
        /// Gets or sets the name of the game being played.
        /// </summary>
        public string game_name { get; set; }
        /// <summary>
        /// Gets or sets the title of the stream.
        /// </summary>
        public string title { get; set; }
        /// <summary>
        /// Gets or sets the delay of the stream in seconds.
        /// </summary>
        public int delay { get; set; }
    }

    /// <summary>
    /// Represents the root object for the JSON response that contains
    /// the list of data records related to broadcaster streams.
    /// </summary>
    public class Root
    {
        /// <summary>
        /// Gets or sets the list of 'Datum' objects representing each
        /// individual stream's data returned from the API.
        /// </summary>
        public List<Datum> data { get; set; }
    }

    /// <summary>
    /// Represents a simplified model that encapsulates specific information
    /// about a broadcaster's stream, such as the user's name, the game name,
    /// and the stream's title.
    /// </summary>
    public class AllDatas
    {
        /// <summary>
        /// Gets or sets the username of the broadcaster.
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// Gets or sets the name of the game that is currently being played on the stream.
        /// </summary>
        public string gameName { get; set; }
        /// <summary>
        /// Gets or sets the title of the stream.
        /// </summary>
        public string titleName { get; set; }
    }

    /// <summary>
    /// Represents a chat message from Twitch, capturing both the user's role and their message content.
    /// This class is tailored for preparing the message to be sent to the Chat Completions API, 
    /// which uses the role and content to generate contextually appropriate responses.
    /// The ToString method has been overridden to provide a meaningful string representation
    /// for logging purposes, which includes the role of the user and the message content.
    /// </summary>
    public class chatMessage
    {
        /// <summary>
        /// Gets or sets the role of the Twitch user who sent the message.
        /// This property is important for the Chat Completions API to understand the context of the message,
        /// as different roles may receive different types of responses based on their privileges or status within the chat.
        /// </summary>
        public string role { get; set; }
        /// <summary>
        /// Gets or sets the text content of the Twitch chat message.
        /// The content is used by the Chat Completions API to generate a response that is coherent
        /// and relevant to the discussion at hand.
        /// </summary>
        public string content { get; set; }

        /// <summary>
        /// Overrides the default ToString method to provide a string representation of the chat message.
        /// This representation includes the user's role and the content of the message, which can be used
        /// for logging or debugging purposes to easily understand the nature of the message.
        /// </summary>
        /// <returns>A string that represents the current object, formatted as "Role: [role], Content: [content]".</returns>
        public override string ToString()
        {
            return $"Role: {role}, Content: {content}";
        }
    }

    // Helper class to store pronouns with an expiration date./// <summary>
    /// Represents a cache entry for a user's pronouns, including the formatted pronouns and the expiration date of the cache.
    /// </summary>
    public class PronounCacheEntry
    {
        /// <summary>
        /// Gets or sets the formatted string of the user's pronouns.
        /// </summary>
        public string FormattedPronouns { get; set; }
        /// <summary>
        /// Gets or sets the expiration date and time of the cached pronouns.
        /// </summary>
        public DateTime Expiration { get; set; }
    }

    /// <summary>
    /// Queues a chat message into a chat log, ensuring the log does not exceed a specified size.
    /// </summary>
    /// <param name = "chatMsg">The chat message to enqueue.</param>
    private void QueueMessage(chatMessage chatMsg)
    {
        // Log that we've entered the method
        LogToFile($"Entering QueueMessage with chatMsg: {chatMsg}", "DEBUG");
        try
        {
            // Log the action of enqueuing a message
            LogToFile($"Enqueuing chat message: {chatMsg}", "INFO");
            ChatLog.Enqueue(chatMsg);
            // Log the status of the queue after enqueuing
            LogToFile($"ChatLog Count after enqueuing: {ChatLog.Count}", "DEBUG");
            if (ChatLog.Count > 20)
            {
                // Log the message about to be dequeued
                chatMessage dequeuedMessage = ChatLog.Peek(); // Peek to see what will be dequeued
                LogToFile($"Dequeuing chat message to maintain queue size: {dequeuedMessage}", "DEBUG");
                ChatLog.Dequeue();
                // Log the status of the queue after dequeuing
                LogToFile($"ChatLog Count after dequeuing: {ChatLog.Count}", "DEBUG");
            }
        }
        catch (Exception ex)
        {
            // Log the error
            LogToFile($"An error occurred while enqueuing or dequeuing a chat message: {ex.Message}", "ERROR");
        }
    }

    /// <summary>
    /// Queues a pair of messages to the GPTLog, one from the user and one from the assistant.
    /// This method ensures that messages are always kept in balance within the queue. 
    /// The queue is managed on a First-In-First-Out (FIFO) basis and maintains up to 5 pairs of messages.
    /// If the limit is exceeded, the oldest pair of messages is dequeued to maintain the size of the queue.
    /// </summary>
    /// <param name = "userContent">The content of the user's message.</param>
    /// <param name = "assistantContent">The content of the assistant's message.</param>
    private void QueueGPTMessage(string userContent, string assistantContent)
    {
        // Log that we've entered the method
        LogToFile("Entering QueueGPTMessage with paired messages.", "DEBUG");
        // Create the user and assistant chat messages
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
            // Enqueue the user and assistant messages as a pair
            GPTLog.Enqueue(userMessage);
            GPTLog.Enqueue(assistantMessage);
            // Log the action of enqueuing the messages
            LogToFile($"Enqueuing user message: {userMessage}", "INFO");
            LogToFile($"Enqueuing assistant message: {assistantMessage}", "INFO");
            // If the queue exceeds 10 messages (5 pairs), dequeue the oldest pair
            if (GPTLog.Count > 10)
            {
                LogToFile("GPTLog limit exceeded. Dequeuing the oldest pair of messages.", "DEBUG");
                GPTLog.Dequeue(); // Dequeue user message
                GPTLog.Dequeue(); // Dequeue assistant message
            }

            // Log the status of the queue after operation
            LogToFile($"GPTLog Count after enqueueing/dequeueing: {GPTLog.Count}", "DEBUG");
        }
        catch (Exception ex)
        {
            // Log the error
            LogToFile($"An error occurred while enqueuing GPT messages: {ex.Message}", "ERROR");
        }
    }

    /// <summary>
    /// Initializes the PNGTuber-GPT application and logs successful setup.
    /// </summary>
    /// <returns>Returns true to indicate successful execution.</returns>
    public bool Execute()
    {
        // Log the start of the initialization process.
        LogToFile("Starting initialization of the PNGTuber-GPT application.", "INFO");
        // Since the method only returns true and no other action is taken, log the successful setup.
        LogToFile("Initialization of PNGTuber-GPT successful. Added all global variables to memory.", "INFO");
        // Log the start of the version number retrieval process.
        LogToFile("Starting to retrieve the version number from a global variable.", "DEBUG");
        // Retrieve the version number from a global variable.
        string initializeVersionNumber = CPH.GetGlobalVar<string>("Version", true);
        // Log the retrieved version number for debugging purposes.
        LogToFile($"Retrieved version number: {initializeVersionNumber}", "DEBUG");
        // Check if the version number was successfully retrieved.
        if (string.IsNullOrWhiteSpace(initializeVersionNumber))
        {
            // Log an error if the version number is not found or is empty.
            LogToFile("The 'Version' global variable is not found or is empty.", "ERROR");
            return false;
        }

        // Log the sending of the version number to the chat for debugging purposes.
        LogToFile($"Sending version number to chat: {initializeVersionNumber}", "DEBUG");
        // Send the version number to the chat.
        CPH.SendMessage($"{initializeVersionNumber} has been initialized successfully.", true);
        // Log the result of sending the version number.
        LogToFile("Version number sent to chat successfully.", "INFO");
        // Return true to indicate the version number has been sent successfully.
        return true;
        return true;
    }

    /// <summary>
    /// Retrieves a Twitch user's nickname appended with their pronouns. If the pronouns are not cached,
    /// it fetches them from an external API and updates the cache. If the preferred username file does not exist,
    /// it creates a default file.
    /// </summary>
    /// <returns>True if the nickname with pronouns is successfully retrieved and set; otherwise, false.</returns>
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
            // Get the path where the database is stored
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' value is either not found or not a valid string.", "ERROR");
                return false;
            }

            // Check if the preferred usernames file exists, create if not
            string filePath = Path.Combine(databasePath, "preferred_userNames.json");
            if (!File.Exists(filePath))
            {
                LogToFile("'preferred_userNames.json' does not exist. Creating default file.", "WARN");
                CreateDefaultUserNameFile(filePath);
            }

            // Retrieve the preferred username
            string preferredUserName = GetPreferredUsername(userName, filePath);
            if (string.IsNullOrWhiteSpace(preferredUserName))
            {
                LogToFile("Preferred user name could not be retrieved.", "ERROR");
                return false;
            }

            // Validate ignore list of bot names.
            string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
            if (string.IsNullOrWhiteSpace(ignoreNamesString))
            {
                LogToFile("'Ignore Bot Usernames' global variable is not found or not a valid string.", "ERROR");
                return false;
            }

            // Log the list of bot usernames to ignore.
            LogToFile($"Bot usernames to ignore: {ignoreNamesString}", "DEBUG");
            // Process and check against the list of bot names to ignore.
            List<string> ignoreNamesList = ignoreNamesString.Split(',').Select(name => name.Trim()).ToList();
            if (ignoreNamesList.Contains(userName, StringComparer.OrdinalIgnoreCase))
            {
                LogToFile($"Message from {userName} ignored as it's in the bot ignore list.", "INFO");
                return false;
            }

            // Retrieve or create pronouns and append them to the preferred username
            string pronouns = GetOrCreatePronouns(userName, databasePath);
            string formattedUsername = $"{preferredUserName}{(string.IsNullOrEmpty(pronouns) ? "" : $" ({pronouns})")}";
            LogToFile($"Formatted 'nicknamePronouns': {formattedUsername}", "INFO");
            // Set the formatted nickname and pronouns for further use
            CPH.SetArgument("nicknamePronouns", formattedUsername);
            CPH.SetArgument("Pronouns", pronouns);
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the method execution
            LogToFile($"An error occurred in GetNicknamewPronouns: {ex.Message}", "ERROR");
            return false;
        }
    }

    /// <summary>
    /// Retrieves or creates the pronouns for a user. If pronouns are already cached and not expired,
    /// it uses the cached pronouns. Otherwise, it fetches the pronouns from the external API and updates
    /// the cache with a 24-hour expiration.
    /// </summary>
    /// <param name = "username">The Twitch username to retrieve or create pronouns for.</param>
    /// <param name = "pronounsCachePath">The file path to the pronouns cache.</param>
    /// <returns>The formatted pronouns string for the user.</returns>
    private string GetOrCreatePronouns(string username, string databasePath)
    {
        // Ensure that databasePath is the directory path to where pronouns.json should be stored
        string pronounsCachePath = Path.Combine(databasePath, "pronouns.json");
        // Check if the pronouns cache file exists, if not, create a new dictionary to store pronouns
        Dictionary<string, PronounCacheEntry> pronounsCache = File.Exists(pronounsCachePath) ? JsonConvert.DeserializeObject<Dictionary<string, PronounCacheEntry>>(File.ReadAllText(pronounsCachePath)) : new Dictionary<string, PronounCacheEntry>();
        // Attempt to get cached pronouns if they exist and are not expired
        if (pronounsCache.TryGetValue(username, out PronounCacheEntry cacheEntry) && cacheEntry.Expiration > DateTime.UtcNow)
        {
            LogToFile($"Using cached pronouns for user '{username}': {cacheEntry.FormattedPronouns}", "INFO");
            return cacheEntry.FormattedPronouns;
        }
        else
        {
            // Fetch pronouns from the API and update the cache
            string pronouns = FetchPronouns(username, databasePath);
            if (!string.IsNullOrEmpty(pronouns))
            {
                pronounsCache[username] = new PronounCacheEntry
                {
                    FormattedPronouns = pronouns,
                    Expiration = DateTime.UtcNow.AddHours(24)
                };
                // Save the updated cache to the file
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

    /// <summary>
    /// Creates a default username file with a predefined user dictionary if the file does not exist.
    /// </summary>
    /// <param name = "filePath">The file path where the default username file will be created.</param>
    private void CreateDefaultUserNameFile(string filePath)
    {
        // Log entry into the method for debugging purposes.
        LogToFile($"Entering CreateDefaultUserNameFile method with filePath: {filePath}", "DEBUG");
        try
        {
            // Log the action of creating the default user dictionary.
            LogToFile("Creating a default user dictionary for the username file.", "DEBUG");
            // Create a default user dictionary to be used when the preferred usernames file is not found.
            var defaultUser = new Dictionary<string, string>
            {
                {
                    "DefaultUser",
                    "Default User"
                }
            };
            // Serialize the default user dictionary to JSON format with indentation for readability.
            string jsonData = JsonConvert.SerializeObject(defaultUser, Formatting.Indented);
            LogToFile("Serialized default user data to JSON.", "DEBUG");
            // Write the JSON data to the specified file path, effectively creating the file.
            File.WriteAllText(filePath, jsonData);
            // Log the result of the file creation.
            LogToFile($"Created and wrote to the file: {filePath}", "INFO");
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the file creation process.
            LogToFile($"An error occurred while creating the default username file: {ex.Message}", "ERROR");
        }
    }

    /// <summary>
    /// Retrieves a user's preferred username from a JSON file. If the file exists and contains the username, the preferred username is returned; otherwise, the current username is used.
    /// </summary>
    /// <param name = "currentUserName">The current username to look up the preferred username for.</param>
    /// <param name = "filePath">The file path of the JSON file containing preferred usernames.</param>
    /// <returns>The preferred username if found, otherwise the current username.</returns>
    private string GetPreferredUsername(string currentUserName, string filePath)
    {
        // Initialize the preferred username with the current username.
        string preferredUserName = currentUserName;
        // Log the entry into the method along with the provided parameters.
        LogToFile($"Entering GetPreferredUsername method with currentUserName: {currentUserName} and filePath: {filePath}", "DEBUG");
        try
        {
            // Check if the file with user preferences exists.
            if (File.Exists(filePath))
            {
                // Read the entire content of the file.
                string jsonData = File.ReadAllText(filePath);
                LogToFile("Read user preferences JSON data from file.", "DEBUG");
                // Deserialize the JSON data into a dictionary of user preferences.
                var userPreferences = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);
                // Log the attempt to find the preferred username.
                LogToFile($"Attempting to find preferred username for {currentUserName}.", "DEBUG");
                // Check if the deserialization was successful and if the current user's preferred name exists.
                if (userPreferences != null && userPreferences.TryGetValue(currentUserName, out var preferredName))
                {
                    // Update the preferred username with the one obtained from the file.
                    preferredUserName = preferredName;
                    LogToFile($"Found and set preferred username: {preferredUserName}", "DEBUG");
                }
                else
                {
                    // Log that the preferred username was not found in the file.
                    LogToFile($"Preferred username for {currentUserName} not found in file. Using current username as preferred.", "INFO");
                }
            }
            else
            {
                // Log that the file does not exist.
                LogToFile($"File not found: {filePath}. Using current username as preferred.", "WARN");
            }

            // Validate ignore list of bot names.
            string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
            if (string.IsNullOrWhiteSpace(ignoreNamesString))
            {
                LogToFile("'Ignore Bot Usernames' global variable is not found or not a valid string.", "ERROR");
                return preferredUserName;
            }

            // Log the list of bot usernames to ignore.
            LogToFile($"Bot usernames to ignore: {ignoreNamesString}", "DEBUG");
            // Process and check against the list of bot names to ignore.
            List<string> ignoreNamesList = ignoreNamesString.Split(',').Select(name => name.Trim()).ToList();
            if (ignoreNamesList.Contains(currentUserName, StringComparer.OrdinalIgnoreCase))
            {
                LogToFile($"Username {currentUserName} is in the bot ignore list. Using current username as preferred.", "DEBUG");
                return currentUserName;
            }
        }
        catch (Exception ex)
        {
            // Log the exception that occurred during the process.
            LogToFile($"Error reading or deserializing user preferred names from file: {ex.Message}", "ERROR");
        }

        // Log the result of the username retrieval.
        LogToFile($"Returning preferred or original username: {preferredUserName}", "INFO");
        // Return the preferred or original username.
        return preferredUserName;
    }

    /// <summary>
    /// Informs the user on how to set their pronouns using an external service, sending a message with the necessary instructions.
    /// </summary>
    /// <returns>Returns true if the instructional message was sent successfully, otherwise false if an error occurred.</returns>
    public bool SetPronouns()
    {
        // Log the entry into the SetPronouns method.
        LogToFile("Entering SetPronouns method.", "DEBUG");
        try
        {
            // Log the action of sending the pronoun setting information.
            LogToFile("Preparing to send pronouns setting information message to user.", "DEBUG");
            // Send a message to the user about setting their pronouns using the provided service.
            string message = "You can set your pronouns at https://pronouns.alejo.io/. Your pronouns will be available via a Public API. This means that users of 7TV, FFZ, and BTTV extensions can see your pronouns in chat.";
            CPH.SendMessage(message, true);
            // Log the successful sending of the message.
            LogToFile("!setpronouns was triggered, sent pronouns setting information message to user.", "INFO");
            // Return true indicating the method completed successfully.
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the sending of the message.
            LogToFile($"An error occurred in SetPronouns while sending message: {ex.Message}", "ERROR");
            // Return false indicating the method did not complete successfully.
            return false;
        }
    }

    private string FetchPronouns(string username, string databasePath)
    {
        LogToFile($"Entering FetchPronouns method for username: {username}", "DEBUG");
        // Define the path for the pronouns cache.
        string pronounsCachePath = Path.Combine(databasePath, "pronouns.json");
        // Load the pronouns cache if it exists.
        Dictionary<string, PronounCacheEntry> pronounsCache = LoadPronounsCache(pronounsCachePath);
        // Check if the pronouns are cached and not expired.
        if (pronounsCache.TryGetValue(username, out PronounCacheEntry cacheEntry) && cacheEntry.Expiration > DateTime.UtcNow)
        {
            LogToFile($"Using cached pronouns for user '{username}'.", "INFO");
            return cacheEntry.FormattedPronouns;
        }

        // If not cached or expired, fetch pronouns from the API.
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
                        // Cache the new pronouns with a 24-hour expiration time.
                        pronounsCache[username] = new PronounCacheEntry
                        {
                            FormattedPronouns = formattedPronouns,
                            Expiration = DateTime.UtcNow.AddHours(24)
                        };
                        // Save the updated cache to the JSON file.
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

    /// <summary>
    /// Formats a concatenated string of pronoun identifiers into a human-readable format.
    /// </summary>
    /// <param name = "pronounId">The concatenated string of pronouns.</param>
    /// <returns>A formatted string with each pronoun capitalized and separated by slashes.
    private string FormatPronouns(string pronounId)
    {
        LogToFile($"Entering FormatPronouns method with pronounId: {pronounId}", "DEBUG");
        // Initialize a list of all possible pronouns.
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
        // Sort the list by the length of the pronouns in descending order to prevent matching substrings of longer pronouns.
        pronounsList.Sort((a, b) => b.Length.CompareTo(a.Length));
        // Initialize the list that will hold the formatted pronouns.
        var formattedPronouns = new List<string>();
        // Convert the pronounId to lowercase for case-insensitive matching.
        string remainingPronounId = pronounId.ToLower();
        LogToFile("Starting pronoun formatting.", "DEBUG");
        // Iterate over each pronoun in the list and attempt to match them at the start of the remaining string.
        while (!string.IsNullOrEmpty(remainingPronounId))
        {
            bool matchFound = false;
            foreach (var pronoun in pronounsList)
            {
                if (remainingPronounId.StartsWith(pronoun))
                {
                    // Capitalize and add the pronoun to the formatted list.
                    formattedPronouns.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pronoun));
                    // Remove the matched pronoun from the start of the remaining string.
                    remainingPronounId = remainingPronounId.Substring(pronoun.Length);
                    matchFound = true;
                    LogToFile($"Matched Pronoun: {pronoun}, Remaining ID: {remainingPronounId}", "DEBUG");
                    break; // Break out of the foreach loop since we found a match.
                }
            }

            // If no match is found, exit the while loop to prevent an infinite loop.
            if (!matchFound)
            {
                break;
            }
        }

        // Join the formatted pronouns into a single string separated by slashes.
        string formattedPronounsString = string.Join("/", formattedPronouns);
        LogToFile($"Formatted pronouns: {formattedPronounsString}", "DEBUG");
        return formattedPronounsString;
    }

    /// <summary>
    /// Retrieves the current nickname with pronouns for a user and sends a message indicating the nickname or stating that no custom nickname is set.
    /// </summary>
    /// <returns>Returns true if the message was successfully sent, otherwise false if an error occurs.</returns>
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

                // Log the processing of the nickname and pronouns.
                LogToFile("Processing the nickname and pronouns for message sending.", "DEBUG");
                // Split the nicknamePronouns at the first space to extract the nickname
                string[] split = nicknamePronouns.Split(new[] { ' ' }, 2);
                string nickname = split[0];
                if (userName.Equals(nickname, StringComparison.OrdinalIgnoreCase))
                {
                    // If the userName is the same as the nickname, the user hasn't set a custom nickname.
                    CPH.SendMessage($"You don't have a custom nickname set. Your username is: {nicknamePronouns}", true);
                    LogToFile("Informed user they don't have a custom nickname set.", "INFO");
                }
                else
                {
                    // If the userName is different from the nickname, the user has set a custom nickname.
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

    /// <summary>
    /// Sets the preferred username for a user and updates the user preferences file.
    /// If any parameter is missing or invalid, or if an error occurs during file operations,
    /// the function will log the error and return false.
    /// </summary>
    /// <returns>True if the preferred username is set successfully, otherwise false.</returns>
    public bool SetPreferredUsername()
    {
        LogToFile("Entering SetPreferredUsername method.", "DEBUG");
        // Retrieve the necessary parameters from the args dictionary.
        string userName = args["userName"]?.ToString();
        string pronouns = args["Pronouns"]?.ToString();
        string preferredUserNameInput = args["rawInput"]?.ToString();
        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        // Log the retrieved parameters for debugging purposes.
        LogToFile($"Retrieved parameters: userName={userName}, Pronouns={pronouns}, rawInput={preferredUserNameInput}, Database Path={databasePath}", "DEBUG");
        // Initial validation of the input parameters.
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(preferredUserNameInput))
        {
            string missingParameter = string.IsNullOrWhiteSpace(userName) ? "userName" : string.IsNullOrWhiteSpace(databasePath) ? "Database Path" : "rawInput";
            LogToFile($"'${missingParameter}' value is either not found or not a valid string.", "ERROR");
            return false;
        }

        // Pronouns may be optional, so if not provided, proceed without them.
        if (string.IsNullOrWhiteSpace(pronouns))
        {
            pronouns = "";
            LogToFile("Pronouns value not found. Proceeding without pronouns.", "DEBUG");
        }

        // Construct the file path for the preferred usernames JSON file.
        string filePath = Path.Combine(databasePath, "preferred_userNames.json");
        LogToFile($"File path for preferred usernames: {filePath}", "DEBUG");
        try
        {
            // Check if the file exists, if not, create it or read existing user preferences.
            Dictionary<string, string> userPreferences = File.Exists(filePath) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath)) : new Dictionary<string, string>();
            // Update or add the preferred username for the current user.
            userPreferences[userName] = preferredUserNameInput;
            LogToFile($"Set preferred username for '{userName}' to '{preferredUserNameInput}'.", "DEBUG");
            // Write the updated user preferences back to the file.
            File.WriteAllText(filePath, JsonConvert.SerializeObject(userPreferences, Formatting.Indented));
            LogToFile("Updated user preferences file successfully.", "INFO");
            // Send a confirmation message to the user.
            string message = $"{userName}, your nickname has been set to {preferredUserNameInput} ({pronouns}).";
            CPH.SendMessage(message, true);
            LogToFile($"Sent confirmation message to user: {message}", "INFO");
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the process and notify the user.
            LogToFile($"An error occurred while setting the preferred username: {ex.Message}", "ERROR");
            string errorMessage = $"{userName}, I was unable to set your nickname. Please try again later.";
            CPH.SendMessage(errorMessage, true);
            LogToFile($"Sent error message to user: {errorMessage}", "INFO");
            return false;
        }
    }

    /// <summary>
    /// Removes the preferred nickname for a user from the keyword contexts file and confirms the action in chat.
    /// </summary>
    /// <returns>Returns true if the nickname was successfully removed, otherwise false.</returns>
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

    /// <summary>
    /// Removes the keyword from the keyword contexts file and confirms the action in chat.
    /// </summary>
    /// <returns>Returns true if the keyword was successfully removed, otherwise false.</returns>
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

    /// <summary>
    /// Removes the username from the keyword contexts file and confirms the action in chat.
    /// </summary>
    /// <returns>Returns true if the username was successfully removed, otherwise false.</returns>
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

    /// <summary>
    /// Retrieves the stored information for a user from the keyword contexts file and posts it to the chat.
    /// </summary>
    /// <returns>Returns true if information is successfully retrieved and sent to chat, otherwise false.</returns>
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
            // Check if the keyword contexts file exists
            if (!File.Exists(filePath))
            {
                LogToFile("The keyword contexts file does not exist. No information to retrieve.", "WARN");
                CPH.SendMessage("No information has been stored for you, {nicknamePronouns}", true);
                return true;
            }

            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
            // Check if there's an entry for the user
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

    /// <summary>
    /// Saves a user's message to a queue unless it's a command or from a bot. It also handles initialization of the queue if needed.
    /// </summary>
    /// <returns>True if the message is successfully saved, otherwise false.</returns>
    public bool SaveMessage()
    {
        LogToFile("Entering SaveMessage method.", "DEBUG");
        // Retrieve the message from the args dictionary.
        string msg = args["rawInput"]?.ToString();
        string userName = args["userName"]?.ToString();
        string ignoreNamesString = CPH.GetGlobalVar<string>("Ignore Bot Usernames", true);
        // Initial validation of the message and username.
        if (string.IsNullOrWhiteSpace(msg) || string.IsNullOrWhiteSpace(userName))
        {
            LogToFile($"'rawInput' or 'userName' value is not found or not a valid string. rawInput: {msg}, userName: {userName}", "ERROR");
            return false;
        }

        // Log the message and username retrieved.
        LogToFile($"Retrieved message: {msg}, from user: {userName}", "INFO");
        // Check if the message starts with "!" indicating a command.
        if (msg.StartsWith("!"))
        {
            LogToFile("Message is a command and will be ignored.", "INFO");
            return false;
        }

        // Validate ignore list of bot names.
        if (string.IsNullOrWhiteSpace(ignoreNamesString))
        {
            LogToFile("'Ignore Bot Usernames' global variable is not found or not a valid string.", "ERROR");
            return false;
        }

        // Log the list of bot usernames to ignore.
        LogToFile($"Bot usernames to ignore: {ignoreNamesString}", "DEBUG");
        // Process and check against the list of bot names to ignore.
        List<string> ignoreNamesList = ignoreNamesString.Split(',').Select(name => name.Trim()).ToList();
        if (ignoreNamesList.Contains(userName, StringComparer.OrdinalIgnoreCase))
        {
            LogToFile($"Message from {userName} ignored as it's in the bot ignore list.", "INFO");
            return false;
        }

        // Retrieve formatted user name with pronouns.
        string nicknamePronouns = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : userName;
        // Log the retrieval of the formatted user name.
        LogToFile($"Retrieved formatted nickname with pronouns: {nicknamePronouns}", "DEBUG");
        // Initialize ChatLog if it's null.
        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            LogToFile("ChatLog queue has been initialized.", "DEBUG");
        }

        // Prepare the message to be queued.
        string messageContent = $"{nicknamePronouns} says: {msg}";
        LogToFile($"Preparing to queue message: {messageContent}", "DEBUG");
        // Create and queue the chat message.
        chatMessage chatMsg = new chatMessage
        {
            role = "user",
            content = messageContent
        };
        QueueMessage(chatMsg);
        // Log that the message has been queued.
        LogToFile($"Message queued successfully: {messageContent}", "INFO");
        // Optionally, sleep for a short duration if required for rate limiting or other purposes.
        System.Threading.Thread.Sleep(250);
        return true;
    }

    /// <summary>
    /// Clears the chat history queue and logs the action.
    /// </summary>
    /// <returns>True if the chat history is cleared successfully, otherwise false.</returns>
    public bool ClearChatHistory()
    {
        LogToFile("Attempting to clear chat history.", "DEBUG");
        // Verify if ChatLog is initialized before attempting to clear it.
        if (ChatLog == null)
        {
            LogToFile("ChatLog is not initialized and cannot be cleared.", "ERROR");
            CPH.SendMessage("Chat history is already empty.", true);
            return false;
        }

        try
        {
            // Clear the chat history queue.
            ChatLog.Clear();
            LogToFile("Chat history has been successfully cleared.", "INFO");
            // Inform the chat that the history has been cleared.
            CPH.SendMessage("Chat history has been cleared.", true);
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur and notify the chat.
            LogToFile($"An error occurred while clearing the chat history: {ex.Message}", "ERROR");
            CPH.SendMessage("I was unable to clear the chat history. Please check the log file for more details.", true);
            return false;
        }
    }

    /// <summary>
    /// Evaluates a message against moderation preferences and performs appropriate actions based on the moderation results.
    /// </summary>
    /// <returns>True if the message passes moderation, otherwise false.</returns>
    public bool PerformModeration()
    {
        LogToFile("Entering PerformModeration method.", "DEBUG");
        // Retrieve the raw input message from the args dictionary safely.
        string input = args["rawInput"]?.ToString();
        if (string.IsNullOrWhiteSpace(input))
        {
            LogToFile("'rawInput' value is either not found or not a valid string.", "ERROR");
            return false;
        }

        LogToFile($"Message for moderation: {input}", "INFO");
        // Load global variables for moderation preferences and construct exclusion list.
        var preferences = LoadModerationPreferences();
        var excludedCategories = preferences.Where(p => p.Value).Select(p => p.Key.Replace("_allowed", "").Replace("_", "/")).ToList();
        LogToFile($"Excluded categories for moderation: {string.Join(", ", excludedCategories)}", "DEBUG");
        try
        {
            // Call the moderation endpoint with the message and excluded categories.
            List<string> flaggedCategories = CallModerationEndpoint(input, excludedCategories.ToArray());
            if (flaggedCategories == null)
            {
                LogToFile("Moderation endpoint failed to respond or responded with an error.", "ERROR");
                return false;
            }

            // Handle the response from the moderation endpoint.
            bool moderationResult = HandleModerationResponse(flaggedCategories, input);
            LogToFile($"Moderation result: {(moderationResult ? "Passed" : "Failed")}", "INFO");
            return moderationResult;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the moderation process.
            LogToFile($"An error occurred in PerformModeration: {ex.Message}", "ERROR");
            return false;
        }
    }

    /// <summary>
    /// Loads the moderation preferences from global variables.
    /// </summary>
    /// <returns>Dictionary of moderation preferences.</returns>
    private Dictionary<string, bool> LoadModerationPreferences()
    {
        LogToFile("Loading moderation preferences.", "DEBUG");
        // Define the list of preferences to load.
        string[] preferenceKeys = new string[]
        {
            "hate_allowed",
            "hate_threatening_allowed",
            "self_harm_allowed",
            "self_harm_intent_allowed",
            "self_harm_instructions_allowed",
            "harassment_allowed",
            "harassment_threatening_allowed",
            "sexual_allowed",
            "violence_allowed",
            "violence_graphic_allowed"
        };
        // Load each preference into a dictionary.
        var preferences = new Dictionary<string, bool>();
        foreach (var key in preferenceKeys)
        {
            bool value = CPH.GetGlobalVar<bool>(key, true);
            preferences.Add(key, value);
            LogToFile($"Loaded moderation preference: {key} is set to {value}.", "DEBUG");
        }

        return preferences;
    }

    /// <summary>
    /// Handles the moderation response by logging the results and informing the user if necessary.
    /// </summary>
    /// <param name = "flaggedCategories">The categories in which the message was flagged.</param>
    /// <param name = "input">The original message that was moderated.</param>
    /// <returns>True if the message was not flagged, otherwise false.</returns>
    private bool HandleModerationResponse(List<string> flaggedCategories, string input)
    {
        if (flaggedCategories.Any())
        {
            string flaggedCategoriesString = string.Join(", ", flaggedCategories);
            string outputMessage = $"This message was flagged in the following categories: {flaggedCategoriesString}. Repeated attempts at abuse may result in a ban.";
            LogToFile(outputMessage, "INFO");
            // Attempt to retrieve the voice alias for TTS.
            string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
            if (string.IsNullOrWhiteSpace(voiceAlias))
            {
                LogToFile("'Voice Alias' global variable is not found or not a valid string.", "ERROR");
                return false;
            }

            // Speak out the moderation result using TTS.
            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            LogToFile($"TTS speak result: {speakResult}", "DEBUG");
            // Inform the user in the chat about the moderation result.
            CPH.SendMessage(outputMessage, true);
            return false;
        }
        else
        {
            // If the message is not flagged, set the moderated message for further use.
            CPH.SetArgument("moderatedMessage", input);
            LogToFile("Message passed moderation.", "DEBUG");
            return true;
        }
    }

    /// <summary>
    /// Calls the OpenAI Moderation API endpoint to check the content of the provided prompt against specified moderation categories.
    /// </summary>
    /// <param name = "prompt">The text to be moderated.</param>
    /// <param name = "excludedCategories">Categories to be excluded from the moderation result.</param>
    /// <returns>A list of categories in which the prompt was flagged, or null if an error occurs.</returns>
    private List<string> CallModerationEndpoint(string prompt, string[] excludedCategories)
    {
        LogToFile("Entering CallModerationEndpoint method.", "DEBUG");
        // Retrieve the OpenAI API Key from global variables.
        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogToFile("The OpenAI API Key is not set or is invalid.", "ERROR");
            return null;
        }

        try
        {
            // Define the moderation endpoint URL.
            string moderationEndpoint = "https://api.openai.com/v1/moderations";
            // Construct the request body for moderation.
            var moderationRequestBody = new
            {
                input = prompt
            };
            string moderationJsonPayload = JsonConvert.SerializeObject(moderationRequestBody);
            byte[] moderationContentBytes = Encoding.UTF8.GetBytes(moderationJsonPayload);
            // Create the web request and set the headers.
            WebRequest moderationWebRequest = WebRequest.Create(moderationEndpoint);
            moderationWebRequest.Method = "POST";
            moderationWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            moderationWebRequest.ContentType = "application/json";
            moderationWebRequest.ContentLength = moderationContentBytes.Length;
            // Log the sending of the moderation request.
            LogToFile("Sending moderation request to OpenAI API.", "DEBUG");
            // Write the request payload to the request stream.
            using (Stream requestStream = moderationWebRequest.GetRequestStream())
            {
                requestStream.Write(moderationContentBytes, 0, moderationContentBytes.Length);
            }

            // Get the response from the moderation request.
            using (WebResponse moderationWebResponse = moderationWebRequest.GetResponse())
            {
                using (Stream responseStream = moderationWebResponse.GetResponseStream())
                {
                    using (StreamReader responseReader = new StreamReader(responseStream))
                    {
                        // Read the response content.
                        string moderationResponseContent = responseReader.ReadToEnd();
                        LogToFile($"Received moderation response: {moderationResponseContent}", "DEBUG");
                        // Deserialize the response content to a ModerationResponse object.
                        var moderationJsonResponse = JsonConvert.DeserializeObject<ModerationResponse>(moderationResponseContent);
                        // Validate the moderation results.
                        if (moderationJsonResponse?.Results == null || !moderationJsonResponse.Results.Any())
                        {
                            LogToFile("No moderation results were returned from the API.", "ERROR");
                            return null;
                        }

                        // Extract flagged categories, excluding any specified categories.
                        List<string> flaggedCategories = moderationJsonResponse.Results[0].Categories.Where(category => category.Value && !excludedCategories.Contains(category.Key)).Select(category => category.Key).ToList();
                        // Log the moderation results.
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
            // Handle any web exceptions and log the response content if available.
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
            // Handle general exceptions and log the error message.
            LogToFile($"An exception occurred while calling the moderation endpoint: {ex.Message}", "ERROR");
            return null;
        }
    }

    /// <summary>
    /// Uses text-to-speech to vocalize a message with the user's preferred nickname or username.
    /// </summary>
    /// <returns>True if the message is spoken successfully, otherwise false.</returns>
    public bool Speak()
    {
        LogToFile("Entering Speak method.", "DEBUG");
        // Retrieve the voice alias for TTS from global variables.
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            LogToFile("'Voice Alias' global variable is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I was unable to speak that message. Please check the configuration.", true);
            return false;
        }

        // Retrieve the user's nickname or username for speaking.
        string userToSpeak = args.ContainsKey("nickname") && !string.IsNullOrWhiteSpace(args["nickname"].ToString()) ? args["nickname"].ToString() : args.ContainsKey("userName") && !string.IsNullOrWhiteSpace(args["userName"].ToString()) ? args["userName"].ToString() : "";
        if (string.IsNullOrWhiteSpace(userToSpeak))
        {
            LogToFile("Unable to retrieve a valid 'nickname' or 'userName' for speaking.", "ERROR");
            CPH.SendMessage("I was unable to speak that message. Please check the input.", true);
            return false;
        }

        // Retrieve the moderated message or the raw input for speaking.
        string messageToSpeak = args.ContainsKey("moderatedMessage") && !string.IsNullOrWhiteSpace(args["moderatedMessage"].ToString()) ? args["moderatedMessage"].ToString() : args.ContainsKey("rawInput") && !string.IsNullOrWhiteSpace(args["rawInput"].ToString()) ? args["rawInput"].ToString() : "";
        if (string.IsNullOrWhiteSpace(messageToSpeak))
        {
            LogToFile("Unable to retrieve a valid 'moderatedMessage' or 'rawInput' for speaking.", "ERROR");
            CPH.SendMessage("I was unable to speak that message. Please check the input.", true);
            return false;
        }

        // Construct the message to be spoken.
        string outputMessage = $"{userToSpeak} said: {messageToSpeak}";
        LogToFile($"Speaking message: {outputMessage}", "INFO");
        try
        {
            // Speak the message using the voice alias.
            int speakResult = CPH.TtsSpeak(voiceAlias, outputMessage, false);
            if (speakResult != 0)
            {
                // If TtsSpeak returns a non-zero value, log an error.
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

    /// <summary>
    /// Stores a keyword and associated definition in a JSON file for recall later.
    /// </summary>
    /// <returns>True if the memory is saved successfully, otherwise false.</returns>
    public bool RememberThis()
    {
        LogToFile("Entering RememberThis method.", "DEBUG");
        // Retrieve necessary global variables.
        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string userName = args.ContainsKey("userName") ? args["userName"].ToString() : "";
        string nicknamePronouns = args.ContainsKey("nicknamePronouns") ? args["nicknamePronouns"].ToString() : "";
        string userToConfirm = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : nicknamePronouns;
        string fullMessage = args.ContainsKey("moderatedMessage") && !string.IsNullOrWhiteSpace(args["moderatedMessage"].ToString()) ? args["moderatedMessage"].ToString() : args.ContainsKey("rawInput") && !string.IsNullOrWhiteSpace(args["rawInput"].ToString()) ? args["rawInput"].ToString() : "";
        // Initial validation of the input parameters and global variables.
        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(fullMessage))
        {
            string missingParameter = string.IsNullOrWhiteSpace(databasePath) ? "Database Path" : string.IsNullOrWhiteSpace(voiceAlias) ? "Voice Alias" : string.IsNullOrWhiteSpace(userName) ? "userName" : "message";
            LogToFile($"'{missingParameter}' value is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }

        try
        {
            // Process the full message to extract the keyword and the message to remember.
            var parts = fullMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                LogToFile("The message does not contain enough parts to extract a keyword and a definition.", "ERROR");
                CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
                return false;
            }

            // Extract the keyword and definition.
            string keyword = parts[0];
            string definition = string.Join(" ", parts.Skip(1));
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            // Ensure the file exists or create a new one.
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{}");
            }

            // Read the existing file content and update it with the new keyword and definition.
            string jsonContent = File.ReadAllText(filePath);
            var keywordContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            keywordContexts[keyword] = definition;
            // Write the updated dictionary back to the file.
            File.WriteAllText(filePath, JsonConvert.SerializeObject(keywordContexts, Formatting.Indented));
            LogToFile($"Keyword '{keyword}' and definition '{definition}' saved to {filePath}", "INFO");
            // Confirm the memory has been saved by sending a message and speaking out loud.
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

    /// <summary>
    /// Saves a piece of information about a user to a JSON file for future reference.
    /// </summary>
    /// <returns>True if the information is saved successfully, otherwise false.</returns>
    public bool RememberThisAboutMe()
    {
        LogToFile("Entering RememberThisAboutMe method.", "DEBUG");
        // Retrieve the database path and voice alias from global variables.
        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string userName = args.ContainsKey("userName") ? args["userName"].ToString() : "";
        string nicknamePronouns = args.ContainsKey("nicknamePronouns") ? args["nicknamePronouns"].ToString() : "";
        string userToConfirm = args.ContainsKey("nicknamePronouns") && !string.IsNullOrWhiteSpace(args["nicknamePronouns"].ToString()) ? args["nicknamePronouns"].ToString() : nicknamePronouns;
        string messageToRemember = args.ContainsKey("moderatedMessage") && !string.IsNullOrWhiteSpace(args["moderatedMessage"].ToString()) ? args["moderatedMessage"].ToString() : args.ContainsKey("rawInput") && !string.IsNullOrWhiteSpace(args["rawInput"].ToString()) ? args["rawInput"].ToString() : "";
        // Initial validation of the input parameters and global variables.
        if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(messageToRemember))
        {
            string missingParameter = string.IsNullOrWhiteSpace(databasePath) ? "Database Path" : string.IsNullOrWhiteSpace(voiceAlias) ? "Voice Alias" : string.IsNullOrWhiteSpace(userName) ? "userName" : "messageToRemember";
            LogToFile($"'{missingParameter}' value is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't remember that right now. Please try again later.", true);
            return false;
        }

        try
        {
            // Construct the file path and ensure the file exists.
            string filePath = Path.Combine(databasePath, "keyword_contexts.json");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "{}");
            }

            // Read the existing file content and update it with the new user information.
            string jsonContent = File.ReadAllText(filePath);
            var userContexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
            userContexts[userName] = messageToRemember;
            // Write the updated dictionary back to the file.
            File.WriteAllText(filePath, JsonConvert.SerializeObject(userContexts, Formatting.Indented));
            LogToFile($"Information about user '{userName}' saved: {messageToRemember}", "INFO");
            // Confirm the information has been saved by sending a message and speaking out loud.
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

    /// <summary>
    /// Clears the stored GPTLog queue containing the history of prompts.
    /// </summary>
    /// <returns>True if the prompt history is cleared successfully, otherwise false.</returns>
    public bool ClearPromptHistory()
    {
        LogToFile("Entering ClearPromptHistory method.", "DEBUG");
        LogToFile("Attempting to clear prompt history.", "DEBUG");
        // Verify if GPTLog is initialized before attempting to clear it.
        if (GPTLog == null)
        {
            LogToFile("GPTLog is not initialized and cannot be cleared.", "ERROR");
            CPH.SendMessage("Prompt history is already empty.", true);
            return false;
        }

        try
        {
            // Clear the prompt history queue.
            GPTLog.Clear();
            LogToFile("Prompt history has been successfully cleared.", "INFO");
            // Inform the chat that the history has been cleared.
            CPH.SendMessage("Prompt history has been cleared.", true);
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur and notify the chat.
            LogToFile($"An error occurred while clearing the prompt history: {ex.Message}", "ERROR");
            CPH.SendMessage("I was unable to clear the prompt history. Please check the log file for more details.", true);
            return false;
        }
    }

    /// <summary>
    /// Sends a user's message to the GPT model and handles the response, including storing context and speaking the response aloud.
    /// </summary>
    /// <returns>True if the GPT model provides a response, otherwise false.</returns>
    public bool AskGPT()
    {
        LogToFile("Entering AskGPT method.", "DEBUG");
        // Check if the ChatLog has been initialized and log the chat history if it exists.
        if (ChatLog == null)
        {
            ChatLog = new Queue<chatMessage>();
            LogToFile("ChatLog queue has been initialized for the first time.", "INFO");
        }
        else
        {
            // Using LINQ to concatenate the message contents, separated by newlines.
            string chatLogAsString = string.Join(Environment.NewLine, ChatLog.Select(m => m.content ?? "null"));
            LogToFile($"ChatLog Content before asking GPT: {Environment.NewLine}{chatLogAsString}", "INFO");
        }

        // Retrieve and validate the voice alias global variable.
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        if (string.IsNullOrWhiteSpace(voiceAlias))
        {
            LogToFile("'Voice Alias' global variable is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        LogToFile("Retrieved and validated 'Voice Alias' global variable.", "DEBUG");
        // Retrieve and validate the user name argument.
        string userName;
        if (!args.TryGetValue("userName", out object userNameObj) || string.IsNullOrWhiteSpace(userNameObj?.ToString()))
        {
            LogToFile("'userName' argument is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        userName = userNameObj.ToString();
        LogToFile("Retrieved and validated 'userName' argument.", "DEBUG");
        // Determine the nickname or username to speak.
        string userToSpeak = args.TryGetValue("nicknamePronouns", out object nicknameObj) && !string.IsNullOrWhiteSpace(nicknameObj?.ToString()) ? nicknameObj.ToString() : userName;
        if (string.IsNullOrWhiteSpace(userToSpeak))
        {
            LogToFile("Both 'nicknamePronouns' and 'userName' are not found or are empty strings.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        // Retrieve and validate the database path global variable.
        string databasePath = CPH.GetGlobalVar<string>("Database Path");
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            LogToFile("'Database Path' global variable is not found or not a valid string.", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
            return false;
        }

        LogToFile("Retrieved and validated 'Database Path' global variable.", "DEBUG");
        // Retrieve the full message to process, either the moderated message or the raw input.
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

        // Construct the paths to the context and keyword context files and ensure they exist.
        string ContextFilePath = Path.Combine(databasePath, "context.txt");
        string keywordContextFilePath = Path.Combine(databasePath, "keyword_contexts.json");
        LogToFile("Constructed file paths for context and keyword context storage.", "DEBUG");
        // Check if the keyword context file exists and read its contents; otherwise, initialize an empty dictionary.
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

        // Load additional context from files and global variables.
        string context = File.Exists(ContextFilePath) ? File.ReadAllText(ContextFilePath) : "";
        string broadcaster = CPH.GetGlobalVar<string>("broadcaster", false);
        string currentTitle = CPH.GetGlobalVar<string>("currentTitle", false);
        string currentGame = CPH.GetGlobalVar<string>("currentGame", false);
        string contextBody = $"{context}\nWe are currently doing: {currentTitle}\n{broadcaster} is currently playing: {currentGame}";
        LogToFile("Assembled context body for GPT prompt.", "DEBUG");
        // Formulate the prompt for GPT.
        string prompt = $"{userToSpeak} asks: {fullMessage}";
        LogToFile($"Constructed prompt for GPT: {prompt}", "DEBUG");
        // Check for mentions of keywords within the prompt and add relevant context.
        bool keywordMatch = keywordContexts.Keys.Any(keyword => prompt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        if (keywordMatch)
        {
            // If a keyword is mentioned, append its context to the body.
            string keyword = keywordContexts.Keys.First(keyword => prompt.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            string keywordPhrase = $"Something you know about {keyword} is:";
            string keywordValue = keywordContexts[keyword];
            contextBody += $"\n{keywordPhrase} {keywordValue}\n";
            LogToFile("Added keyword-specific context to context body.", "DEBUG");
        }

        // Check for user-specific context and append it as well.
        if (keywordContexts.ContainsKey(userName))
        {
            string usernamePhrase = $"Something you know about {userToSpeak} is:";
            string usernameValue = keywordContexts[userName];
            contextBody += $"\n{usernamePhrase} {usernameValue}\n";
            LogToFile("Added user-specific context to context body.", "DEBUG");
        }

        try
        {
            // Call the GPT model with the prompt and the context.
            string GPTResponse = GenerateChatCompletion(prompt, contextBody); // Placeholder for the actual GPT call.
            if (string.IsNullOrWhiteSpace(GPTResponse))
            {
                LogToFile("GPT model did not return a response.", "ERROR");
                CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please check the log for details.", true);
                return false;
            }

            LogToFile($"GPT model response: {GPTResponse}", "DEBUG");
            // Speak the GPT response.
            CPH.TtsSpeak(voiceAlias, GPTResponse, false);
            LogToFile("Spoke GPT's response.", "INFO");
            // Send the response in chunks to the chat if it's longer than a certain length.
            if (GPTResponse.Length > 500)
            {
                LogToFile("The response is too long for Twitch; it will be sent in chunks to the chat.", "INFO");
                int startIndex = 0;
                while (startIndex < GPTResponse.Length)
                {
                    // Determine the chunk size dynamically based on punctuation or space before 500 characters.
                    int chunkSize = Math.Min(500, GPTResponse.Length - startIndex);
                    int endIndex = startIndex + chunkSize;
                    // Look for the last full word or punctuation if the chunkSize is less than the total length.
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

                    // Extract the substring for the current chunk.
                    string messageChunk = GPTResponse.Substring(startIndex, endIndex - startIndex).Trim();
                    CPH.SendMessage(messageChunk, true);
                    // Update the startIndex for the next loop iteration.
                    startIndex = endIndex;
                    // Sleep after sending each message chunk to avoid flooding.
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
            // Log any exceptions that occur and notify the chat.
            LogToFile($"An error occurred while processing the AskGPT request: {ex.Message}", "ERROR");
            CPH.SendMessage("I'm sorry, but I can't answer that question right now. Please try again later.", true);
            return false;
        }
    }

    /// <summary>
    /// Removes emojis and other non-ASCII characters from the provided text.
    /// </summary>
    /// <param name = "text">The text from which emojis should be removed.</param>
    /// <returns>The sanitized text without emojis.</returns>
    private string RemoveEmojis(string text)
    {
        LogToFile("Entering RemoveEmojis method.", "DEBUG");
        // Log the original text if needed for debugging.
        LogToFile($"Original text before removing emojis: {text}", "DEBUG");
        // Regular expression pattern to match emoji characters specifically
        string emojiPattern = @"[\uD83C-\uDBFF\uDC00-\uDFFF]";  // Use Unicode property for emoji characters
        // Log the regular expression pattern used for removing emojis.
        LogToFile($"Using regex pattern to remove emojis: {emojiPattern}", "DEBUG");
        // Regular expression to match and remove emojis (non-ASCII characters) from the 'text' string.
        string sanitizedText = Regex.Replace(text, emojiPattern, "");
        // Log the text after removing emojis.
        LogToFile($"Text after removing emojis: {sanitizedText}", "DEBUG");
        // Remove any extra spaces left after removing the emojis.
        sanitizedText = Regex.Replace(sanitizedText, @"\s+", " ").Trim();
        // Log the final sanitized text.
        LogToFile($"Sanitized text without emojis: {sanitizedText}", "INFO");
        return sanitizedText;
    }

    /// <summary>
    /// Generates a response from the GPT model using the provided prompt and context.
    /// </summary>
    /// <param name = "prompt">The user's prompt to the GPT model.</param>
    /// <param name = "contextBody">The context body to provide background information to the GPT model.</param>
    /// <returns>The generated response text from the GPT model.</returns>
    public string GenerateChatCompletion(string prompt, string contextBody)
    {
        LogToFile("Entering GenerateChatCompletion method.", "DEBUG");
        // Initialize the variable to hold the generated text.
        string generatedText = string.Empty;
        // Retrieve the necessary global variables for OpenAI API.
        string apiKey = CPH.GetGlobalVar<string>("OpenAI API Key", true);
        string voiceAlias = CPH.GetGlobalVar<string>("Voice Alias", true);
        string AIModel = CPH.GetGlobalVar<string>("OpenAI Model", true);
        // Logging the retrieved API key, voice alias, and AI model for debugging purposes.
        LogToFile($"Voice Alias: {voiceAlias}, AI Model: {AIModel}", "DEBUG");
        // Check if the necessary configuration values are present.
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceAlias) || string.IsNullOrWhiteSpace(AIModel))
        {
            LogToFile("One or more configuration values are missing or invalid. Please check the OpenAI API Key, Voice Alias, and AI Model settings.", "ERROR");
            return "Configuration error. Please check the log for details.";
        }

        LogToFile("All configuration values are valid and present.", "DEBUG");
        // Define the OpenAI completions endpoint.
        string completionsEndpoint = "https://api.openai.com/v1/chat/completions";
        // Log that all necessary configuration values are present.
        LogToFile("All configuration values are valid and present.", "DEBUG");
        // Construct the list of messages including system, assistant, and user messages.
        var messages = new List<chatMessage>
        {
            new chatMessage
            {
                role = "system",
                content = contextBody
            },
            // Add instruction message for the GPT model here.
            new chatMessage
            {
                role = "user",
                content = "I am going to send you the chat log from Twitch. You should reference these messages for all future prompts if it is relevant to the prompt being asked. Each message will be prefixed with the users name that you can refer to them as, if referring to their message in the response. After each message you receive, you will return simply \"OK\" to indicate you have received this message, and no other text. When I am finished I will say FINISHED, and you will again respond with simply \"OK\" and nothing else, and then resume normal operation on all future prompts."
            },
            // Assistant's acknowledgment message.
            new chatMessage
            {
                role = "assistant",
                content = "OK"
            }
        };
        // Process user messages stored in ChatLog.
        if (ChatLog != null)
        {
            foreach (var chatMessage in ChatLog)
            {
                messages.Add(chatMessage);
                // Assistant's acknowledgment message for each user message.
                messages.Add(new chatMessage { role = "assistant", content = "OK" });
            }
        }

        // Add the "FINISHED" message from the user and final "OK" from the assistant.
        messages.Add(new chatMessage { role = "user", content = "FINISHED" });
        messages.Add(new chatMessage { role = "assistant", content = "OK" });
        // Add messages from GPTLog after the chat log has been sent.
        if (GPTLog != null)
        {
            foreach (var gptMessage in GPTLog)
            {
                messages.Add(gptMessage);
            }
        }

        // Finally, add the user's current prompt to the messages.
        messages.Add(new chatMessage { role = "user", content = $"{prompt} You must respond in less than 500 characters." });
        // Serialize the completion request to JSON.
        string completionsRequestJSON = JsonConvert.SerializeObject(new { model = AIModel, messages = messages }, Formatting.Indented);
        // Log the JSON payload that will be sent to the OpenAI API.
        LogToFile($"Request JSON: {completionsRequestJSON}", "DEBUG");
        // Create and configure the web request to the OpenAI API.
        WebRequest completionsWebRequest = WebRequest.Create(completionsEndpoint);
        completionsWebRequest.Method = "POST";
        completionsWebRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        completionsWebRequest.ContentType = "application/json";
        // Send the request and handle the response.
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

                // Check the 'Strip Emojis From Response' setting and remove emojis from response if set to true.
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

        // Handle the case where GPT does not return a response.
        if (string.IsNullOrEmpty(generatedText))
        {
            generatedText = "ChatGPT did not return a response.";
            LogToFile("The GPT model did not return any text.", "ERROR");
        }
        else
        {
            // Replace line breaks with spaces in the generated text.
            generatedText = generatedText.Replace("\r\n", " ").Replace("\n", " ");
            LogToFile($"Prompt: {prompt}", "INFO");
            LogToFile($"Response: {generatedText}", "INFO");
        }

        // Enqueue the user's prompt and the assistant's generated text to GPTLog as a balanced pair.
        // The QueueGPTMessage method ensures that there is an equal number of user and assistant messages.
        // It maintains the limit of 5 user messages and 5 assistant messages within GPTLog.
        QueueGPTMessage(prompt, generatedText);
        // Optionally post question and answer to Discord if enabled.
        PostToDiscord(prompt, generatedText);
        // Return the processed text.
        return generatedText;
    }

    private void PostToDiscord(string question, string answer)
    {
        // Check if Discord logging is enabled.
        bool logDiscord = CPH.GetGlobalVar<bool>("Log GPT Questions to Discord", true);
        // If it's disabled, log this status and exit the method.
        if (!logDiscord)
        {
            LogToFile("Posting to Discord is disabled. The message will not be sent.", "INFO");
            return;
        }

        // Retrieve Discord webhook settings.
        string discordWebhookUrl = CPH.GetGlobalVar<string>("Discord Webhook URL", true);
        string discordUsername = CPH.GetGlobalVar<string>("Discord Bot Username", true);
        string discordAvatarUrl = CPH.GetGlobalVar<string>("Discord Avatar Url", true);
        // Log the retrieval of Discord webhook settings.
        LogToFile("Retrieved Discord webhook settings.", "DEBUG");
        // Construct the message for Discord.
        string discordOutput = $"Question: {question}\nAnswer: {answer}";
        // Log the attempt to post to Discord.
        LogToFile($"Attempting to post to Discord: {discordOutput}", "INFO");
        // Try to post the message to Discord and handle any errors.
        try
        {
            // Use the provided webhook URL to post the message to Discord.
            CPH.DiscordPostTextToWebhook(discordWebhookUrl, discordOutput, discordUsername, discordAvatarUrl, false);
            LogToFile("The message was successfully posted to Discord.", "INFO");
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the posting process.
            LogToFile($"Failed to post the message to Discord: {ex.Message}", "ERROR");
        }
    }

    /// <summary>
    /// Retrieves and sets global variables with the current streaming information using Twitch API.
    /// </summary>
    /// <returns>True if the information is retrieved and set successfully, otherwise false.</returns>
    public bool GetStreamInfo()
    {
        LogToFile("Attempting to retrieve stream information.", "DEBUG");
        // Start an asynchronous task to get all streaming data.
        Task<AllDatas> getAllDatasTask = FunctionGetAllDatas();
        getAllDatasTask.Wait(); // Synchronously wait for the task to complete.
        AllDatas datas = getAllDatasTask.Result; // Retrieve the result of the asynchronous operation.
        if (datas != null)
        {
            // Set global variables with the retrieved streaming information.
            CPH.SetGlobalVar("broadcaster", datas.UserName, false);
            CPH.SetGlobalVar("currentGame", datas.gameName, false);
            CPH.SetGlobalVar("currentTitle", datas.titleName, false);
            // Log the retrieved information for debugging purposes.
            LogToFile($"Retrieved stream information: Broadcaster - {datas.UserName}, Game - {datas.gameName}, Title - {datas.titleName}", "INFO");
            return true; // Return true to indicate successful execution.
        }
        else
        {
            // Log an error if the data could not be retrieved.
            LogToFile("Failed to retrieve stream information.", "ERROR");
            return false;
        }
    }

    /// <summary>
    /// Asynchronously gets streaming data from the Twitch API using WebRequest.
    /// </summary>
    /// <returns>A task that on completion provides the streaming data.</returns>
    public async Task<AllDatas> FunctionGetAllDatas()
    {
        LogToFile("Starting retrieval of stream data using WebRequest.", "DEBUG");
        // Retrieve the broadcast user ID from the script arguments.
        string broadcasterId = args["broadcastUserId"].ToString();
        LogToFile($"Broadcast user ID: {broadcasterId}", "DEBUG");
        // Prepare the URL for the Twitch API request.
        string twitchApiEndpoint = "https://api.twitch.tv/helix/channels?broadcaster_id=" + broadcasterId;
        LogToFile($"Twitch API endpoint: {twitchApiEndpoint}", "DEBUG");
        // Prepare the request.
        string clientIdValue = CPH.TwitchClientId;
        string tokenValue = CPH.TwitchOAuthToken;
        WebRequest request = WebRequest.Create(twitchApiEndpoint);
        request.Method = "GET";
        request.Headers.Add("Client-ID", clientIdValue);
        request.Headers.Add("Authorization", "Bearer " + tokenValue);
        request.ContentType = "application/json";
        try
        {
            // Get the response from the request.
            using (WebResponse response = await request.GetResponseAsync())
            {
                // Read the response stream.
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseBody = await reader.ReadToEndAsync();
                    LogToFile($"Response from Twitch API: {responseBody}", "DEBUG");
                    // Deserialize the JSON response into a Root object.
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

    /// <summary>
    /// Logs the specified message to a file based on the current globally defined logging level.
    /// </summary>
    /// <param name = "logItem">The log message to be written.</param>
    /// <param name = "logLevel">The log level of the message (DEBUG, ERROR, WARN, INFO).</param>
    private void LogToFile(string logItem, string logLevel)
    {
        // Retrieve the global logging level setting.
        string globalLogLevel = CPH.GetGlobalVar<string>("Logging Level", true);
        // Check if the 'Logging Level' global variable is found. If not, log an error and throw an exception.
        if (globalLogLevel == null)
        {
            string notFoundMessage = "The 'Logging Level' global variable is not found.";
            CPH.LogError(notFoundMessage);
            throw new InvalidOperationException(notFoundMessage);
        }

        // Validate the global log level and throw an exception if it is not an expected value.
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

        // Define the order of logging levels.
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
        // Only proceed with logging if the log level is at or above the global log level.
        if (logLevelPriority[logLevel] >= logLevelPriority[globalLogLevel])
        {
            // Retrieve the database path from the global variables and ensure the 'logs' subdirectory exists.
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            string logDirectoryPath = Path.Combine(databasePath, "logs");
            if (!Directory.Exists(logDirectoryPath))
            {
                Directory.CreateDirectory(logDirectoryPath);
            }

            // Define the log file name based on the current date.
            string logFileName = DateTime.Now.ToString("PNGTuber-GPT_yyyyMMdd") + ".log";
            string logFilePath = Path.Combine(logDirectoryPath, logFileName);
            // Create the log entry with the current timestamp and log level.
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {logLevel}] {logItem}{Environment.NewLine}";
            // Append the log entry to the log file.
            File.AppendAllText(logFilePath, logEntry);
        }
    }

    /// <summary>
    /// Clears the contents of the current day's log file by setting it to an empty string.
    /// </summary>
    public bool ClearLogFile()
    {
        // Retrieve the database path from the global variables.
        string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
        if (string.IsNullOrEmpty(databasePath))
        {
            string errorMessage = "The 'Database Path' global variable is not found or is empty.";
            LogToFile(errorMessage, "ERROR");
            return false;
        }

        // Define the path to the 'logs' subdirectory.
        string logDirectoryPath = Path.Combine(databasePath, "logs");
        if (!Directory.Exists(logDirectoryPath))
        {
            // If the logs directory doesn't exist, there's no log file to clear.
            LogToFile("The 'logs' subdirectory does not exist. No log file to clear.", "INFO");
            return false;
        }

        // Define the log file name based on the current date.
        string logFileName = DateTime.Now.ToString("PNGTuber-GPT_yyyyMMdd") + ".log";
        string logFilePath = Path.Combine(logDirectoryPath, logFileName);
        // Check if the log file exists.
        if (File.Exists(logFilePath))
        {
            try
            {
                // Clear the contents of the log file by setting it to an empty string.
                File.WriteAllText(logFilePath, $"Cleared the log file: {logFileName}");
                File.WriteAllText(logFilePath, string.Empty);
                CPH.SendMessage($"Cleared the log file.", true);
                return true;
            }
            catch (Exception ex)
            {
                // If there's an error clearing the log file, log the exception.
                LogToFile($"An error occurred while clearing the log file: {ex.Message}", "ERROR");
                CPH.SendMessage("An error occurred while clearing the log file.", true);
                return false;
            }
        }
        else
        {
            // If the log file doesn't exist for the current day, log this information.
            LogToFile("No log file exists for the current day to clear.", "INFO");
            CPH.SendMessage("No log file exists for the current day to clear.", true);
            return false;
        }
    }

    /// <summary>
    /// Sends the current version number of the PNGTuber-GPT application, retrieved from a global variable, to the chat.
    /// </summary>
    /// <returns>True to indicate the message was sent successfully.</returns>
    public bool Version()
    {
        // Log the start of the version number retrieval process.
        LogToFile("Starting to retrieve the version number from a global variable.", "DEBUG");
        // Retrieve the version number from a global variable.
        string versionNumber = CPH.GetGlobalVar<string>("Version", true);
        // Log the retrieved version number for debugging purposes.
        LogToFile($"Retrieved version number: {versionNumber}", "DEBUG");
        // Check if the version number was successfully retrieved.
        if (string.IsNullOrWhiteSpace(versionNumber))
        {
            // Log an error if the version number is not found or is empty.
            LogToFile("The 'Version' global variable is not found or is empty.", "ERROR");
            return false;
        }

        // Log the sending of the version number to the chat for debugging purposes.
        LogToFile($"Sending version number to chat: {versionNumber}", "DEBUG");
        // Send the version number to the chat.
        CPH.SendMessage(versionNumber, true);
        // Log the result of sending the version number.
        LogToFile("Version number sent to chat successfully.", "INFO");
        // Return true to indicate the version number has been sent successfully.
        return true;
    }

    /// <summary>
    /// Sends the current version number of the PNGTuber-GPT application, retrieved from a global variable, to the chat.
    /// </summary>
    /// <returns>True to indicate the message was sent successfully.</returns>
    public bool SayPlay()
    {
        // Log the start of SayPlayMethod
        LogToFile("Entering the SayPlay method.", "DEBUG");
        // Send the !play command to chat
        CPH.SendMessage("!play", true);
        // Log the result of sending the version number.
        LogToFile("Sent !play command to chat successfully.", "INFO");
        // Return true to indicate the version number has been sent successfully.
        return true;
    }

    /// <summary>
    /// Retrieves the nickname from the file.
    /// </summary>
    /// <returns>True if the nickname is found or not found; otherwise, false if an error occurs.</returns>
    public bool GetNickname()
    {
        try
        {
            // Get the path where the database is stored
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' value is either not found or not a valid string.", "ERROR");
                return false;
            }

            // Check if the preferred usernames file exists, create if not
            string filePath = Path.Combine(databasePath, "preferred_userNames.json");
            if (!File.Exists(filePath))
            {
                LogToFile("'preferred_userNames.json' does not exist. Creating default file.", "WARN");
                CreateDefaultUserNameFile(filePath);
            }

            // Retrieve the userName value from the args dictionary
            string userName = args.ContainsKey("userName") ? args["userName"].ToString() : "";
            // Retrieve the preferred username
            string preferredUserName = GetPreferredUsername(userName, filePath);
            if (string.IsNullOrWhiteSpace(preferredUserName))
            {
                LogToFile("Preferred user name could not be retrieved.", "WARN");
            }

            // Set the nickname argument
            CPH.SetArgument("nickname", preferredUserName);
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during the method execution
            LogToFile($"An error occurred in GetNickname: {ex.Message}", "ERROR");
            return false;
        }
    }

    /// <summary>
    /// Saves the settings to a JSON file in the specified database path.
    /// </summary>
    /// <returns>True if the settings are saved successfully; otherwise, false if an error occurs.</returns>
    public bool SaveSettings()
    {
        try
        {
            // Log the entry of the method
            LogToFile("Entering SaveSettings method.", "DEBUG");
            // Retrieve the settings from the global variables
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
                HateAllowed = CPH.GetGlobalVar<string>("hate_allowed", true),
                HateThreateningAllowed = CPH.GetGlobalVar<string>("hate_thretening_allowed", true),
                SelfHarmAllowed = CPH.GetGlobalVar<string>("self-harm_allowed", true),
                ViolenceAllowed = CPH.GetGlobalVar<string>("violence_allowed", true),
                SelfHarmIntentAllowed = CPH.GetGlobalVar<string>("self-harm_intent_allowed", true),
                SelfHarmInstructionsAllowed = CPH.GetGlobalVar<string>("self-harm_instructions_allowed", true),
                HarassmentAllowed = CPH.GetGlobalVar<string>("harrassment_allowed", true),
                HarassmentThreateningAllowed = CPH.GetGlobalVar<string>("harrassment_threatening_allowed", true),
                LogGptQuestionsToDiscord = CPH.GetGlobalVar<string>("Log GPT Questions to Discord", true),
                DiscordWebhookUrl = CPH.GetGlobalVar<string>("Discord Webhook URL", true),
                DiscordBotUsername = CPH.GetGlobalVar<string>("Discord Bot Username", true),
                DiscordAvatarUrl = CPH.GetGlobalVar<string>("Discord Avatar Url", true)
            };
            // Log the values of the settings (you can customize the format)
            LogToFile($"OpenApiKey: {settings.OpenApiKey}", "DEBUG");
            LogToFile($"OpenAiModel: {settings.OpenAiModel}", "DEBUG");
            // Add similar logging for other settings...
            // Check if any of the settings are null or empty
            if (string.IsNullOrWhiteSpace(settings.OpenApiKey) || string.IsNullOrWhiteSpace(settings.OpenAiModel) || string.IsNullOrWhiteSpace(settings.DatabasePath) || string.IsNullOrWhiteSpace(settings.IgnoreBotUsernames) || string.IsNullOrWhiteSpace(settings.VoiceAlias) || string.IsNullOrWhiteSpace(settings.LoggingLevel) || string.IsNullOrWhiteSpace(settings.Version) || string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl) || string.IsNullOrWhiteSpace(settings.DiscordBotUsername) || string.IsNullOrWhiteSpace(settings.DiscordAvatarUrl))
            {
                LogToFile("One or more settings are null or empty.", "WARN");
                return false;
            }

            // Convert the settings object to JSON
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            // Save the JSON to the settings.json file in the specified database path
            var filePath = Path.Combine(settings.DatabasePath, "settings.json");
            File.WriteAllText(filePath, json);
            // Log the values of the settings
            LogToFile($"Settings saved successfully. Settings: {json}", "INFO");
            // Log the success message for encryption
            LogToFile("Encryption of OpenAI API Key successful.", "INFO");
            // Log the exit of the method
            LogToFile("Exiting SaveSettings method.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            // Log the error if there was an exception during saving
            LogToFile($"Error saving settings: {ex.Message}", "ERROR");
            return false;
        }
    }

    public bool ReadSettings()
    {
        try
        {
            LogToFile("Entering ReadSettings method.", "DEBUG");
            // Get the path where the database is stored
            string databasePath = CPH.GetGlobalVar<string>("Database Path", true);
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                LogToFile("'Database Path' value is either not found or not a valid string.", "ERROR");
                return false;
            }

            // Construct the path to the settings file
            string filePath = Path.Combine(databasePath, "settings.json");
            // Check if the settings file exists
            if (!File.Exists(filePath))
            {
                LogToFile("Settings file not found.", "WARN");
                return false;
            }

            // Read the JSON from the file
            string json = File.ReadAllText(filePath);
            // Deserialize the JSON into an instance of AppSettings
            AppSettings settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json);
            // Set the global variables using the SetGlobalVar method
            CPH.SetGlobalVar("OpenAI API Key", DecryptData(settings.OpenApiKey), true);
            CPH.SetGlobalVar("OpenAI Model", settings.OpenAiModel, true);
            CPH.SetGlobalVar("Database Path", settings.DatabasePath, true);
            CPH.SetGlobalVar("Ignore Bot Usernames", settings.IgnoreBotUsernames, true);
            CPH.SetGlobalVar("Voice Alias", settings.VoiceAlias, true);
            CPH.SetGlobalVar("Strip Emojis From Response", settings.StripEmojisFromResponse, true);
            CPH.SetGlobalVar("Logging Level", settings.LoggingLevel, true);
            CPH.SetGlobalVar("Version", settings.Version, true);
            CPH.SetGlobalVar("hate_allowed", settings.HateAllowed, true);
            CPH.SetGlobalVar("hate_thretening_allowed", settings.HateThreateningAllowed, true);
            CPH.SetGlobalVar("self-harm_allowed", settings.SelfHarmAllowed, true);
            CPH.SetGlobalVar("violence_allowed", settings.ViolenceAllowed, true);
            CPH.SetGlobalVar("self-harm_intent_allowed", settings.SelfHarmIntentAllowed, true);
            CPH.SetGlobalVar("self-harm_instructions_allowed", settings.SelfHarmInstructionsAllowed, true);
            CPH.SetGlobalVar("harrassment_allowed", settings.HarassmentAllowed, true);
            CPH.SetGlobalVar("harrassment_threatening_allowed", settings.HarassmentThreateningAllowed, true);
            CPH.SetGlobalVar("Log GPT Questions to Discord", settings.LogGptQuestionsToDiscord, true);
            CPH.SetGlobalVar("Discord Webhook URL", settings.DiscordWebhookUrl, true);
            CPH.SetGlobalVar("Discord Bot Username", settings.DiscordBotUsername, true);
            CPH.SetGlobalVar("Discord Avatar Url", settings.DiscordAvatarUrl, true);
            // Log the values of the settings
            LogToFile($"Settings loaded successfully. Settings: {json}", "INFO");
            LogToFile("Exiting ReadSettings method.", "DEBUG");
            return true;
        }
        catch (Exception ex)
        {
            // Log the error if there was an exception during reading the settings
            LogToFile($"Error reading settings: {ex.Message}", "ERROR");
            return false;
        }
    }

    /// <summary>
    /// Encrypts data using AES with machine-specific key management.
    /// </summary>
    /// <param name = "data">The data to be encrypted.</param>
    /// <returns>The base64 encoded encrypted data, or null if an error occurs.</returns>
    private string EncryptData(string data)
    {
        try
        {
            LogToFile("Entering EncryptData method.", "DEBUG");
            // Encrypt the data using ProtectedData with current user scope and entropy
            LogToFile("Encrypting data with user-specific key.", "INFO");
            byte[] encryptedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(data), null, DataProtectionScope.CurrentUser);
            // Convert the encrypted data to base64 string for storage or transmission
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

    /// <summary>
    /// Decrypts data using AES with machine-specific key management.
    /// </summary>
    /// <param name = "data">The data to be encrypted.</param>
    /// <returns>The decrypted data as a string, or null if an error occurs.</returns>
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

            // Convert the base64 string to byte array
            byte[] encryptedDataBytes = Convert.FromBase64String(encryptedData);
            // Decrypt the data using ProtectedData with user scope
            byte[] decryptedData = ProtectedData.Unprotect(encryptedDataBytes, null, DataProtectionScope.CurrentUser);
            // Convert the decrypted data to a string
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