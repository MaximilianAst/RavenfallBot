using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchChatBot
{
  class Program
  {
    static void Main(string[] args)
    {
      var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
      var twitchUserSettings = cfg.GetSection("TwitchUser").Get<TwitchUser>();


      var api = new TwitchAPI();
      api.Settings.Secret = twitchUserSettings.Secret;
      api.Settings.ClientId = twitchUserSettings.ClientId;
      // twitchUserSettings.UserToken = api.V5.Auth.GetAccessToken(); // not working for now
      Console.WriteLine("Hello World!");
      Bot bot = new Bot(twitchUserSettings);
      Console.ReadLine();
    }
  }

  class Bot
  {
    private readonly TwitchUser _twitchUserSettings;
    TwitchClient client;

    public Bot(TwitchUser twitchUserSettings)
    {
      _twitchUserSettings = twitchUserSettings;
      ConnectionCredentials credentials =
        new ConnectionCredentials(twitchUserSettings.Username, twitchUserSettings.UserToken);
      var clientOptions = new ClientOptions
      {
        MessagesAllowedInPeriod = 100,
        ThrottlingPeriod = TimeSpan.FromSeconds(1),
      };
      WebSocketClient customClient = new WebSocketClient(clientOptions);
      client = new TwitchClient(customClient);
      client.Initialize(credentials, "mrbalrog");

      client.OnLog += Client_OnLog;
      client.OnJoinedChannel += Client_OnJoinedChannel;
      client.OnMessageReceived += Client_OnMessageReceived;
      client.OnWhisperReceived += Client_OnWhisperReceived;
      client.OnNewSubscriber += Client_OnNewSubscriber;
      client.OnConnected += Client_OnConnected;

      client.Connect();
    }

    private void Client_OnLog(object sender, OnLogArgs e)
    {
      //Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
    }

    private void Client_OnConnected(object sender, OnConnectedArgs e)
    {
      Console.WriteLine($"Connected to {e.AutoJoinChannel}");
    }

    private async void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
    {
      if (false /* enable autojoin */)
      {
        client.SendMessage(e.Channel, "!join");
        await Task.Delay(1000);
        client.SendMessage(e.Channel, "!train crafting");
      }
      else
      {
        client.SendMessage(e.Channel, "!stats crafting");
      }
    }

    private const RegexOptions REGEX_OPTIONS =
      RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private DateTime dungeonTimeout = DateTime.UtcNow;
    private const string DUNGEON_PATTERN = @".*\s+available\.\s+Type\s+!dungeon\s+.*\s+join.*";
    private DateTime raidTimeout = DateTime.UtcNow;
    private const string RAID_PATTERN =
      @"A\s+level\s+\d+\s+raid\s+boss\s+has\s+appeared!\s+Help\s+fight\s+him\s+by\s+typing\s+!raid";
    private DateTime joinTimeout = DateTime.UtcNow;
    private const string DISCONNECT_PATTERN = @".*not\s+.*playing.*!join.*";

    private async void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
      if (!(e.ChatMessage.IsBroadcaster || e.ChatMessage.IsModerator))
      {
        if (e?.ChatMessage?.Username?.Equals(_twitchUserSettings.Username, StringComparison.InvariantCultureIgnoreCase) ?? false)
        {
          Console.WriteLine("ME: " + e?.ChatMessage?.Message);
        }
        
        return;
      }

      try // DUNGEON
      {
        var dungeonMatch = Regex.Match(e?.ChatMessage?.Message ?? string.Empty, DUNGEON_PATTERN, REGEX_OPTIONS);
        if (dungeonMatch.Success && DateTime.UtcNow >= dungeonTimeout)
        {
          dungeonTimeout = DateTime.UtcNow.AddMinutes(3);
          Console.WriteLine("DUNGEON: " + e?.ChatMessage?.Message);
          client.SendMessage(e.ChatMessage.Channel, "!dungeon");
          return;
        }
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception);
      }

      try // RAID
      {
        var raidMatch = Regex.Match(e?.ChatMessage?.Message ?? string.Empty, RAID_PATTERN, REGEX_OPTIONS);
        if (raidMatch.Success && DateTime.UtcNow >= raidTimeout)
        {
          raidTimeout = DateTime.UtcNow.AddMinutes(2);
          Console.WriteLine("RAID: " + e?.ChatMessage?.Message);
          client.SendMessage(e.ChatMessage.Channel, "!raid");
          return;
        }
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception);
      }

      try // DISCONNECTED
      {
        var dcMatch = Regex.Match(e?.ChatMessage?.Message ?? string.Empty, string.Concat(_twitchUserSettings.Username, DISCONNECT_PATTERN), REGEX_OPTIONS);
        if (dcMatch.Success && DateTime.UtcNow >= raidTimeout)
        {
          joinTimeout = DateTime.UtcNow.AddMinutes(10);
          Console.WriteLine("DC: " + e?.ChatMessage?.Message);

          await Task.Delay(1000);
          client.SendMessage(e.ChatMessage.Channel, "!join");
          await Task.Delay(1000);
          client.SendMessage(e.ChatMessage.Channel, "!train crafting");
          return;
        }
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception);
      }

      try // MyMessages
      {
        if (e?.ChatMessage?.Message?.Contains(_twitchUserSettings.Username, StringComparison.InvariantCultureIgnoreCase) ?? false)
        {
          Console.WriteLine("MSG: " + e?.ChatMessage?.Message);
        }
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception);
      }
    }

    private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
    {
      // if (e.WhisperMessage.Username == "my_friend")
      //   client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
    }

    private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
    {
      // if (e.Subscriber.SubscriptionPlan == SubscriptionPlan.Prime)
      //   client.SendMessage(e.Channel,
      //     $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points! So kind of you to use your Twitch Prime on this channel!"
      //   );
      // else
      //   client.SendMessage(e.Channel,
      //     $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points!");
    }
  }
}
