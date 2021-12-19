using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class Logger
    {
        public Task Log(LogMessage message)
        {
            string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message.Source.PadRight(11).Substring(0, 11) + " ";
            Console.Write(logEntry);
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    break;
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case LogSeverity.Info:
                    break;
                case LogSeverity.Verbose:
                    break;
                case LogSeverity.Debug:
                    break;
                default:
                    break;
            }
            Console.Write(message.Severity.ToString().PadRight(8));
            logEntry += message.Severity.ToString().PadRight(8);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(" " + message.Message);
            logEntry += " " + message.Message;
            if (message.Exception != null)
            {
                Console.WriteLine(" " + message.Exception);
                logEntry += " " + message.Exception;
            }
            else
            {
                Console.WriteLine();
            }
            try
            {
                File.AppendAllText("PNavCopy_Log.txt", logEntry+"\n");
            }
            catch(Exception e)
            {
                Console.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Logger      ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("Critical ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"Unable to write to Log File. {e}");
            }
            return Task.CompletedTask;
        }
    }
}
