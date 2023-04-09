namespace zhsbot;

public record struct MsgBean
{
  public int msg_id;
  public long channel_id;
  public long user_id;
  public DateTime send_time;
  public string content;
}

public record struct UserBean
{
  public long user_id;
  public string first_name;
  public string last_name;
  public long access_hash;
  public bool is_bot;
}

public record struct ChannelBean
{
  public long channel_id;
  public string title;
  public bool is_fill_history;
  public long access_hash;
}