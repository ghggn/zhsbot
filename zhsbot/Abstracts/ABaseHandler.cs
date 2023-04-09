using TL;

namespace zhsbot.Abstracts;

public abstract class ABaseHandler
{
  public abstract ValueTask HandleUpdate(Message message);
}