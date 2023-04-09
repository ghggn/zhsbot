using TL;
using zhsbot.Abstracts;

namespace zhsbot.Handlers;

public class InsertNewMsgOrSearch : ABaseHandler
{
  public override async ValueTask HandleUpdate(Message message)
  {
    Console.WriteLine(message.message);
    if ((message.flags & Message.Flags.mentioned) != 0)
    {
      var cmds = message.message.Trim().Split()[1..];
      if (cmds.Length < 1 || cmds.Length > 5)
      {
        return;
      }

      int l = string.Join("", cmds).Length;
      if (l <= 1 || l >= 20)
      {
        return;
      }
      var sstr = "+" + string.Join(" +", cmds);
      var msg_beans = Wraper.GetInstance().Curd.FulltextSearch(sstr);
      if (msg_beans is null)
      {
        return;
      }
      await SendSearchResult(msg_beans, sstr, Wraper.GetInstance().Chats[message.peer_id.ID]);
      return;
    }

    if (!Wraper.MainSettings.Channel_IDs.Contains(message.peer_id.ID))
    {
      return;
    }

    MsgBean msgBean = new MsgBean();
    msgBean.msg_id = message.id;
    msgBean.user_id = message.from_id.ID;
    msgBean.channel_id = message.peer_id.ID;
    msgBean.content = message.message;
    msgBean.send_time = message.date;

    Wraper.GetInstance().Curd.InsertMsg(msgBean);
  }

  public async ValueTask SendSearchResult(List<(MsgBean, UserBean)> bean_tuples, string s, InputPeer send_to_peer)
  {
    string searchStr = s.Replace("+", "");
    string temp = $"搜索 <b>{searchStr}</b> 结果如下:\n";
    foreach (var value in bean_tuples)
    {
      string s1 = value.Item1.content.Length > 25 ? value.Item1.content[..25] + "..." : value.Item1.content;
      string user_name = $"{value.Item2.first_name} {value.Item2.last_name}";
      temp += $"<b>{HtmlText.Escape(user_name)}</b> : <a href=\"https://t.me/c/{value.Item1.channel_id}/{value.Item1.msg_id}\">{HtmlText.Escape(s1)}</a>\n";
    }
    if (bean_tuples.Count == 10)
    {
      temp += "有更多结果未展开,请用更多有效关键词精确检索";
    }

    var entities = Wraper.GetInstance().TelegramClient.HtmlToEntities(ref temp);
    await Wraper.GetInstance().TelegramClient.SendMessageAsync(send_to_peer, temp, entities: entities);
  }
}