using DesktopNMS.Services;
var chat = RecordedChat.Create(File.ReadAllBytes(args[0]));
using var output = File.Create(args[1]);
new IntelligenceChatPdfWriter().Write(chat, output);
