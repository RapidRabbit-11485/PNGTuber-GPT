const fs = require('fs');
const tmi = require('tmi.js');

const client = new tmi.Client({
  channels: ['rapidrabbit11485'],
});

const logFile = 'message_log.json';
const maxMessages = 15;
let messageBuffer = [];

client.connect()
  .then(() => {
    console.log('Connected to Twitch chat');
  })
  .catch((err) => {
    console.error('Error connecting to Twitch chat:', err);
    process.exit(1);
  });

client.on('message', (channel, tags, message, self) => {
  const strippedMessage = message.replace(/[^\x00-\x7F]/g, '');
  const logData = {
    displayName: tags['display-name'],
    message: strippedMessage,
  };

  messageBuffer.push(logData);

  // If the buffer size exceeds the limit, remove the oldest messages
  if (messageBuffer.length > maxMessages) {
    const removedMessages = messageBuffer.slice(0, messageBuffer.length - maxMessages);
    messageBuffer = messageBuffer.slice(-maxMessages); // Keep only the last maxMessages in the buffer
    removedMessages.forEach((msg) => {
      appendMessageToFile(msg);
    });
    deleteOldestLinesFromFile(maxMessages);
  }

  console.log(`${logData.displayName}: ${logData.message}`);
  appendMessageToFile(logData);
});

// Wait for the user to press any key to stop the listener
console.log('Press any key to stop the listener.');
process.stdin.setRawMode(true);
process.stdin.resume();
process.stdin.on('data', () => {
  console.log('Listener stopped.');
  process.exit();
});

function appendMessageToFile(data) {
  fs.appendFile(logFile, JSON.stringify(data) + '\n', (err) => {
    if (err) {
      console.error('Error appending message to file:', err);
    }
  });
}

function deleteOldestLinesFromFile(remainingLines) {
  fs.readFile(logFile, 'utf8', (err, data) => {
    if (err) {
      console.error('Error reading log file:', err);
      return;
    }

    const lines = data.trim().split('\n');
    if (lines.length > remainingLines) {
      const linesToKeep = lines.slice(-remainingLines);
      const newContent = linesToKeep.join('\n') + '\n';
      fs.writeFile(logFile, newContent, (err) => {
        if (err) {
          console.error('Error writing to log file:', err);
        }
      });
    }
  });
}
