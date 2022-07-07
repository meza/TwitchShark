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

    public class TwitchUser
    {
        public string Username { get; set; }
        public string Color { get; set; }
    }

    public class TwitchChatMessage : EventArgs
    {
        public DateTime DateTime { get; set; }
        public TwitchUser Sender { get; set; }
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
        if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_DEBUG))
        {
            Debug.Log("Sending PONG");
        }

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
            Debug.Log("Login failed");
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
            Sender = new TwitchUser { Username = command.Tags["display-name"], Color = command.Tags["color"] },
            IsMod = command.Tags["mod"] == "1" || command.Tags["badges"].Contains("broadcaster"),
            IsSub = command.Tags["subscriber"] == "1",
            Message = command.Message,
            Channel = command.Parameters.TrimStart('#'),
            DateTime = DateTime.Now
        };

        OnMessage(this, msg);
    }

    public async Task Start(CancellationTokenSource cts)
    {
        try
        {
            Debug.Log($"Connecting to twitch, thread: {cts.Token.GetHashCode()}");
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(this.Ip, this.Port);

            streamReader = new StreamReader(tcpClient.GetStream());
            streamWriter = new StreamWriter(tcpClient.GetStream()) { NewLine = "\r\n", AutoFlush = true };

            await streamWriter.WriteLineAsync($"PASS {this.Token}");
            await streamWriter.WriteLineAsync($"NICK {this.Username}");

            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();
                string line = await streamReader.ReadLineAsync();

                if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_DEBUG))
                {
                    Debug.Log($"Received message: {line}");
                }

                cts.Token.ThrowIfCancellationRequested();

                if (line == null) continue;

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
                        Debug.Log("Reconnect message received");
                        break;
                }
            }
        }

        catch (OperationCanceledException e)
        {
            Debug.Log($"Twitch disconnect requested for thread: {e.CancellationToken.GetHashCode()}");
        }
        catch (ObjectDisposedException e)
        {
            if (TwitchSharkName.ExtraSettingsAPI_GetCheckboxState(TwitchSharkName.SETTINGS_DEBUG))
            {
                Debug.Log("Already cancelled, no need to worry");
            }
        }
        catch (IOException e)
        {
            Debug.Log($"Twitch had a hiccup, reconnecting... {e.Message}");
            Start(cts);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            cts.Dispose();
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
