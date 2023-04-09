using System.Net;

namespace zhsbot;

public class NetworkChecker
{
  private HttpClient _client = new HttpClient();
  private string _tg_host = "https://www.google.com";
  private int _max_interval = 10;

  private async ValueTask<bool> check()
  {
    try
    {
      using HttpResponseMessage responseMessage = await _client.GetAsync(_tg_host);
      if (responseMessage.StatusCode == HttpStatusCode.OK)
      {
        return true;
      }
    }
    catch (Exception e)
    {
      //Console.WriteLine(e);
      return false;
    }

    return false;
  }

  public async ValueTask<bool> GetNetworkStatus()
  {
    var temp_int = 0;
    while (_max_interval > temp_int)
    {
      if (await check())
      {
        return true;
      }
      temp_int++;
    }
    return false;
  }
}