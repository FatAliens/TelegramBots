using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotTest
{
    class Program
    {
        static TelegramBotClient client;
        public const string TOKEN = "1798836537:AAFJoboEHVb_wHMer80EQOGK7zrhXu4pXkg";

        static void Main(string[] args)
        {
            client = new TelegramBotClient(TOKEN);
            client.StartReceiving();
            client.OnMessage += ClientOnMessage;
            client.OnInlineQuery += ClientOnInlineQuery;
            Console.WriteLine("Press any key to stop!");
            Console.ReadKey();
            client.StopReceiving();
        }

        private static async void ClientOnInlineQuery(object? sender, InlineQueryEventArgs e)
        {
            // Inline Results
            InlineQueryResultBase[] results = new[]
            {
                new InlineQueryResultVenue(e.InlineQuery.Id, 30, 30, "Venue title", "address")
            };

            // Answer with results:
            await client.AnswerInlineQueryAsync(e.InlineQuery.Id,
                results,
                isPersonal: true);
        }


        private static void ClientOnMessage(object? sender, MessageEventArgs e)
        {
            SimulateTyping(e);
        }

        private static async Task SimulateTyping(MessageEventArgs e)
        {
            await client.SendChatActionAsync(e.Message.Chat.Id, ChatAction.Typing);
            Thread.Sleep(600);
            await client.SendTextMessageAsync(e.Message.Chat.Id, "text1");
        }
    }
}