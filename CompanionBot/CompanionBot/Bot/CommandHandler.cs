using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Bot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly GuildSettings _settings;
        private readonly Logger _logger;
        public CommandHandler(IServiceProvider services)
        {
            _services = services;
            _client = services.GetRequiredService<DiscordSocketClient>();
            _commands = services.GetRequiredService<CommandService>();
            _settings = services.GetRequiredService<GuildSettings>();
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
            if (!(messageParam is SocketUserMessage message)) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix(_settings[(message.Channel as IGuildChannel).Guild.Id].Prefix, ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                (message.Author.IsBot && !message.Author.IsWebhook))
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
            if (!string.IsNullOrEmpty(result?.ErrorReason))
            {
                await context.Channel.SendMessageAsync("Error: " + result.ErrorReason);
                await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Command error in guild {context.Guild.Id}: {result.ErrorReason}"));
            }
            else if (!result.IsSuccess)
            {
                if (result.Error.HasValue)
                {
                    await context.Channel.SendMessageAsync("Error: " + result.Error.Value.ToString());
                    await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Command error in guild {context.Guild.Id}: {result.Error.Value}"));
                }
                else
                {
                    await context.Channel.SendMessageAsync("Error: Unknown Error while Executing this Command!");
                    await _logger.Log(new LogMessage(LogSeverity.Warning, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Unknown Command error in guild {context.Guild.Id}!"));
                }
            }
            else
            {
                // Log Command Execution
                await _logger.Log(new LogMessage(LogSeverity.Info, command.IsSpecified ? command.Value.Name : this.GetType().Name, $"Command executed successfully in guild {context.Guild.Id}"));
            }
        }
    }
}