using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using HtmlAgilityPack;

namespace MinsktransBot
{
    public class DataParser
    {
        public class Bus
        {
            public List<Direction> Directions { get; set; }
            public string Number { get; set; }
            public string Url { get; set; }
        }

        public class Direction
        {
            public string Title { get; set; }
            public List<Station> Stations { get; set; }
        }

        public class Station
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public List<Day> Days { get; set; }
        }

        public class Day
        {
            public string Title { get; set; }
            public List<TimeSpan> Time { get; set; }
        }

        public const string BusLink = "https://minsk.btrans.by/avtobus";

        public List<Bus> BusCollection { get; private set; }

        public DataParser()
        {
            BusCollection = new List<Bus>();
        }

        public void RefreshData()
        {
            BusCollection = GetBusCollection();

            var timer = new Stopwatch();

            int counter = 0;
            foreach (var bus in BusCollection)
            {
                if (counter >= 200)
                {
                    return;
                }

                timer.Restart();
                bus.Directions = GetDirections(bus.Url);
                timer.Stop();
                Console.WriteLine($"[{timer.ElapsedMilliseconds} ms] {bus.Number} [{bus.Directions[0].Stations.Count} stops] [{bus.Directions.Count} directions]");
                counter++;
            }
        }

        private HtmlDocument LoadHtmlDocument(string url)
        {
            var web = new HtmlWeb();
            web.PreRequest += request =>
            {
                request.CookieContainer = new System.Net.CookieContainer();
                return true;
            };
            return web.Load(url);
        }

        List<Bus> GetBusCollection()
        {
            var document = LoadHtmlDocument(BusLink);

            var busNodes =
                document.DocumentNode.SelectNodes(@"//a[@class='hexagon-link-content']");

            var busCollection = new List<Bus>();

            foreach (var busNode in busNodes)
            {
                if (int.TryParse(busNode.InnerText, out int n))
                {
                    busCollection.Add(new Bus()
                    {
                        Number = busNode.InnerText.Trim(),
                        Url = "https://minsk.btrans.by" + busNode.Attributes["href"].Value.Trim()
                    });
                }
            }

            return busCollection;
        }

        List<Direction> GetDirections(string busLink)
        {
            var htmlDocument = LoadHtmlDocument(busLink);

            var directionDocuments = htmlDocument.DocumentNode.SelectNodes(@"//div[@class='direction']");

            var directions = new List<Direction>();

            foreach (var directionDocument in directionDocuments)
            {
                string title = directionDocument.SelectSingleNode(@".//h2").InnerText.Trim();

                var stationDocuments = directionDocument.SelectSingleNode(".//ul").SelectNodes(".//a");

                var stations = new List<Station>();

                foreach (var stationDocument in stationDocuments)
                {
                    string stationUrl = "https://minsk.btrans.by" + stationDocument.Attributes["href"].Value + "/detailed";
                    string stationTitle = stationDocument.InnerText.Trim();
                    stations.Add(new Station()
                    {
                        Title = stationTitle,
                        Url = stationUrl,
                        Days = LoadDays(stationUrl)
                    });
                }

                directions.Add(new Direction() {Title = title, Stations = stations});
            }

            return directions;
        }

        private List<Day> LoadDays(string url)
        {
            var stationDocument = LoadHtmlDocument(url);
            var timeCellNodes = stationDocument.DocumentNode.SelectNodes("//div[@class='timetable-ceil']");

            var dayCollection = new List<Day>();

            var headerNodes = stationDocument.DocumentNode
                .SelectSingleNode("//div[@class='timetable-ceil timetable-ceil__additional']/div[@class='timetable-ceil-day-item']")
                .ChildNodes.Where(div => div.Attributes["class"].Value.Contains("timetable-ceil-day-minutes"));
            foreach (var header in headerNodes)
            {
                dayCollection.Add(new Day() {Title = header.InnerText.Trim(), Time = new List<TimeSpan>()});
            }

            foreach (var timeCell in timeCellNodes)
            {
                int hour = int.Parse(timeCell.SelectSingleNode("./div[@class='timetable-ceil-hour']").InnerText);

                var divNodes = timeCell.SelectSingleNode(".//div[@class='timetable-ceil-day-item']").ChildNodes;
                string header = null;
                foreach (var div in divNodes)
                {
                    if (div.Attributes["class"].Value.Contains("timetable-ceil-day-header"))
                    {
                        header = div.InnerText.Trim();
                    }
                    else if (div.Attributes["class"].Value.Contains("timetable-ceil-day-minutes"))
                    {
                        string classAttribute = div.Attributes["class"].Value;
                        string[] minutes = div.InnerText.Trim().Split(' ');
                        if (minutes[0] != "")
                        {
                            foreach (string minute in minutes)
                            {
                                var currentDay = dayCollection.Where(day => day?.Title == header).FirstOrDefault();
                                currentDay.Time.Add(new TimeSpan(hour, int.Parse(minute), 0));
                            }
                        }
                    }
                }
            }

            return dayCollection;
        }

        public void SerializeToJson(string filePath)
        {
            var writer = new StreamWriter(filePath);
            var serializer = new XmlSerializer(typeof(List<DataParser.Bus>));
            serializer.Serialize(writer, BusCollection);
            writer.Close();
        }

        public void DeserializeFromJson(string filePath)
        {
            BusCollection.Clear();
            var reader = new StreamReader(filePath);
            var serializer = new XmlSerializer(typeof(List<DataParser.Bus>));
            BusCollection = (List<DataParser.Bus>) serializer.Deserialize(reader);
            reader.Close();
        }
    }
}