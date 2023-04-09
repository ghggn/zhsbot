using System.Data;
using MySql.Data.MySqlClient;
using TL;
using zhsbot.Helper;

namespace zhsbot;

public class DatabaseCURD : IDisposable
{
  private readonly string _ownnerMark;
  private MySqlConnection? _mySqlConnection;

  private readonly string _host;
  private readonly string _user;
  private readonly int _port;
  private readonly string _password;
  private readonly string _database;

  public DatabaseCURD(Settings settings, string ownner_mark)
  {
    _host = settings.Database_Host;
    _user = settings.Database_User;
    _port = settings.Database_Port;
    _database = settings.Database_Database;
    _password = settings.Database_Password;
    _ownnerMark = ownner_mark;
  }

  public MySqlConnection GetSqlConnection()
  {
    if (_mySqlConnection is null)
    {
      _mySqlConnection = new MySqlConnection($"server={_host};user={_user};database={_database};port={_port};password={_password}");
      _mySqlConnection.InfoMessage += (_, args) =>
      {
        foreach (var mySqlError in args.errors)
        {
          Console.WriteLine(mySqlError.Message);
        }
      };
      _mySqlConnection.StateChange += (_, args) => { Console.WriteLine($"{_ownnerMark} : mysqlconnection state switched [{args.OriginalState} --> {args.CurrentState}]"); };
      OpenMysqlConnection(_mySqlConnection);
    }
    if (_mySqlConnection.State != ConnectionState.Open)
    {
      OpenMysqlConnection(_mySqlConnection);
    }
    return _mySqlConnection;
  }

  private void OpenMysqlConnection(MySqlConnection connection)
  {
    try
    {
      connection.Open();
    }
    catch (Exception)
    {
      Helpers.WriteLine("Database connect failed, Application can not run without database.", ConsoleColor.Red);
      throw;
    }
  }

  public bool IsHistoryFilled(InputPeer peer)
  {
    if (GetChannel(peer.ID).is_fill_history)
    {
      Helpers.WriteLine("The message history is already filled", ConsoleColor.Green);
      return true;
    }
    return false;
  }

  public async ValueTask<bool> StartFillHistoryLoop(InputPeer peer, int min_msg_id = 0)
  {
    Helpers.WriteLine($"Start fill {(min_msg_id == 0 ? "history" : "gap")} in channel {peer.ID}", ConsoleColor.Red);
    await foreach (var msgs in LoopHistory(peer, min_msg_id))
    {
      InsertMsg(msgs.ToArray());
    }
    Helpers.WriteLine($"{(min_msg_id == 0 ? "history" : "gap")} has filled in channel {peer.ID}", ConsoleColor.Red);
    return true;
  }

  public void MarkChannelHistoryIsFilled(long channel_id)
  {
    string sql_channel = $"update channel set is_fill_history = 1 where channel_id=@channel_id";
    using MySqlCommand command1 = new MySqlCommand(sql_channel, GetSqlConnection());
    command1.Parameters.AddWithValue("@channel_id", channel_id);
    command1.ExecuteNonQuery();
  }

  public ChannelBean GetChannel(long channel_id)
  {
    ChannelBean channelBean = new ChannelBean();
    string sql_channel = $"select * from channel where channel_id=@cid";
    using MySqlCommand command1 = new MySqlCommand(sql_channel, GetSqlConnection());
    command1.Parameters.AddWithValue("@cid", channel_id);
    using var mySqlDataReader = command1.ExecuteReader();
    if (mySqlDataReader.Read())
    {
      channelBean.title = (string)mySqlDataReader["title"];
      channelBean.channel_id = channel_id;
      channelBean.access_hash = (long)mySqlDataReader["access_hash"];
      channelBean.is_fill_history = (bool)mySqlDataReader["is_fill_history"];
    }
    else
    {
      throw new ApplicationException($"imposeable exception , it can not be HAPPEN! {channel_id}");
    }
    return channelBean;
  }

  public UserBean GetUser(long user_id)
  {
    UserBean userBean = new();
    string sql_user = $"select * from user where user_id=@uid";
    using MySqlCommand command1 = new MySqlCommand(sql_user, _mySqlConnection);
    command1.Parameters.AddWithValue("@uid", user_id);
    using var mySqlDataReader = command1.ExecuteReader();
    if (mySqlDataReader.Read())
    {
      userBean.last_name = (string)mySqlDataReader["last_name"];
      userBean.first_name = (string)mySqlDataReader["first_name"];
      userBean.user_id = (long)mySqlDataReader["user_id"];
      userBean.is_bot = (bool)mySqlDataReader["is_bot"];
      userBean.access_hash = (long)mySqlDataReader["access_hash"];
    }
    else
    {
      throw new ApplicationException($"imposeable exception , it can not be HAPPEN! {user_id}");
    }
    return userBean;
  }

  public int GetMsgIDMinOrMax(long channel_id, bool max = false)
  {
    int res = 0;
    string sql_msg = $"select msg_id from msg where channel_id=@channel_id order by msg_id  limit 1";
    if (max)
    {
      sql_msg = $"select msg_id from msg where channel_id=@channel_id order by msg_id desc limit 1";
    }
    using MySqlCommand command = new MySqlCommand(sql_msg, GetSqlConnection());
    command.Parameters.AddWithValue("@channel_id", channel_id);
    using var mySqlDataReader = command.ExecuteReader();
    if (mySqlDataReader.Read())
    {
      res = (int)mySqlDataReader["msg_id"];
    }
    return res;
  }

