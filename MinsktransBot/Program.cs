using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Telegram.Bot;
using System.Xml.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MinsktransBot
{
    class Program
    {
        private static TelegramBotClient client;
        private const string TOKEN = "7445663207:AAEXw0NkZbsYiTu6S_Mn_C7kNvFPFQ9JcHU"; //todo

        private const string PREV_EMOJI = "\u2b05\ufe0f";
        private const string NEXT_EMOJI = "\u27a1\ufe0f";
        private const string TOP_EMOJI = "\u2b06\ufe0f";
        private const string GREEN_DOT_EMOJI = "\ud83d\udfe2";
        private const string YELLOW_DOT_EMOJI = "\ud83d\udfe1";
        private const string TIME_EMOJI = "\ud83d\udd54";

        private static DataParser parser;

        static async Task Main(string[] args)
        {
            await LoadData();
            Console.WriteLine($"First {parser.BusCollection[0].Number} : Last {parser.BusCollection[^1].Number} : Count {parser.BusCollection.Count}");

            //client = new TelegramBotClient(TOKEN);
            //client.StartReceiving(HandleUpdate, async (bot, ex, ct) => Console.WriteLine(ex));
            Console.ReadKey();
        }

        static async Task LoadData()
        {
            parser = new DataParser();

            await RefreshData();

            parser.DeserializeFromJson("BusCollection.xml");
        }

        static async Task RefreshData()
        {
            await parser.RefreshData();
            parser.SerializeToJson("BusCollection.xml");
        }

        private static async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                ClientOnMessage(update.Message ?? throw new Exception("Empthy Massage!"));
            }
            else if (update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                ClienOnCallbackQuery(update.CallbackQuery ?? throw new Exception("Empthy Callback Query!"));
            }
        }

        private static async void ClientOnMessage(Message message)
        {
            Console.WriteLine(message.Text);

            var keyboard = GetBusKeyboard(0);
            await client.SendTextMessageAsync(message.Chat.Id, "Выбирите автобус:", replyMarkup: keyboard);
        }

        private static async void ClienOnCallbackQuery(CallbackQuery callback)
        {
            Console.WriteLine($"[{callback.Data}]");

            var callbackArgs = callback.Data.Split('|');

            if (callbackArgs.Length < 2)
            {
                return;
            }

            ChatId chatId = callback.Message.Chat.Id;
            int messageId = callback.Message.MessageId;

            if (callbackArgs[0] == "BUS")
            {
                if (callbackArgs[1] == "SELECT")
                {
                    int busNumber = int.Parse(callbackArgs[2]);
                    var keyboard = GetDirectionKeyboard(0, busNumber);
                    await client.EditMessageTextAsync(chatId, messageId, "Выбирите направление:", replyMarkup: keyboard);
                }
                else if (callbackArgs[1] == "PAGE")
                {
                    int pageNumber = int.Parse(callbackArgs[2]);
                    var keyboard = GetBusKeyboard(pageNumber);
                    await client.EditMessageTextAsync(chatId, messageId, "Выбирите автобус:", replyMarkup: keyboard);
                }
            }
            else if (callbackArgs[0] == "DIRECTION")
            {
                int busNumber = int.Parse(callbackArgs[1]);
                if (callbackArgs[2] == "SELECT")
                {
                    int directionNumber = int.Parse(callbackArgs[3]);
                    var keyboard = GetStationKeyboard(0, busNumber, directionNumber);
                    await client.EditMessageTextAsync(chatId, messageId, "Выбирите остановку:", replyMarkup: keyboard);
                }
            }
            else if (callbackArgs[0] == "STATION")
            {
                int busNumber = int.Parse(callbackArgs[1]);
                int directionNumber = int.Parse(callbackArgs[2]);
                if (callbackArgs[3] == "SELECT")
                {
                    int stationNumber = int.Parse(callbackArgs[4]);
                    var times = GetNearTime(parser.BusCollection[busNumber].Directions[directionNumber].Stations[stationNumber].Days, 3);
                    if (times?.Count > 0)
                    {
                        string output = "";
                        int counter = 0;
                        foreach (var time in times)
                        {
                            if (counter == 0)
                            {
                                output += GREEN_DOT_EMOJI;
                            }
                            else
                            {
                                output += YELLOW_DOT_EMOJI;
                            }
                            output += Convert.ToUInt32(time.TotalMinutes - DateTime.Now.TimeOfDay.TotalMinutes) + "мин ";
                            output += TIME_EMOJI + time + "\n";
                            counter++;
                        }
                        await client.EditMessageTextAsync(chatId, messageId,
                            output,
                            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(TOP_EMOJI, $"DIRECTION|{busNumber}|SELECT|{directionNumber}")));
                    }
                    else
                    {
                        await client.EditMessageTextAsync(chatId, messageId,
                            "Этот автобус сейчас не ходит!",
                            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(TOP_EMOJI, $"DIRECTION|{busNumber}|SELECT|{directionNumber}")));
                    }
                }
                else if (callbackArgs[3] == "PAGE")
                {
                    int pageNumber = int.Parse(callbackArgs[4]);
                    var keyboard = GetStationKeyboard(pageNumber, busNumber, directionNumber);
                    await client.EditMessageTextAsync(chatId, messageId, "Выбирите остановку:", replyMarkup: keyboard);
                }
            }
            else
            {
                await client.AnswerCallbackQueryAsync(callback.Id, "Error");
                return;
            }

            await client.AnswerCallbackQueryAsync(callback.Id);
        }

        private static InlineKeyboardMarkup GetKeyboardFromCollection<T>(List<T> collection, int pageNumber, string prefix,
            Func<T, string> elementDataGetter,
            int rows = 8, int columns = 5, string backButton = null)
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

            if ((collection.Count > elementsPerPage) || backButton != null)
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

                if (backButton != null)
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

        private static InlineKeyboardMarkup GetDirectionKeyboard(int pageNumber, int busNumber)
        {
            return GetKeyboardFromCollection(parser.BusCollection[busNumber].Directions, 0, $"DIRECTION|{busNumber}",
                direction => direction.Title, 20, 1, backButton: $"BUS|PAGE|0");
        }

        private static InlineKeyboardMarkup GetStationKeyboard(int pageNumber, int busNumber, int directionNumber)
        {
            var foundBus = parser.BusCollection[busNumber];
            var foundDirection = foundBus.Directions[directionNumber];
            return GetKeyboardFromCollection(foundDirection.Stations, pageNumber, $"STATION|{busNumber}|{directionNumber}",
                direction => direction.Title, 8, 1, backButton: $"BUS|SELECT|{busNumber}");
        }

        private static List<TimeSpan> GetNearTime(List<DataParser.Day> days, int count)
        {
            var currentTime = DateTime.Now.TimeOfDay;
            Console.WriteLine(currentTime);
            DataParser.Day foundDay = null;
            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("буд"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("пн"));
                    }

                    break;
                case DayOfWeek.Tuesday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("буд"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("вт"));
                    }

                    break;
                case DayOfWeek.Wednesday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("буд"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("ср"));
                    }

                    break;
                case DayOfWeek.Thursday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("буд"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("чт"));
                    }

                    break;
                case DayOfWeek.Friday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("буд"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("пт"));
                    }

                    break;
                case DayOfWeek.Saturday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("вых"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("сб"));
                    }

                    break;
                case DayOfWeek.Sunday:
                    foundDay = days.SingleOrDefault(day => day.Title.Contains("вых"));
                    if (foundDay == null)
                    {
                        foundDay = days.SingleOrDefault(day => day.Title.Contains("вс"));
                    }

                    break;
            }

            if (foundDay != null)
            {
                foundDay.Time.ForEach(time=>Console.WriteLine(time));
                var nearTimes = foundDay.Time.Where(time => time > currentTime).ToList();
                if (nearTimes?.Count == 0)
                {
                    return null;
                }
                Console.WriteLine(nearTimes.Count);
                List<TimeSpan> output = new List<TimeSpan>();
                int counter = 0;
                foreach (var time in nearTimes)
                {
                    if (counter >= count)
                    {
                        break;
                    }
                    output.Add(time);
                    counter++;
                }
                return output;
            }
            else
            {
                return null;
            }
        }
    }
}