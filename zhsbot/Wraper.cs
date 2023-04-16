using System.Text.Json;
using TL;
using WTelegram;
using zhsbot.Abstracts;
using zhsbot.Helper;
using Helpers = zhsbot.Helper.Helpers;

namespace zhsbot;

public class Wraper
{
  private static List<ABaseHandler> SelfHandlers = new();
  private static Wraper _wraper;
  public static Settings MainSettings;

  public readonly Client TelegramClient;
  public ManualResetEvent _manualResetEvent = new ManualResetEvent(false);
  public Messages_Dialogs Dialogs;

  public readonly DatabaseCRUD Crud;
  private readonly string _settingsFileName = "setting.json";
  public readonly Dictionary<long, User> Users = new();
  public readonly Dictionary<long, ChatBase> Chats = new();

  private Wraper()
  {
    ReadSettings();
    Crud = new DatabaseCRUD(MainSettings, "wraper_for_handle_update");
#if !DEBUG
    WTelegram.Helpers.Log = (i, s) =>
    {
      if (i >= 4)
      {
        Helpers.WriteLine(s, ConsoleColor.Red);
      }
    };
#endif
    TelegramClient = new Client(GetConfig);
    TelegramClient.MaxAutoReconnects = 2;
  }

  public static Wraper GetInstance()
  {
    if (_wraper == null)
    {
      _wraper = new Wraper();
    }
    return _wraper;
  }

  private void ReadSettings()
  {
    var settingFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settingsFileName);
    if (!File.Exists(settingFilePath))
    {
      throw new FileNotFoundException("Settings file is needed!");
    }
    var opt = new JsonSerializerOptions()
    {
      IncludeFields = true
    };
    MainSettings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingFilePath), opt);
  }

  private string? GetConfig(string name)
  {
    switch (name)
    {
      case "phone_number":
        return MainSettings.PhoneNumber;
      case "api_id":
        return MainSettings.API_ID;
      case "api_hash":
        return MainSettings.API_HASH;
      case "verification_code":
        Console.Write("Verification Code: ");
        return Console.ReadLine();
      case "session_pathname":
        return MainSettings.SessionFilePath;
      case "password":
        return MainSettings.Password;
      default:
        return null;
    }
  }

  public async ValueTask Start()
  {
    await InitClient();
  }

  public async ValueTask InitClient()
  {
    NetworkChecker checker = new NetworkChecker();
    if (!await checker.GetNetworkStatus())
    {
      Helpers.WriteLine("Network unavailable ! Application is stoping ! .", ConsoleColor.Red);
      _manualResetEvent.Set();
      return;
    }

    try
    {
      await TelegramClient.LoginUserIfNeeded();
    }
    catch (Exception e)
    {
      Helpers.WriteLine($"Loggin failed ! Application is stoping !  --> {e.Message}");
      TelegramClient.Dispose();
      _manualResetEvent.Set();
      return;
    }
    Helpers.WriteLine($"Client Login Success! {DateTime.Now}, Profile Name: {MainSettings.ProfileName}", ConsoleColor.Green);
    await GetAllDialogs();
    TelegramClient.OnUpdate += HandleUpdate;
  }

  public void AddHandler(ABaseHandler handler)
  {
    SelfHandlers.Add(handler);
  }

  public async Task HandleUpdate(IObject obj)
  {
    if (obj is ReactorError reactorError)
    {
      Helpers.WriteLine($"warnning ! Application is stoping ! --> {reactorError.Exception.GetType()} {reactorError.Exception.Message}");
      TelegramClient.Dispose();
      _manualResetEvent.Set();
    }
    if (obj is not UpdatesBase updates) return;
    CollectPeersAndSaveToDataBase(updates.Users, updates.Chats);
    foreach (var update in updates.UpdateList)
    {
      Message msg = null;
      if (update is UpdateNewMessage unm)
      {
        if (unm.message is Message m1)
        {
          msg = m1;
        }
      }
      if (update is UpdateEditChannelMessage ecm)
      {
        if (ecm.message is Message m1)
        {
          msg = m1;
        }
      }
      if (msg == null)
      {
        return;
      }

      if (string.IsNullOrWhiteSpace(msg.message))
      {
        return;
      }
      if ((updates.UserOrChat(msg.peer_id)) is Channel { IsChannel: true })
      {
        return;
      }
      if ((msg.flags & Message.Flags.out_) != 0)
      {
        return;
      }
      if (msg.from_id is PeerUser && Users[msg.from_id.ID].IsBot)
      {
        return;
      }
      if (!MainSettings.Channel_IDs.Contains(msg.peer_id.ID) && !MainSettings.Debug_Channels.Contains(msg.peer_id.ID))
      {
        return;
      }
      foreach (var handler in SelfHandlers)
      {
        handler.HandleUpdate(msg);
      }
    }
  }

  public int GetTopMsgIdOfAnChannel(long dialog_peer_id)
  {
    int res = 0;
    if (Dialogs is null)
    {
      return res;
    }
    foreach (var dialogsDialog in Dialogs.Dialogs)
    {
      if (dialogsDialog.Peer.ID == dialog_peer_id)
      {
        res = dialogsDialog.TopMessage;
        return res;
      }
    }

    return res;
  }

  private void CollectPeersAndSaveToDataBase(Dictionary<long, User> users, Dictionary<long, ChatBase> chats)
  {
    var postUsers = new Dictionary<long, User>();
    var postChats = new Dictionary<long, ChatBase>();

    foreach (var chat in chats)
    {
      if (chat.Value is Chat ch) continue;
      if (chat.Value is ChannelForbidden || chat.Value is ChatForbidden) continue;
      if (chat.Value is Channel channel)
      {
        if ((channel.flags & Channel.Flags.min) != 0 && Chats.ContainsKey(channel.id))
        {
          continue;
        }
        Chats.MeAddOrUpdate(channel.id, channel);
        postChats.MeAddOrUpdate(channel.id, channel);
      }
    }
    foreach (var user in users)
    {
      if ((user.Value.flags & User.Flags.min) != 0 && Users.ContainsKey(user.Value.id))
      {
        continue;
      }
      Users.MeAddOrUpdate(user.Value.id, user.Value);
      postUsers.Add(user.Value.id, user.Value);
    }

    Crud.CollectPeerEntity(postUsers, postChats);
  }

  private async ValueTask GetAllDialogs()
  {
    Dialogs = await TelegramClient.Messages_GetAllDialogs();
    CollectPeersAndSaveToDataBase(Dialogs.users, Dialogs.chats);
  }
}