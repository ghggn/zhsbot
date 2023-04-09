namespace zhsbot.Abstracts;

public abstract class ACommandHandler : ABaseHandler
{
  public string CmdName;
  public string Description;
  public string Usage;
  
  public ACommandHandler(string cmdName, string description, string usage)
  {
    this.CmdName = cmdName;
    this.Description = description;
    this.Usage = usage;
  }
}