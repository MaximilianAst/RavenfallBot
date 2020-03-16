using System.Globalization;
using System.Reflection.Metadata;

namespace TwitchChatBot
{
  public class TwitchUser
  {
    
    public string Username { get; set; } // Only needed for oauth (not if token generator was used)
    public string UserToken { get; set; } // Only needed for oauth (not if token generator was used)
    public string ClientId { get; set; }
    public string Secret { get; set; } // Token generator or with oauth if implemented properlys
  }
}
