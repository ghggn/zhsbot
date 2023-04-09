using TL;
using zhsbot.Handlers;
using zhsbot.Helper;

namespace zhsbot;

public class Program
{
  public static async Task Main(string[] args)
  {
    Wraper.GetInstance().AddHandler(new InsertNewMsgOrSearch());

    await Wraper.GetInstance().Start();

    Task.Run(async () =>
    {
      using DatabaseCURD curd = new DatabaseCURD(Wraper.MainSettings,"fill_history_or_Gap");
      Helpers.WriteLine("Fill history loop started !", ConsoleColor.Yellow);
      foreach (var id in Wraper.MainSettings.Channel_IDs)
      {
        try
        {
          ChatBase peer;
          if (!Wraper.GetInstance().Dialogs.chats.TryGetValue(id, out peer))
          {
            Console.WriteLine($"you not a member of channel --> {id}");
            continue;
          }
          if (!curd.IsHistoryFilled(peer))
          {
            await curd.StartFillHistoryLoop(peer);
          }
          else
          {
            Helpers.WriteLine($"Channel history is filled. channel_id --> {id}", ConsoleColor.Green);
          }
          Helpers.WriteLine($"Now we check is there a gap in msgs. channel_id --> {id}", ConsoleColor.Green);
          int topMsgId = Wraper.GetInstance().GetTopMsgIdOfAnChannel(peer.ID);
          int maxMsgIdInDatabase = curd.GetMsgIDMinOrMax(peer.ID, true);
          if (maxMsgIdInDatabase == topMsgId)
          {
            Helpers.WriteLine($"DataBase is newest, unnesscery to Fill Gap. channel_id --> {id}", ConsoleColor.Green);
            continue;
          }
          await curd.StartFillHistoryLoop(peer, maxMsgIdInDatabase);

          Helpers.WriteLine("All messge fill task processed !");
        }
        catch (Exception e)
        {
          Helpers.WriteLine($"get an error when loop the history, channel_id --> {id} , Error : {e}", ConsoleColor.Red);
        }
      }
    });

    Wraper.GetInstance()._manualResetEvent.WaitOne();
  }
}