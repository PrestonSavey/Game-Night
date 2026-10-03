using GameNight.Application.Games;
using GameNight.Application.Lobbies;
using GameNight.Application.Security;
using GameNight.Application.Sessions;
using GameNight.Application.Words;
using GameNight.Domain.Abstractions;
using GameNight.Domain.Games.Bluff;
using GameNight.Domain.Games.Impostor;
using GameNight.Domain.Games.LastWord;
using GameNight.Domain.Games.Spectrum;
using Microsoft.Extensions.DependencyInjection;

namespace GameNight.Application;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the runtime. Note the ISessionBroadcaster is NOT registered here -
    /// that is the transport's job, and leaving it out is what keeps this layer testable.
    /// </summary>
    public static IServiceCollection AddGameNight(this IServiceCollection services)
    {
        // Loaded once and shared. Building it takes a moment, so never make it transient.
        services.AddSingleton<IWordList, EmbeddedWordList>();

        // Every game you write gets one line here and nothing else changes.
        services.AddSingleton<IGameMode, SpectrumGameMode>();
        services.AddSingleton<IGameMode, LastWordGameMode>();
        services.AddSingleton<IGameMode, ImpostorGameMode>();
        services.AddSingleton<IGameMode, BluffGameMode>();

        services.AddSingleton<IGameModeRegistry, GameModeRegistry>();
        services.AddSingleton<ILobbyCodeGenerator, LobbyCodeGenerator>();
        services.AddSingleton<IRejoinTokenService>(_ => new RejoinTokenService());
        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        services.AddSingleton<ISessionRuntime, SessionRuntime>();

        return services;
    }
}
