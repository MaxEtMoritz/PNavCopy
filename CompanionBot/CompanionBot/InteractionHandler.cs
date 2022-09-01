using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
            // See Dependency Injection guide (https://discordnet.dev/guides/text_commands/dependency-injection.html) for more information.
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.InteractionCreated += HandleInteraction;
        }

        private async Task HandleInteraction(SocketInteraction arg)
        {
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
            await RegisterManualCommands();
        }

        public async Task RegisterSlashCommandsGlobally()
        {
            await _service.RegisterCommandsGloballyAsync();
            await RegisterManualCommands();
        }

        private async Task RegisterManualCommands()
        {
            ulong testguildId = _config.GetValue<ulong>("testServerId", 0);
            if (testguildId > 0)
            {                    
                var disconnectCmd = _service.GetSlashCommandInfo<ManualSlashModules>(nameof(ManualSlashModules.DisconnectAsync));
                var testGuild = _client.GetGuild(testguildId);
                var botOwner = (await _client.GetApplicationInfoAsync()).Owner;
                var statusCommand = _service.GetSlashCommandInfo<ManualSlashModules>(nameof(ManualSlashModules.BotStatus));
                await _service.AddCommandsToGuildAsync(testGuild, false, disconnectCmd, statusCommand);
                try
                {
                    await _service.ModifySlashCommandPermissionsAsync(disconnectCmd, testGuild, new(testGuild.EveryoneRole, false), new(botOwner, true));
                    await _service.ModifySlashCommandPermissionsAsync(statusCommand, testGuild, new(testGuild.EveryoneRole, false), new(botOwner, true));
                }
                catch (Exception e)
                {
                    await _logger.Log(new(LogSeverity.Warning, nameof(RegisterManualCommands), "permission modification broken.", e));
                }
                await _logger.Log(new LogMessage(LogSeverity.Debug, nameof(RegisterManualCommands), "Registered disconnect + status command and modified permissions for it."));
            }
            else
            {
                await _logger.Log(new LogMessage(LogSeverity.Info, nameof(RegisterManualCommands), "Did not register disconnect + status command since no test server ID was provided."));
            }
        }

        private async Task CommandExecuted(ICommandInfo info, IInteractionContext bla, IResult result)
        {
            SocketInteractionContext<SocketInteraction> context = bla as SocketInteractionContext<SocketInteraction>;
            Func<string, Embed[], bool, bool, AllowedMentions, MessageComponent, Embed, RequestOptions, Task> respond;
            if (context.Interaction.HasResponded)
                respond = context.Interaction.FollowupAsync;
            else
                respond = context.Interaction.RespondAsync;
            CultureInfo.CurrentCulture = new(context.Interaction.UserLocale);
            CultureInfo.CurrentUICulture = new(context.Interaction.UserLocale);
            // if an Error occurred, send the Reason for the Error as response to the interaction, or as follow-up message.
            if (result.Error.HasValue)
            {
                switch (result.Error.Value)
                {
                    case InteractionCommandError.UnknownCommand:
                        await _logger.Log(new LogMessage(LogSeverity.Error, info.Name, $"Unknown interaction {info.Name}!"));
                        await respond(Properties.Resources.ErrorUnknownCommand, null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.ConvertFailed:
                        await _logger.Log(new LogMessage(LogSeverity.Warning, info.Name, $"Parameter(s) could not be converted: {result.ErrorReason}"));
                        await respond(String.Format(Properties.Resources.ErrorConvertFailed, result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.BadArgs:
                        int minCount = 0;
                        int maxCount = 0;
                        info.Parameters.ToList().ForEach(pi =>
                        {
                            if (pi.IsRequired)
                                minCount++;
                            maxCount++;
                        });
                        await _logger.Log(new LogMessage(LogSeverity.Error, info.Name, $"Too many or few parameters (expected {minCount} to {maxCount}): {result.ErrorReason}"));
                        await respond(String.Format(Properties.Resources.errorBadArgs,result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.Exception:
                        string logentry = "Exception while executing interaction.\n";
                        if (context.Guild != null) logentry += $"Guild: {context.Guild.Name} ({context.Guild.Id})\n";
                        logentry += $"User: {context.User.Username} ({context.User.Id})\nError message: {result.ErrorReason}";
                        await _logger.Log(new LogMessage(LogSeverity.Critical, info.Name, logentry));
                        await respond(String.Format(Properties.Resources.errorException,result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.Unsuccessful:
                        await _logger.Log(new LogMessage(LogSeverity.Warning, info.Name, $"Interaction execution unsuccessful: {result.ErrorReason}"));
                        await respond(String.Format(Properties.Resources.interactionUnsuccessful,result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.UnmetPrecondition:
                        await _logger.Log(new LogMessage(LogSeverity.Info, info.Name, $"User {context.User.Username} did not meet all preconditions: {result.ErrorReason}"));
                        await respond(String.Format(Properties.Resources.unmetPrecondition,result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                    case InteractionCommandError.ParseFailed:
                        await _logger.Log(new LogMessage(LogSeverity.Error, info.Name, $"Parsing of Command Context failed: {result.ErrorReason}"));
                        await respond(String.Format(Properties.Resources.errorParseFailed,result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                    default:
                        await _logger.Log(new LogMessage(LogSeverity.Critical, info.Name, $"Unknown Error Type encountered: {result.Error.Value} - {result.ErrorReason}"));
                        await respond(String.Format(Properties.Resources.errorException, result.ErrorReason), null, false, true, null, null, null, null);
                        break;
                }
            }
            else
            {
                // Log Command Execution
                await _logger.Log(new LogMessage(LogSeverity.Info, info.Name, context.Guild != null ? $"Interaction executed in guild {context.Guild.Name} ({context.Guild.Id})" : $"Interaction executed in DM with User {context.User.Username}#{context.User.Discriminator} ({context.User.Id})."));
            }
        }
    }
}
