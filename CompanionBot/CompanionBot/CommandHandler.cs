using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CompanionBot
{
    internal class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly Logger _logger;
        public CommandHandler(IServiceProvider services)
        {
            _services = services;
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _logger = services.GetRequiredService<Logger>();
        }

        public async Task InstallCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            _commands.CommandExecuted += OnCommandExecutedAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide (https://docs.stillu.cc/guides/commands/dependency-injection.html) for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: _services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (messageParam is not SocketUserMessage message) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on a mention inside of a guild and make sure no bots trigger commands
            if (message.Channel is not IGuildChannel ||
                (message.Author.IsBot && !message.Author.IsWebhook) ||
                !message.HasMentionPrefix(_client.CurrentUser, ref argPos))
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: _services);
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // if an Error occurred, send the Reason for the Error in the Channel where the Command was executed.
            if (result.Error.HasValue)
            {
                ChannelPermissions perms = (context.Guild as SocketGuild).GetUser(_client.CurrentUser.Id).GetPermissions(context.Channel as IGuildChannel);
                switch (result.Error.Value)
                {
                    case CommandError.UnknownCommand:
                        await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : "unknown", $"Unknown Command in guild {context.Guild.Name} ({context.Guild.Id}): {context.Message.Resolve()}."));
                        //if (perms.SendMessages)
                        //    await context.Message.ReplyAsync("Unknown command.");
                        break;
                    case CommandError.ParseFailed:
                        await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Parsing of command failed in guild {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"Invalid command structure: {result.ErrorReason}.");
                        break;
                    case CommandError.BadArgCount:
                        int minCount = 0;
                        int maxCount = 0;
                        command.Value.Parameters.ToList().ForEach(pi => {
                            if (!pi.IsOptional)
                                minCount++;
                            maxCount++;
                        });
                        await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Too few or many parameters. Expected {minCount} - {maxCount}."));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"Too few or too many parameters.\nExpected {minCount} to {maxCount}.");
                        break;
                    case CommandError.ObjectNotFound:
                        await _logger.Log(new LogMessage(LogSeverity.Error, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"ObjectNotFound while parsing parameter in guild  {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"Could not parse Command parameter(s): {result.ErrorReason}.");
                        break;
                    case CommandError.MultipleMatches:
                        await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Multiple matches while parsing parameter in guild  {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"There are multiple matches for one or more parameters. Please refine your query.\n```{result.ErrorReason}```");
                        break;
                    case CommandError.UnmetPrecondition:
                        await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Unmet precondition in guild  {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"This command is not allowed in the current context: {result.ErrorReason}.");
                        break;
                    case CommandError.Exception:
                        await _logger.Log(new LogMessage(LogSeverity.Error, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"CommandHandler Exception thrown for guild  {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"⚠Internal Error.⚠\nThe bot has encountered a problem while executing this command. If the error persists, please open a GitHub issue at https://github.com/MaxEtMoritz/PNavCopy, providing as much information as possible.\nThis Error message will (hopefully😅) help the developer investigate:\n```{result.ErrorReason}```");
                        break;
                    case CommandError.Unsuccessful:
                        await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Unsuccessful command in guild  {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync($"This command failed: {result.ErrorReason}.");
                        break;
                    default:
                        await _logger.Log(new LogMessage(LogSeverity.Warning, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Command error in guild {context.Guild.Name} ({context.Guild.Id}): {result.ErrorReason}"));
                        if (perms.SendMessages)
                            await context.Message.ReplyAsync("Error: " + result.ErrorReason);
                        break;
                }
            }
            else
            {
                // Log Command Execution
                await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Command executed in guild {context.Guild.Name} ({context.Guild.Id})"));
            }
        }
    }
}