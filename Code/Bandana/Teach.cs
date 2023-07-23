using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

public class CPHInline
{
    private string responseVariable; // Variable to store the response value
    public bool Execute()
    {
        // Load existing JSON data from file
        string projectFilePath = args["PROJECT_FILE_PATH"].ToString();
        string filePath = projectFilePath + "`\\keyword_contexts.json";
        string userName = args["userName"].ToString();
        CPH.LogInfo("Username: " + userName);
        string rawInput = args["rawInput"].ToString();
        CPH.LogInfo("Remember this: " + rawInput);
        string jsonData = File.ReadAllText(filePath);
        // Parse JSON data into a JObject
        JObject jsonObject = JObject.Parse(jsonData);
        // Update the JObject with new key-value pairs
        jsonObject[userName] = rawInput;
        // Convert the updated JObject back to a string
        string updatedJsonData = jsonObject.ToString(Formatting.Indented);
        // Save the updated JSON data back to the file
        File.WriteAllText(filePath, updatedJsonData);
        responseVariable = $"{userName} I will remember {rawInput} about you.";
        CPH.SetArgument("response", responseVariable);
        return true;
    }
}