using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchChatBot
{
  public static class Program
  {
    static void Main(string[] args)
    {
      var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
      var twitchUserSettings = cfg.GetSection(nameof(TwitchUser)).Get<TwitchUser>();
      var botSettings = cfg.GetSection(nameof(BotSettings)).Get<BotSettings>();

      Console.WriteLine("Starting bot...");
      var bot = new Bot(twitchUserSettings, botSettings);
      bot.ConnectAndStart();
      Console.ReadLine();
    }
  }

  internal class Bot
  {
    private const RegexOptions REGEX_OPTIONS = RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private DateTime _dungeonTimeout = DateTime.UtcNow;
    private const string DUNGEON_PATTERN = @".*\s+available\.\s+Type\s+!dungeon\s+.*\s+join.*";
    private DateTime _raidTimeout = DateTime.UtcNow;
    private const string RAID_PATTERN = @"A\s+level\s+\d+\s+raid\s+boss\s+has\s+appeared!\s+Help\s+fight\s+him\s+by\s+typing\s+!raid";
    private const string DISCONNECT_PATTERN = @".*not\s+.*playing.*!join.*";

    private readonly TwitchUser _twitchUserSettings;
    private readonly BotSettings _botSettings;
    private readonly TwitchClient _client;

    public Bot(TwitchUser twitchUserSettings, BotSettings botSettings)
    {
      _twitchUserSettings = twitchUserSettings;
      _botSettings = botSettings;
      var credentials = new ConnectionCredentials(twitchUserSettings.Username, twitchUserSettings.UserToken);
      var clientOptions = new ClientOptions
      {
        MessagesAllowedInPeriod = 100,
        ThrottlingPeriod = TimeSpan.FromSeconds(1),
      };
      var customClient = new WebSocketClient(clientOptions);
      _client = new TwitchClient(customClient);
      _client.Initialize(credentials, _botSettings.Channel);

      _client.OnJoinedChannel += Client_OnJoinedChannel;
      _client.OnMessageReceived += Client_OnMessageReceived;
      _client.OnConnected += Client_OnConnected;
    }

    public void ConnectAndStart()
    {
      _client.Connect();
    }

    private void Client_OnConnected(object sender, OnConnectedArgs e)
    {
      Console.WriteLine($"Connected to {e.AutoJoinChannel}");
    }

    private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
    {
       _client.SendMessage(e.Channel, "!stats " + _botSettings.Training);
    }

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
        if (dungeonMatch.Success && DateTime.UtcNow >= _dungeonTimeout)
        {
          _dungeonTimeout = DateTime.UtcNow.AddMinutes(3);
          Console.WriteLine("DUNGEON: " + e?.ChatMessage?.Message);
          _client.SendMessage(e.ChatMessage.Channel, "!dungeon");
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
        if (raidMatch.Success && DateTime.UtcNow >= _raidTimeout)
        {
          _raidTimeout = DateTime.UtcNow.AddMinutes(2);
          Console.WriteLine("RAID: " + e?.ChatMessage?.Message);
          _client.SendMessage(e.ChatMessage.Channel, "!raid");
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
        if (dcMatch.Success && DateTime.UtcNow >= _raidTimeout)
        {
          Console.WriteLine("DC: " + e?.ChatMessage?.Message);

          await Task.Delay(2000);
          _client.SendMessage(e.ChatMessage.Channel, "!join");
          await Task.Delay(2000);
          _client.SendMessage(e.ChatMessage.Channel, "!train " + _botSettings.Training);
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
  }
}
