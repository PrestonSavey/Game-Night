using System.Security.Cryptography;

namespace GameNight.Application.Lobbies;

public interface ILobbyCodeGenerator
{
    LobbyCode Next();
}

public sealed class LobbyCodeGenerator : ILobbyCodeGenerator
{
    public LobbyCode Next()
    {
        var chars = new char[LobbyCode.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = LobbyCode.Alphabet[RandomNumberGenerator.GetInt32(LobbyCode.Alphabet.Length)];
        }
        return new LobbyCode(new string(chars));
    }
}