  private async IAsyncEnumerable<List<MsgBean>> LoopHistory(InputPeer peer, int min_msg_id = 0)
  {
    int limit = 100;
    int offsetId = 0;
    if (min_msg_id == 0)
    {
      offsetId = GetMsgIDMinOrMax(peer.ID);
    }

    List<MsgBean> resMsgs = new();
    while (true)
    {
      var resList = (Messages_ChannelMessages)await Wraper.GetInstance().TelegramClient.Messages_Search(peer, q: null, limit: limit, offset_id: offsetId, min_id: min_msg_id);
      CollectPeerEntity(resList.users, resList.chats);
      resMsgs.Clear();

      foreach (var msgBase in resList.Messages)
      {
        if (msgBase is Message { message: not null, from_id: PeerUser } msg)
        {
          if (offsetId > msg.id || offsetId == 0)
          {
            offsetId = msg.id;
          }

          if (string.IsNullOrWhiteSpace(msg.message))
          {
            continue;
          }
          if ((msg.flags & Message.Flags.mentioned) != 0 || (msg.flags & Message.Flags.out_) != 0)
          {
            continue;
          }
          MsgBean bean = new()
          {
            content = msg.message,
            send_time = msg.date,
            msg_id = msg.id
          };

          if (resList.users[msg.from_id.ID].IsBot)
          {
            continue;
          }
          bean.user_id = msg.from_id.ID;
          bean.channel_id = msg.peer_id.ID;

          resMsgs.Add(bean);
        }
      }

      yield return resMsgs;

      if (resList.Messages.Length < limit)
      {
        if (min_msg_id != 0)
        {
          MarkChannelHistoryIsFilled(peer.ID);
        }
        break;
      }
    }
  }

  public void InsertMsg(params MsgBean[] msgs)
  {
    string sql_channel = $"insert into msg(msg_id, user_id, channel_id, send_time, content) values (@msg_id,@user_id, @channel_id, @send_time, @content) ON DUPLICATE KEY UPDATE msg_id=@msg_id,user_id=@user_id,channel_id=@channel_id,send_time=@send_time,content=@content";
    using MySqlCommand channel_command = new MySqlCommand(sql_channel, GetSqlConnection());
    foreach (var message in msgs)
    {
      channel_command.Parameters.AddWithValue("@msg_id", message.msg_id);
      channel_command.Parameters.AddWithValue("@user_id", message.user_id);
      channel_command.Parameters.AddWithValue("@channel_id", message.channel_id);
      channel_command.Parameters.AddWithValue("@send_time", message.send_time);
      channel_command.Parameters.AddWithValue("@content", message.content);
      channel_command.ExecuteNonQuery();
      channel_command.Parameters.Clear();
      Helpers.WriteLine($"insert data row --> {message.ToString()}", ConsoleColor.Green);
    }
  }

  public List<(MsgBean, UserBean)>? FulltextSearch(string s)
  {
    string sql_search = $"select a.content, a.msg_id, a.channel_id, u.first_name, u.last_name from (select * from msg where match(content) against(@s in boolean mode)) as a inner join user u on a.user_id = u.user_id order by a.msg_id desc limit 10";
    using MySqlCommand command = new MySqlCommand(sql_search, GetSqlConnection());
    command.Parameters.AddWithValue("@s", s);
    using var reader = command.ExecuteReader();
    List<(MsgBean, UserBean)> beans = new();
    while (reader.Read())
    {
      beans.Add((new MsgBean()
      {
        content = (string)reader["content"],
        msg_id = (int)reader["msg_id"],
        channel_id = (long)reader["channel_id"]
      }, new UserBean()
      {
        first_name = (string)reader["first_name"],
        last_name = (string)reader["last_name"]
      }));
    }

    if (beans.Count == 0)
    {
      Console.WriteLine("没有搜索结果");
      return null;
    }
    return beans;
  }

  public void CollectPeerEntity(Dictionary<long, User> users, Dictionary<long, ChatBase> chatBases)
  {
    string sql_channel = $"insert into channel (channel_id, title,is_fill_history) values (@ci,@t,@ifh) ON DUPLICATE KEY UPDATE title=@t,access_hash=@ah";
    string sql_user = $"insert into user (user_id, last_name, first_name,is_bot,access_hash) values (@ui,@ln,@fn,@ib,@ah) ON DUPLICATE KEY UPDATE last_name=@ln,first_name=@fn,access_hash=@ah";
    using MySqlCommand user_command = new MySqlCommand(sql_user, GetSqlConnection());
    foreach (var (_, value) in users)
    {
      user_command.Parameters.AddWithValue("@ui", value.id);
      user_command.Parameters.AddWithValue("@ln", value.last_name + "");
      user_command.Parameters.AddWithValue("@fn", value.first_name + "");
      user_command.Parameters.AddWithValue("@ib", value.IsBot);
      user_command.Parameters.AddWithValue("@ah", value.access_hash);
      user_command.ExecuteNonQuery();
      user_command.Parameters.Clear();
    }

    using MySqlCommand channel_command = new MySqlCommand(sql_channel, GetSqlConnection());
    foreach (var (_, value) in chatBases)
    {
      if (value is Channel c && !(c.flags.HasFlag(Channel.Flags.broadcast)))
      {
        channel_command.Parameters.AddWithValue("@ci", value.ID);
        channel_command.Parameters.AddWithValue("@t", value.Title);
        channel_command.Parameters.AddWithValue("@ifh", false);
        channel_command.Parameters.AddWithValue("@ah", c.access_hash);
        channel_command.ExecuteNonQuery();
        channel_command.Parameters.Clear();
      }
    }
  }

  public void Dispose()
  {
    _mySqlConnection?.Dispose();
  }
}