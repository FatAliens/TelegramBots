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
        private const string TOKEN = ""; //todo
        
        private const string PREV_EMOJI = "\u2b05\ufe0f";
        private const string NEXT_EMOJI = "\u27a1\ufe0f";
        private const string TOP_EMOJI = "\u2b06\ufe0f";

        private static DataParser parser;

        static void LoadData()
        {
            parser = new DataParser();

            RefreshData();

            parser.DeserializeFromJson("BusCollection.xml");
        }

        static void RefreshData()
        {
            parser.RefreshData();
            parser.SerializeToJson("BusCollection.xml");
        }

        static void Main(string[] args)
        {
            LoadData();
            Console.WriteLine($"First {parser.BusCollection[0].Number} : Last {parser.BusCollection[^1].Number} : Count {parser.BusCollection.Count}");

            /*
            client = new TelegramBotClient(TOKEN);
            client.StartReceiving();

            client.OnMessage += ClientOnMessage;
            client.OnCallbackQuery += ClienOnCallbackQuery;

            Console.WriteLine("Press any key to stop!");
            Console.ReadKey();
            client.StopReceiving();
            */
        }

        private static InlineKeyboardMarkup GetKeyboardFromCollection<T>(List<T> collection, int pageNumber, string prefix,
            Func<T, string> elementDataGetter,
            int rows = 8, int columns = 5, string backButton = "")
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

                    string text = elementDataGetter.Invoke(collection[i]);
                    string data = prefix + "|SELECT|" + i;
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData(text, data));
                    i++;
                }
            }

            if ((collection.Count > elementsPerPage)||backButton!="")
            {
                buttons.Add(new List<InlineKeyboardButton>());

                if (pageNumber != 0)
                {
                    string data = prefix + "|PAGE|" + (pageNumber - 1);
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData(PREV_EMOJI, data));
                }

                if (collection.Count > (pageNumber * elementsPerPage) + elementsPerPage)
                {
                    string data = prefix + "|PAGE|" + (pageNumber + 1);
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData(NEXT_EMOJI, data));
                }

                if (backButton!="")
                {
                    buttons[^1].Add(InlineKeyboardButton.WithCallbackData(TOP_EMOJI, backButton));
                }
            }

            return new InlineKeyboardMarkup(buttons);
        }

        private static InlineKeyboardMarkup GetBusKeyboard(int pageNumber)
        {
            return GetKeyboardFromCollection<DataParser.Bus>(parser.BusCollection, pageNumber,
                "BUS",
                bus => bus.Number.ToString());
        }

        private static async void ClientOnMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Message.Text);

            var keyboard = GetBusKeyboard(0);
            await client.SendTextMessageAsync(e.Message.Chat.Id, "Выбирите автобус:", replyMarkup: keyboard);
        }

        private static async void ClienOnCallbackQuery(object? sender, CallbackQueryEventArgs e)
        {
            Console.WriteLine($"[{e.CallbackQuery.Data}]");

            var callbackArgs = e.CallbackQuery.Data.Split('|');

            if (callbackArgs.Length < 2)
            {
                return;
            }

            if (callbackArgs[0] == "BUS")
            {
                if (callbackArgs[1] == "SELECT")
                {
                    var foundBus = parser.BusCollection[int.Parse(callbackArgs[2])];
                    if (foundBus != null)
                    {
                        var keyboard = GetKeyboardFromCollection(foundBus.Directions, 0, $"DIRECTION|{parser.BusCollection.IndexOf(foundBus)}",
                            direction => direction.Title, 20, 1, backButton: $"BUS|PAGE|0");
                        await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Выбирите направление:", replyMarkup: keyboard);
                    }
                }
                else if (callbackArgs[1] == "PAGE")
                {
                    var keyboard = GetBusKeyboard(int.Parse(callbackArgs[2]));
                    await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Выбирите автобус:", replyMarkup: keyboard);
                }
            }
            else if (callbackArgs[0] == "DIRECTION")
            {
                int busNumber = int.Parse(callbackArgs[1]);
                if (callbackArgs[2] == "SELECT")
                {
                    int directionNumber = int.Parse(callbackArgs[3]);
                    var foundDirection = parser.BusCollection[busNumber].Directions[directionNumber];
                    if (foundDirection != null)
                    {
                        var keyboard = GetKeyboardFromCollection(foundDirection.Stations, 0, $"STATION|{busNumber}|{directionNumber}",
                            direction => direction.Title, 8, 1, backButton: $"BUS|SELECT|{callbackArgs[1]}");
                        await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Выбирите остановку:", replyMarkup: keyboard);
                    }
                }
            }
            else if (callbackArgs[0] == "STATION")
            {
                int busNumber = int.Parse(callbackArgs[1]);
                int directionNumber = int.Parse(callbackArgs[2]);
                if (callbackArgs[3] == "SELECT")
                {
                    /*int stationNumber = int.Parse(callbackArgs[4]);
                    var times = parser.BusCollection[busNumber].Directions[directionNumber].Stations[stationNumber].Times;
                    if (times.Count > 0)
                    {
                        string text = "";
                        foreach (var time in times)
                        {
                            text += time + "\n";
                        }

                        await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, text, replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(TOP_EMOJI, $"DIRECTION|{busNumber}|SELECT|{directionNumber}")));
                    }*/
                }
                else if (callbackArgs[3] == "PAGE")
                {
                    var keyboard = GetKeyboardFromCollection(parser.BusCollection[busNumber].Directions[directionNumber].Stations, int.Parse(callbackArgs[4]), $"STATION|{busNumber}|{directionNumber}",
                        direction => direction.Title, 8, 1);
                    await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, "Выбирите остановку:", replyMarkup: keyboard);
                }
            }
            else
            {
                await client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "Error");
                return;
            }

            await client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
        }
    }
}