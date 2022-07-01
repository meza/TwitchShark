using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

class Twitch
{
    private readonly String Username;
    private readonly String Token;
    readonly string Ip = "irc.chat.twitch.tv";
    readonly int Port = 6667;
    private StreamReader streamReader;
    private StreamWriter streamWriter;
    private TaskCompletionSource<int> connected = new TaskCompletionSource<int>();
    public event TwitchChatEventHandler OnMessage = delegate { };
    public delegate void TwitchChatEventHandler(object sender, TwitchChatMessage e);

    public class TwitchChatMessage : EventArgs
    {
        public string Sender { get; set; }
        public string Message { get; set; }
        public string Channel { get; set; }
        public bool IsSub { get; set; }
        public bool IsMod { get; set; }
    }

    public Twitch(String username, String token)
    {
        this.Username = username;
        this.Token = token;
    }

    public async Task Start(CancellationTokenSource cts)
    {
        Debug.Log("Connecting to twitch");
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(this.Ip, this.Port);

        streamReader = new StreamReader(tcpClient.GetStream());
        streamWriter = new StreamWriter(tcpClient.GetStream()) { NewLine = "\r\n", AutoFlush = true };

        await streamWriter.WriteLineAsync($"PASS {this.Token}");
        await streamWriter.WriteLineAsync($"NICK {this.Username}");

        connected.SetResult(0);
        Debug.Log("Connected to twitch");

        while (true)
        {
            if (cts.IsCancellationRequested)
            {
                Debug.Log("Stopping Twitch Thread");
                break;
            }
            string line = await streamReader.ReadLineAsync();
            string[] split = line.Split(' ');
            if (line.StartsWith("PING"))
            {
                Console.WriteLine("PING");
                await streamWriter.WriteLineAsync($"PONG {split[1]}");
            }
            if (split.Length > 3 && split[2] == "PRIVMSG")
            {
              
                var parts = split[0].Split(';');
                var msg = new TwitchChatMessage();
                Array.ForEach(parts, (part) =>
                {
                    var subparts = part.Split('=');
                    if (subparts[0] == "display-name")
                    {
                        msg.Sender = subparts[1];
                    }

                    if (subparts[0] == "subscriber")
                    {
                        msg.IsSub = subparts[1] == "1";
                    }

                    if (subparts[0] == "mod")
                    {
                        msg.IsMod = subparts[1] == "1";
                    }
                });

                int secondColonPosition = split[4].IndexOf(':');//the 1 here is what skips the first character
                msg.Message = split[4].Substring(secondColonPosition + 1);//Everything past the second colon
                msg.Channel = split[3].TrimStart('#');
                OnMessage(this, msg);
            }
        }

    }
    public async Task SendMessage(string channel, string message)
    {
        await connected.Task;
        await streamWriter.WriteLineAsync($"PRIVMSG #{channel} :{message}");
    }

    public async Task JoinChannel(string channel)
    {
        Debug.Log($"Will join #{channel} when the connection is ready");
        await connected.Task;
        Debug.Log("Requesting capabilities");
        await streamWriter.WriteLineAsync("CAP REQ :twitch.tv/commands twitch.tv/tags");
        Debug.Log("Capabilities Sent");
        await streamWriter.WriteLineAsync($"JOIN #{channel}");
        Debug.Log($"Joined #{channel}");

    }
}
