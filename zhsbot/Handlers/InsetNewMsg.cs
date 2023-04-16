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

      long uid = -1;
      if ((message.flags & Message.Flags.has_reply_to) != 0)
      {
        MsgBean bean = Wraper.GetInstance().Crud.GetMsg(message.reply_to.reply_to_msg_id);
        uid = bean.user_id;
      }

      var sstr = "+" + string.Join(" +", cmds);
      long res_count = 0;
      var msg_beans = Wraper.GetInstance().Crud.FulltextSearch(sstr, out res_count, user_id: uid);
      if (res_count == 0)
      {
        return;
      }
      await SendSearchResult(msg_beans!, sstr, Wraper.GetInstance().Chats[message.peer_id.ID], res_count, uid != -1);
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

    Wraper.GetInstance().Crud.InsertMsg(msgBean);
  }

  private async ValueTask SendSearchResult(List<(MsgBean, UserBean)> bean_tuples, string s, InputPeer send_to_peer, long count, bool is_specific_user)
  {
    string searchStr = s.Replace("+", "");
    string temp = "";
    if (!is_specific_user)
    {
      temp = $"搜索 <b>{searchStr}</b> 结果如下:\n";
      foreach (var value in bean_tuples)
      {
        string s1 = value.Item1.content.Length > 25 ? value.Item1.content[..25] + "..." : value.Item1.content;
        string user_name = $"{value.Item2.first_name} {value.Item2.last_name}";
        temp += $"<b>{HtmlText.Escape(user_name)}</b> : <a href=\"https://t.me/c/{value.Item1.channel_id}/{value.Item1.msg_id}\">{HtmlText.Escape(s1)}</a>\n";
      }
    }
    else
    {
      temp = $"对 <b>{bean_tuples[0].Item2.first_name} {bean_tuples[0].Item2.last_name}</b> 搜索 <b>{searchStr}</b> 结果如下:\n";
      foreach (var value in bean_tuples)
      {
        string s1 = value.Item1.content.Length > 25 ? value.Item1.content[..25] + "..." : value.Item1.content;
        temp += $": <a href=\"https://t.me/c/{value.Item1.channel_id}/{value.Item1.msg_id}\">{HtmlText.Escape(s1)}</a>\n";
      }
    }
    if (count > 10)
    {
      temp += $"有{count - 10}条结果未展开,请用更多有效关键词精确检索";
    }

    var entities = Wraper.GetInstance().TelegramClient.HtmlToEntities(ref temp);
    await Wraper.GetInstance().TelegramClient.SendMessageAsync(send_to_peer, temp, entities: entities);
  }
}