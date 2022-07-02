using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class Twitch
{
    private readonly String Username;
    private readonly String Token;
    readonly string Ip = "irc.chat.twitch.tv";
    readonly int Port = 6667;
    private TwitchParser parser = new TwitchParser();
    private StreamReader streamReader;
    private StreamWriter streamWriter;
    private TaskCompletionSource<int> connected = new TaskCompletionSource<int>();
    public event TwitchConnectionEventHandler OnConnection = delegate { };
    public event TwitchChatEventHandler OnMessage = delegate { };
    public delegate void TwitchChatEventHandler(object sender, TwitchChatMessage e);
    public delegate void TwitchConnectionEventHandler(object sender, TwitchConnection e);

    public class TwitchConnection : EventArgs
    {
        public bool Success { get; set; }
    }
    public class TwitchCommand
    {
        public string Command { get; set; }
        public string Message { get; set; }
        public string Parameters { get; set; }
        public string Hostmask { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }
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

    private async void OnPing(TwitchCommand command)
    {
        await streamWriter.WriteLineAsync($"PONG {command.Message}");
    }

    private async void OnConnectedMessage(TwitchCommand command)
    {
        connected.SetResult(0);
        Debug.Log("Connected to twitch");
        OnConnection(this, new TwitchConnection
        {
            Success = true
        });
    }

    private async void OnNotice(TwitchCommand command, CancellationTokenSource cts)
    {
        var message = command.Message.ToLower();
        if (message == "login authentication failed" || message == "improperly formatted auth")
        {
            OnConnection(this, new TwitchConnection
            {
                Success = false
            });
            cts.Cancel();
        }
    }

    private async void OnPRIVMessage(TwitchCommand command)
    {
        var msg = new TwitchChatMessage
        {
            Sender = command.Tags["display-name"],
            IsMod = command.Tags["mod"] == "1",
            IsSub = command.Tags["subscriber"] == "1",
            Message = command.Message,
            Channel = command.Parameters.TrimStart('#')
        };

        OnMessage(this, msg);
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

        while (true)
        {
            if (cts.IsCancellationRequested)
            {
                Debug.Log("Stopping Twitch Thread");
                break;
            }
            string line = await streamReader.ReadLineAsync();
            var command = parser.Parse(line);

            switch (command.Command)
            {
                case "PING":
                    OnPing(command);
                    break;
                case "001":
                    OnConnectedMessage(command);
                    break;
                case "NOTICE":
                    OnNotice(command, cts);
                    break;
                case "PRIVMSG":
                    OnPRIVMessage(command);
                    break;
                case "RECONECT":
                    break;
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
        await connected.Task;
        await streamWriter.WriteLineAsync("CAP REQ :twitch.tv/commands twitch.tv/tags");
        await streamWriter.WriteLineAsync($"JOIN #{channel}");
        Debug.Log($"Joined #{channel}");

    }
}
