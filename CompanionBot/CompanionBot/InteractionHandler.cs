using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CompanionBot
{
    internal class InteractionHandler
    {
        private readonly InteractionService _service;
        private readonly IServiceProvider _services;
        private readonly Logger _logger;
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;

        public InteractionHandler(IServiceProvider services)
        {
            _services = services;
            _client = services.GetRequiredService<DiscordSocketClient>();
            _service = services.GetRequiredService<InteractionService>();
            _logger = services.GetRequiredService<Logger>();
            _config = services.GetRequiredService<IConfiguration>();
        }

        public async Task InstallCommandsAsync()
        {
            _service.SlashCommandExecuted += CommandExecuted;
            _service.ContextCommandExecuted += CommandExecuted;
            _service.ComponentCommandExecuted += CommandExecuted;
            _service.AutocompleteCommandExecuted += CommandExecuted;
            _service.Log += _logger.Log;

            // Here we discover all of the interaction modules in the
            // entry assembly and load them.
            // A service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide (https://docs.stillu.cc/guides/commands/dependency-injection.html) for more information.
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.InteractionCreated += HandleInteraction;
        }

        private async Task HandleInteraction(SocketInteraction arg)
        {
            await _logger.Log(new LogMessage(LogSeverity.Debug, nameof(HandleInteraction), "HandleInteraction Called."));
            try
            {
                // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
                var ctx = new SocketInteractionContext(_client, arg);
                await _service.ExecuteCommandAsync(ctx, _services);
            }
            catch (Exception ex)
            {
                await _logger.Log(new LogMessage(LogSeverity.Error, nameof(HandleInteraction), "Error executing Interaction Handler", ex));

                // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
                // response, or at least let the user know that something went wrong during the command execution.
                if (arg.Type == InteractionType.ApplicationCommand)
                    await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }

        public async Task RegisterSlashCommandsForTestGuild()
        {
            ulong id = _config.GetValue<ulong>("testServerId", 0);
            if (id > 0)
                await _service.RegisterCommandsToGuildAsync(id);
            else
                await _logger.Log(new LogMessage(LogSeverity.Warning, nameof(RegisterSlashCommandsForTestGuild), "No test server ID specified in config.json, skipped registering commands!"));
        }

        public async Task RegisterSlashCommandsGlobally()
        {
            await _service.RegisterCommandsGloballyAsync();
        }

        private async Task CommandExecuted(ICommandInfo info, IInteractionContext bla, IResult result)
        {
            await _logger.Log(new LogMessage(LogSeverity.Debug, nameof(CommandExecuted), "CommandExecuted Handler called."));
            SocketInteractionContext<SocketInteraction> context = bla as SocketInteractionContext<SocketInteraction>;
            Func<string, Embed[], bool, bool, AllowedMentions, MessageComponent, Embed, RequestOptions, Task> respond;
            if (context.Interaction.HasResponded)
                respond = context.Interaction.FollowupAsync;
            else
                respond = context.Interaction.RespondAsync;
            // if an Error occurred, send the Reason for the Error as response to the interaction, or as follow-up message.
            if (result.Error.HasValue)
            {
                switch (result.Error.Value)
                {
                    case InteractionCommandError.UnknownCommand:
                        await _logger.Log(new LogMessage(LogSeverity.Error, info.Name, $"Unknown interaction {info.Name}!"));
                        await respond("⚠Unknown interaction.⚠\nPlease contact the bot owner by opening a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing what you were trying to do when getting this error.", null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.ConvertFailed:
                        await _logger.Log(new LogMessage(LogSeverity.Warning, info.Name, $"Parameter(s) could not be converted: {result.ErrorReason}"));
                        await respond($"⚠One or more parameters could not be converted.⚠\nError message:\n```{result.ErrorReason}```\nPlease check your parameters.\nIf they seem correct, open up a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing what you did, which parameters you entered and what the error message was.", null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.BadArgs:
                        int minCount = 0;
                        int maxCount = 0;
                        info.Parameters.ToList().ForEach(pi => {
                            if (pi.IsRequired)
                                minCount++;
                            maxCount++;
                        });
                        await _logger.Log(new LogMessage(LogSeverity.Error, info.Name, $"Too many or few parameters (expected {minCount} to {maxCount}): {result.ErrorReason}"));
                        await respond($"⚠Too many or few parameters.⚠\nError message:\n```{result.ErrorReason}```\nPlease contact the bot owner by opening a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing what you did and which parameters you specified.", null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.Exception:
                        await _logger.Log(new LogMessage(LogSeverity.Critical, info.Name, $"Exception while executing interaction.\nGuild: {context.Guild.Name} ({context.Guild.Id})\nUser: {context.User.Username} ({context.User.Id})\nMessage: {result.ErrorReason}"));
                        await respond($"⚠Internal Error.⚠\nThe bot has encountered a problem while executing this interaction. If the error persists, please open a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing as much information as possible.\nThis Error message will (hopefully😅) help the developer investigate:\n```{result.ErrorReason}```", null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        await _logger.Log(new LogMessage(LogSeverity.Warning, info.Name, $"Interaction execution unsuccessful: {result.ErrorReason}"));
                        await respond($"❌This interaction failed.❌\nReason: {result.ErrorReason}", null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.UnmetPrecondition:
                        await _logger.Log(new LogMessage(LogSeverity.Info, info.Name, $"User {context.User.Username} did not meet all preconditions: {result.ErrorReason}"));
                        await respond($"❌This interaction is not allowed in this context.❌\n{result.ErrorReason}", null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.ParseFailed:
                        await _logger.Log(new LogMessage(LogSeverity.Error, info.Name, $"Parsing of Command Context failed: {result.ErrorReason}"));
                        await respond($"⚠Internal Error.⚠\nThe bot has encountered a problem while executing this interaction. If the error persists, please open a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing as much information as possible.\nThis Error message will (hopefully😅) help the developer investigate:\n```{result.ErrorReason}```", null, false, true, null, null, null, null);
                        break;
                    default:
                        await _logger.Log(new LogMessage(LogSeverity.Critical, info.Name, $"Unknown Error Type encountered: {result.Error.Value} - {result.ErrorReason}"));
                        await respond($"⚠Internal Error.⚠\nThe bot has encountered a problem while executing this interaction. Please open a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing as much information as possible.\nThis Error message will (hopefully😅) help the developer investigate:\n```{result.ErrorReason}```", null, false, true, null, null, null, null);
                        break;
                }
            }
            else
            {
                // Log Command Execution
                await _logger.Log(new LogMessage(LogSeverity.Info, info.Name, $"Interaction executed in guild {context.Guild.Name} ({context.Guild.Id})"));
            }
        }
    }
}
