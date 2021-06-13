using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using System.Xml.Serialization;

namespace MinsktransBot
{
    class Program
    {
        private static TelegramBotClient client;
        private const string TOKEN = "1692023716:AAFLvx6ys9nYSxTmbCtnK8OHyswoBpe6A4E";

        private const string BUSLINK = "https://kogda.by/routes/minsk/autobus";

        private static DataParser parser;

        static void LoadData()
        {
            parser = new DataParser(BUSLINK);

            //parser.RefreshData();
            //parser.SerializeToJson("busCollection.xml");

            parser.DeserializeFromJson("busCollection.xml");
        }

        static void Main(string[] args)
        {
            LoadData();
            Console.WriteLine($"First {parser.BusCollection[0].Number} : Last {parser.BusCollection[^1].Number} : Count {parser.BusCollection.Count}");

            client = new TelegramBotClient(TOKEN);
            client.StartReceiving();

            client.OnMessage += ClientOnMessage;
            client.OnCallbackQuery += ClienOnCallbackQuery;

            Console.WriteLine("Press any key to stop!");
            Console.ReadKey();
            client.StopReceiving();
        }

        private static InlineKeyboardMarkup GetKeyboardFromCollection<T>(List<T> collection, int pageNumber, Func<T, string> printingMethod, Func<T, string> dataCallbackPrintingMethod, int rows = 8, int columns = 5)
        {
            int elementsPerPage = columns * rows;
            
            int startPosition = pageNumber * elementsPerPage;

            var buttons = new List<List<InlineKeyboardButton>>();


            for (int i = startPosition; i < startPosition + elementsPerPage;)
            {
                if (i >= collection.Count)
                {
                    break;
                }

                buttons.Add(new List<InlineKeyboardButton>());

                for (int j = 0; j < columns; j++)
                {
                    if (i >= collection.Count)
                    {
                        break;
                    }
                    string value = printingMethod.Invoke(collection[i]);
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData(value, "BUS_NUMBER|" + value));
                    i++;
                }
            }

            if (collection.Count > elementsPerPage)
            {
                buttons.Add(new List<InlineKeyboardButton>());
                
                if (pageNumber != 0)
                {
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData("Назад", "BUS_PREV|" + pageNumber));
                }

                if (collection.Count > (pageNumber*elementsPerPage)+elementsPerPage)
                {
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData("Далее", "BUS_NEXT|" + pageNumber));
                }
            }

            return new InlineKeyboardMarkup(buttons);
        }

        private static async void ClientOnMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Message.Text);

            var keyboard = GetKeyboardFromCollection<DataParser.Bus>(parser.BusCollection, 0, bus => bus.Number.ToString());
            await client.SendTextMessageAsync(e.Message.Chat.Id, "Выбирите автобус:", replyMarkup: keyboard);
        }

        private static async void ClienOnCallbackQuery(object? sender, CallbackQueryEventArgs e)
        {
            Console.WriteLine($"[{e.CallbackQuery.Data}]");

            var callbackArgs = e.CallbackQuery.Data.Split('|');

            if (callbackArgs.Length > 1)
            {
                return;
            }

            if (callbackArgs[0] == "BUS_NUMBER")
            {
                var foundBus = parser.BusCollection.Where(bus => bus.Number == callbackArgs[1]).FirstOrDefault();
                if (foundBus != null)
                {
                    await client.SendTextMessageAsync(e.CallbackQuery.Message.Chat.Id, foundBus.Number);
                }
            }

            if (callbackArgs[0] == "BUS_PREV")
            {
                var keyboard = GetKeyboardFromCollection<DataParser.Bus>(parser.BusCollection, int.Parse(callbackArgs[1]) - 1, bus => bus.Number.ToString());
                await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Выбирите автобус:", replyMarkup: keyboard);
            }

            if (callbackArgs[0] == "BUS_NEXT")
            {
                var keyboard = GetKeyboardFromCollection<DataParser.Bus>(parser.BusCollection, int.Parse(callbackArgs[1]) + 1, bus => bus.Number.ToString());
                await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Выбирите автобус:", replyMarkup: keyboard);
            }

            await client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
        }
    }
}