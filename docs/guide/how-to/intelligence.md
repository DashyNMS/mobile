# Ask about your network with Intelligence

Intelligence answers questions in plain words: what's alerting, how a device is doing or what changed overnight. It looks things up with read-only tools, so it can't change anything on your network.

On iPhone and iPad, Apple Intelligence works out the answers on the device. Your questions and network data aren't sent to DashyNMS or to an AI service. The app still contacts your own LibreNMS, Graylog and Unimus servers to look things up.

## What you need

- iOS or iPadOS 26 or later.
- A device that supports Apple Intelligence.
- Apple Intelligence turned on in the Settings app, under **Apple Intelligence & Siri**, and its model downloaded.

Intelligence isn't available on Android, or on older iPhones and iPads.

## Turn it on

1. Open **Settings → Intelligence**. If it can't run here, the page says why.
2. Choose **Turn on Intelligence** and read the notice.
3. Choose **Turn on**, then open **More → Intelligence**. You can pin it to the tab bar.

## Ask

1. Type a question, or tap one of the starter questions.
2. Watch what's being looked up. Stop the answer at any time.
3. Tap a device, alert or other item in the answer to open it.

Graphs fill the width of the answer card. Tap a graph to open a larger preview.

On narrow screens, tables with clear column headings show each row as labelled fields. If a table asks you to scroll sideways, swipe it to see the other columns. **Jump to latest** appears beside the question field when you scroll back through a chat.

Be specific: name the device, site or port. The on-device model holds less at once than the ones on a computer, so it looks up fewer things for each question. Ask about one part of the network at a time, and start a **New chat** when you change subject.

Answers can be wrong, and lookups can use data that's a few minutes old. Check LibreNMS before acting. When something couldn't be read, the answer says so.

## Keep a chat to part of the network

Use Spotlight to keep the chat to some devices, a group or a location. Choose **Add to Spotlight** on an item in an answer to add that too. You can also start a chat from a device, alert, sensor or port: swipe its row, or press and hold it, and choose **Ask Intelligence**.

## Choose how much detail you get

Choose **Brief**, **Balanced** or **Detailed** for the next answer, or set **Default detail** in **Settings → Intelligence**.

**Colour status indicators** shows states in colour in answers. **Configuration text** decides what Unimus lookups share with the model on this device: **Summary** for dates and change counts, **Full redacted text** for the configuration with secrets removed, or **Off**.

## Saved chats

Chats are saved, encrypted, on this phone for the server you're signed in to. Rename, pin or delete one from **Chats**. Turn on **Single-chat mode** to use one chat without saving it. **Delete saved chats** removes them all.

**Export conversation** shares a chat as Markdown or PDF. Check it first, because it names your devices.

## Report an answer

Press and hold an answer and choose **Report this answer**. Remove names and addresses from the preview, then open it in your email app. Nothing is sent until you send that email.

See [Privacy and your data](../reference/your-data.md#intelligence).
