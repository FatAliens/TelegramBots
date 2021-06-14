using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace ch2Bot
{
    class Program
    {
        [Serializable]
        class Thread
        {
            public string Title;
            public string Text;

            public long Number;
            public List<string> Images;
        }

        class Comment
        {
            public string Text = "default";
            public string Images = "google.com";
        }

        static TelegramBotClient client;

        private static List<Thread> threads;

        public const string TOKEN = "";//todo

        public const string THREAD_EMOJI = "\u270d\ufe0f";
        public const string PHOTO_EMOJI = "\ud83d\udcf7";
        public const string NEXT_EMOJI = "\u27a1\ufe0f";
        public const string PREV_EMOJI = "\u2b05\ufe0f";

        public const string CATALOG_PATH = "https://2ch.hk/b/catalog.json";

        static void DownloadFile(string link, string fileName)
        {
            WebClient web = new WebClient();
            web.DownloadFile(link, fileName);
        }

        static List<Thread> GetThreads(string link)
        {
            JObject json = JObject.Parse(File.ReadAllText(link));

            var threadsJson = (JArray) json["threads"];

            var threads = new List<Thread>();

            foreach (JObject threadJson in threadsJson)
            {
                threads.Add(new Thread()
                {
                    Number = (long) threadJson["num"],
                    Title = (string) threadJson["subject"],
                    Text = (string) threadJson["comment"]
                });
                var filesJson = (JArray) threadJson["files"];
                threads.Last().Images = new List<string>();
                foreach (JObject fileJson in filesJson)
                {
                    //if file is jpeg or png
                    if ((int) fileJson["type"] <= 2)
                    {
                        threads.Last().Images.Add("2ch.hk" + (string) fileJson["path"]);
                    }
                }
            }

            return threads;
        }

        static List<Comment> GetComments(string threadNumber)
        {
            string threadLink = $"https://2ch.hk/b/res/{threadNumber}.json";
            string filePath = $"{threadNumber}.json";
            DownloadFile(threadLink, filePath);

            var json = JObject.Parse(File.ReadAllText(filePath));

            var comments = new List<Comment>();

            var commentsJson = (JArray)json["threads"][0]["posts"];

            foreach (var node in commentsJson)
            {
                comments.Add(new Comment()
                {
                    Text = (string) node["comment"]
                });
            }
            
            return comments;
        }

        static string GetClearString(string str)
        {
            string output = WebUtility.HtmlDecode(str);
            output = Regex.Replace(output, @"(<br>)+", "\n", RegexOptions.Compiled);
            output = Regex.Replace(output, @"<.+>", "", RegexOptions.Compiled);
            output = Regex.Replace(output, @"<\/.+>", "", RegexOptions.Compiled);
            if (string.IsNullOrWhiteSpace(output))
            {
                return output = "String is empthy!";
            }
            if (output.Length > 4000)
            {
                output = output.Substring(0, 4000);
            }

            return output.Trim();
        }

        static void Main(string[] args)
        {
            DownloadFile(CATALOG_PATH, "catalog.json");

            threads = GetThreads("catalog.json");

            Console.WriteLine($"Num = {threads[0].Number}");

            var comments = GetComments(threads[0].Number.ToString());
            
            foreach (var comment in comments)
            {
                if (!string.IsNullOrWhiteSpace(comment.Text))
                {
                    Console.WriteLine(GetClearString(comment.Text));
                }
            }
            
            
            
            /*client = new TelegramBotClient(TOKEN);
            client.StartReceiving();

            client.OnMessage += ClientOnMessage;
            client.OnCallbackQuery += ClientOnInline;

            Console.WriteLine("Press any key to stop!");
            Console.ReadKey();
            client.StopReceiving();*/
        }

        private static InlineKeyboardMarkup GetKeyboard(int threadNumber)
        {
            var buttons = new List<InlineKeyboardButton>();

            if (threadNumber != 0)
            {
                //to home button
                buttons.Add(InlineKeyboardButton.WithCallbackData(THREAD_EMOJI + threadNumber, "0"));
                //prev button
                buttons.Add(InlineKeyboardButton.WithCallbackData(PREV_EMOJI, (threadNumber - 1).ToString()));
            }

            if (threadNumber < threads.Count - 1)
            {
                //next button
                buttons.Add(InlineKeyboardButton.WithCallbackData(NEXT_EMOJI, (threadNumber + 1).ToString()));
            }

            if (threads[threadNumber].Images.Count != 0)
            {
                //show photo button
                buttons.Add(InlineKeyboardButton.WithCallbackData(PHOTO_EMOJI, "@" + threadNumber));
            }

            return new InlineKeyboardMarkup(buttons);
        }

        private static async void ClientOnInline(object? sender, CallbackQueryEventArgs e)
        {
            Console.WriteLine("Query: " + e.CallbackQuery.Data + " on [" + e.CallbackQuery.Id + "]");

            await client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

            if (e.CallbackQuery.Data.Contains("@"))
            {
                int number = int.Parse(e.CallbackQuery.Data.Substring(1));
                var album = new List<IAlbumInputMedia>();
                var images = threads[number].Images;

                Console.WriteLine("Send photo count: " + images.Count);
                foreach (var image in images)
                {
                    album.Add(new InputMediaPhoto(new InputMedia(image)));
                }

                try
                {
                    await client.SendMediaGroupAsync(album, e.CallbackQuery.Message.Chat.Id);
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException exception)
                {
                    Console.WriteLine("On SendImage Exception\nLinks:");
                    foreach (var image in images)
                    {
                        Console.WriteLine(image);
                    }
                    Console.WriteLine("--------------------------------------------");
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message);
                    Console.WriteLine("--------------------------------------------");
                }


                await client.DeleteMessageAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId);

                //string text = GetThreadString(threads[number]);todo
                string text = "";

                await client.SendTextMessageAsync(e.CallbackQuery.Message.Chat.Id,
                    text, disableWebPagePreview: true,
                    parseMode: ParseMode.Default, replyMarkup: GetKeyboard(number));
            }
            else
            {
                int threadNumber = int.Parse(e.CallbackQuery.Data);
                //string text = GetThreadString(threads[threadNumber]);todo
                string text = "";
                
                await client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId,
                    text, disableWebPagePreview: true,
                    parseMode: ParseMode.Default, replyMarkup: GetKeyboard(threadNumber));
            }
        }

        private static async void ClientOnMessage(object? sender, MessageEventArgs e)
        {
            Console.WriteLine(e.Message.Text + " on " + e.Message.Chat.Id);

            await client.SendTextMessageAsync(e.Message.Chat.Id, /*GetThreadString(threads[0])*/"",
                disableWebPagePreview: true, parseMode: ParseMode.Default, replyMarkup: GetKeyboard(0));
        }
    }
}