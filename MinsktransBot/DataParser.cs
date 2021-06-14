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

            public Bus()
            {
            }

            public Bus(List<Direction> directions, string number, string url)
            {
                Directions = directions;
                Number = number;
                Url = url;
            }
        }

        public class Direction
        {
            public string Title { get; set; }
            public List<Station> Stations { get; set; }

            public Direction()
            {
            }

            public Direction(string title, List<Station> stations)
            {
                Title = title;
                Stations = stations;
            }
        }

        public class Station
        {
            public string Title { get; set; }
            public string Url { get; set; }

            public List<string> Times { get; set; }

            public Station()
            {
            }

            public Station(string title, string url, List<string> times)
            {
                Title = title;
                Url = url;
                Times = times;
            }
        }

        public string BusLink { get; private set; }

        public List<Bus> BusCollection { get; private set; }

        public DataParser(string busLink)
        {
            BusLink = busLink;
            BusCollection = new List<Bus>();
        }

        public void RefreshData()
        {
            BusCollection = GetBusCollection();
            var timer = new Stopwatch();

            int maxBusLoad = 3;
            foreach (var bus in BusCollection)
            {
                if (maxBusLoad == 0)
                {
                    return;
                }
                timer.Restart();
                bus.Directions = GetStationPages(bus.Url);
                timer.Stop();
                Console.WriteLine($"[{timer.ElapsedMilliseconds} ms] {bus.Number} [{bus.Directions[0].Stations.Count} stops] [{bus.Directions.Count} directions]");
                maxBusLoad--;
            }
        }

        List<Bus> GetBusCollection()
        {
            var web = new HtmlWeb();
            var document = web.Load(BusLink);

            var busNodes =
                document.DocumentNode.SelectNodes(@"//a[@href]");
            foreach (var busNode in busNodes.ToList())
            {
                if (!busNode.Attributes["href"].Value.Contains("https://kogda.by/routes/minsk/autobus/"))
                {
                    busNodes.Remove(busNode);
                }
            }

            var busCollection = new List<Bus>();

            foreach (var busNode in busNodes)
            {
                busCollection.Add(new Bus()
                {
                    Number = busNode.InnerText.Trim(),
                    Url = busNode.Attributes["href"].Value.Trim()
                });
            }

            return busCollection;
        }

        List<Direction> GetStationPages(string busLink)
        {
            var web = new HtmlWeb();
            var stationCollection = new List<Station>();
            var htmlDocument = web.Load(busLink);

            var stationPagesDocuments = htmlDocument.DocumentNode.SelectNodes(@"//div[@class='panel panel-default']");

            var directions = new List<Direction>();

            foreach (var page in stationPagesDocuments)
            {
                string title = page.SelectSingleNode(@".//h4/a").InnerText.Trim();

                var stationDocuments = page.SelectSingleNode(".//ul").SelectNodes(".//a");

                var stations = new List<Station>();

                foreach (var stationDocument in stationDocuments)
                {
                    var times = new List<string>();
                    string url = stationDocument.Attributes["href"].Value + "/detailed";
                    stations.Add(new Station()
                    {
                        Title = stationDocument.InnerText.Trim(),
                        Url = url,
                        Times = LoadTime(url)
                    });
                }

                directions.Add(new Direction(title, stations));
            }

            return directions;
        }
        
        public List<string> LoadTime(string url)
        {
            var web = new HtmlWeb();
            var stationDocument = web.Load(url);
            var timeNodes = stationDocument.DocumentNode.SelectNodes("//span[@class='time']");

            var timeCollection = new List<string>();

            foreach (var timeNode in timeNodes)
            {
                timeCollection.Add(timeNode.InnerText.Trim());
            }

            return timeCollection;
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