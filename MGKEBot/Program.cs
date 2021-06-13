using System;
using Telegram.Bot;
using Telegram.Bot.Args;
using HtmlAgilityPack;
using Telegram.Bot.Types;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MGKEBot
{
    class Program
    {
        struct Day
        {
            public struct Subject
            {
                public string Title;
                public string Cabinet;
            }

            public string Title;
            public List<Subject> Subjects;
        }

        private const string TOKEN = "1622981140:AAFSwQR_LmwtklAVcu03DqFBxmLHfQQXEu0";

        private const string ONE_EMOJI = "1\ufe0f\u20e3";
        private const string TWO_EMOJI = "2\ufe0f\u20e3";
        private const string THREE_EMOJI = "3\ufe0f\u20e3";
        private const string FOUR_EMOJI = "4\ufe0f\u20e3";
        private const string DOT_EMOJI = "\ud83d\udd18";

        private static TelegramBotClient client;

        static List<Day> GetData()
        {
            var web = new HtmlWeb();
            var document = web.Load("http://mgke.minsk.edu.by/ru/main.aspx?guid=3831");

            var tables = document.DocumentNode.SelectNodes("//tbody");

            var firstTable = tables[0];

            var trNodes = firstTable.SelectNodes("//tr");

            List<Day> output = new List<Day>();

            for (int i = 0; i < trNodes.Count; i++)
            {
                var trNode = trNodes[i];
                for (int j = 0; j < trNode.ChildNodes.Count; j++)
                {
                    var tdNode = trNode.ChildNodes[j];
                    if (tdNode.InnerText.Contains("39"))
                    {
                        Day outputDay = new Day();

                        //название дня
                        outputDay.Title = trNodes[i - 1].ChildNodes[1].InnerText.Trim();

                        //заполнение дня названиями предметов
                        outputDay.Subjects = new List<Day.Subject>();

                        outputDay.Subjects.AddRange(new Day.Subject[]
                        {
                            new Day.Subject()
                            {
                                Title = String.Join("\n", trNodes[i + 2].ChildNodes[j + 2].ChildNodes[1].InnerHtml
                                    .Trim()
                                    .Split("<br>").Select((t) => t.Trim())),
                                Cabinet = String.Join("\n",
                                    trNodes[i + 2].ChildNodes[j + 4].ChildNodes[1].InnerHtml.Trim()
                                        .Split("<br>").Select((t) => t.Trim()))
                            },
                            new Day.Subject()
                            {
                                Title = String.Join("\n", trNodes[i + 3].ChildNodes[j + 2].ChildNodes[1].InnerHtml
                                    .Trim()
                                    .Split("<br>").Select((t) => t.Trim())),
                                Cabinet = String.Join("\n",
                                    trNodes[i + 3].ChildNodes[j + 4].ChildNodes[1].InnerHtml.Trim()
                                        .Split("<br>").Select((t) => t.Trim()))
                            },
                            new Day.Subject()
                            {
                                Title = String.Join("\n", trNodes[i + 4].ChildNodes[j + 2].ChildNodes[1].InnerHtml
                                    .Trim()
                                    .Split("<br>").Select((t) => t.Trim())),
                                Cabinet = String.Join("\n",
                                    trNodes[i + 4].ChildNodes[j + 4].ChildNodes[1].InnerHtml.Trim()
                                        .Split("<br>").Select((t) => t.Trim()))
                            },
                            new Day.Subject()
                            {
                                Title = String.Join("\n", trNodes[i + 5].ChildNodes[j + 2].ChildNodes[1].InnerHtml
                                    .Trim()
                                    .Split("<br>").Select((t) => t.Trim())),
                                Cabinet = String.Join("\n",
                                    trNodes[i + 5].ChildNodes[j + 4].ChildNodes[1].InnerHtml.Trim()
                                        .Split("<br>").Select((t) => t.Trim()))
                            }
                        });


                        output.Add(outputDay);
                    }
                }
            }

            return output;
        }

        private static string FormationDayString(Day day)
        {
            string output = "<b>" + day.Title + "</b>" + "\n\n";

            int counter = 1;
            foreach (var subject in day.Subjects.ToList())
            {
                //проверка предметов на пустоту

                if (subject.Title == "&nbsp;")
                {
                    day.Subjects.Remove(subject);
                    counter++;
                    continue;
                }
                
                //emoji
                switch (counter)
                {
                    case 1:
                        output += ONE_EMOJI;
                        break;
                    case 2:
                        output += TWO_EMOJI;
                        break;
                    case 3:
                        output += THREE_EMOJI;
                        break;
                    case 4:
                        output += FOUR_EMOJI;
                        break;
                    default:
                        throw new Exception("Owerflow emoji counter!");
                }

                output += "\n<b>" + subject.Title + "</b>\n";
                if (subject.Cabinet != "-")
                {
                    output += "<i>Кабинет:</i>\n<b>" + subject.Cabinet + "</b>\n";
                }

                counter++;
            }

            return output;
        }

        static void Main()
        {
            GetData();
            //todo
            Console.WriteLine("--------------------");

            client = new TelegramBotClient(TOKEN);
            client.StartReceiving();
            client.OnMessage += (async (sender, args) => await client.SendTextMessageAsync(args.Message.Chat.Id, FOUR_EMOJI));
            client.OnCallbackQuery += ClientOnCallbackQuery;
            Console.WriteLine("Press any key to stop!");
            Console.ReadKey();
            client.StopReceiving();
        }

        private static void ClientOnCallbackQuery(object? sender, CallbackQueryEventArgs e)
        {
            Console.WriteLine($"Чат #{e.CallbackQuery.Message.Chat.Id}, Запрос: {e.CallbackQuery.Data}");
            var data = GetData();
            if (e.CallbackQuery.Data == "1")
            {
                client.SendTextMessageAsync(e.CallbackQuery.Message.Chat.Id,
                    FormationDayString(data[0]), ParseMode.Html);
            }
            else
            {
                client.SendTextMessageAsync(e.CallbackQuery.Message.Chat.Id,
                    FormationDayString(data[1]), ParseMode.Html);
            }
        }

        private static async void ClientOnMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine($"Чат #{e.Message.Chat.Id}, Запрос: {e.Message.Text}");

            if (e.Message.Text == "Расписание")
            {
                var data = GetData();

                if (data.Count == 0)
                {
                    client.SendTextMessageAsync(e.Message.Chat.Id, "Распиние не найдено :(");
                }
                else if (data.Count == 1)
                {
                    client.SendTextMessageAsync(e.Message.Chat.Id, FormationDayString(data[0]), ParseMode.Html);
                }
                else
                {
                    var inlineKeyboard = new InlineKeyboardMarkup(new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData(data[0].Title, "1"),
                        InlineKeyboardButton.WithCallbackData(data[1].Title, "2")
                    });
                    client.SendTextMessageAsync(e.Message.Chat.Id, "Выберите вариант расписания:",
                        replyMarkup: inlineKeyboard);
                }
            }
            else
            {
                var replyKeyboard = new ReplyKeyboardMarkup(new KeyboardButton[]
                {
                    new KeyboardButton("Расписание")
                }, resizeKeyboard: false);
                client.SendTextMessageAsync(e.Message.Chat.Id,
                    "Введите \"Расписание\" или нажмите на кнопку!", replyMarkup: replyKeyboard);
            }
        }
    }
}