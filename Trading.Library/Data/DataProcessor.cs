﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Trading.Library.Data
{
    public class DataProcessor
    {
        private readonly string _connectionString;
        private readonly string _apiKey;
        private Database _db;
        public DataProcessor(string connectionString, string apiKey, Database db)
        {
            _connectionString = connectionString;
            _apiKey = apiKey;
            _db = db;
        }
        public async Task ProcessData(List<string> stocks)
        {
            //List<string> ftse100 = ReadFile(StockSymbolsPath);
            foreach (string stock in stocks)
            {
                string apiURL = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={stock}&interval=1min&apikey={_apiKey}&outputsize=compact&datatype=json"; //needs to be in config.json !!!

                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync(apiURL);

                    if (response.IsSuccessStatusCode)
                    {
                        string read = await response.Content.ReadAsStringAsync();
                        using (StringReader stringReader = new StringReader(read))
                        using (JReader jReader = new JReader(stringReader))
                        {
                            Database db = new Database(_connectionString);
                            JObject jObj = JObject.Load(jReader);
                            //Console.WriteLine(jObj.ToString());
                            var metaData = jObj["Meta_Data"];
                            string outputSize = metaData["Output_Size"].ToString(); //need to check if output size exists
                            string timeZone = metaData["Time_Zone"].ToString();                            
                            var data = jObj["Time_Series_(Daily)"];
                            Dictionary<string, Dictionary<string, string>> stockInfo = data.ToObject<Dictionary<string, Dictionary<string, string>>>();
                            string oldestDate = stockInfo.Keys.Reverse().ToList()[0];
                            DateTime oldestdDate = ConvertToDateTime(oldestDate);
                            foreach (string date in stockInfo.Keys.Reverse()) //I want the oldest day first; if I start with the newest day, I cannot calc returns5 for example, because I haveen't yet added the price 5 days ago to the database. 
                            { // as a result I should be able to calculate features in the same foreach loop.
                                DateTime currentDate = ConvertToDateTime(date);
                                if (!db.CheckFieldPopulated(currentDate, stock))
                                {
                                    db.InsertRecord(date, stock, decimal.Parse(stockInfo[date]["open"]), decimal.Parse(stockInfo[date]["high"]), decimal.Parse(stockInfo[date]["low"]), decimal.Parse(stockInfo[date]["close"]), decimal.Parse(stockInfo[date]["volume"]));
                                }
                                /*decimal price = -1;
                                price = db.GetData(currentDate, stock);
                                if (price != -1) //will not add all dates <= to the last day you logged in!. 
                                {
                                    db.InsertRecord(date, stock, decimal.Parse(stockInfo[date]["open"]), decimal.Parse(stockInfo[date]["high"]), decimal.Parse(stockInfo[date]["low"]), decimal.Parse(stockInfo[date]["close"]), decimal.Parse(stockInfo[date]["volume"]));
                                }*/
                            }
                            //Features feature = new Features(db);
                            //db.DeleteRecords();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"HTTP Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
            }

        }

        public void PopulateFeatures(List<string> stocks)
        {
            foreach (string stock in stocks)
            {
                Database db = new Database(_connectionString); //!! does this need to be here or in constructor?
                DateTime newestDate = DateTime.Now;
                newestDate = newestDate.AddDays(-1);
                DateTime oldestDate = new DateTime(2023, 09, 14); //ok this will always be this, just make it clear in writeup
                Features feature = new Features(oldestDate, db);
                bool checkDateRange = true;
                DateTime currentDate = oldestDate;
                while (checkDateRange)
                {
                    if (db.CheckFieldPopulated(currentDate, stock)) //checks if date in database, only days where the stock market is open are on database
                    {
                        Dictionary<string, int> returns = new Dictionary<string, int>() { { "Returns", 1 }, { "Returns5", 5 }, { "Returns20", 20 }, { "Returns40", 40 } };
                        foreach (string field in returns.Keys)
                        {
                            int n = returns[field];
                            if (feature.CheckValidReturns(currentDate, stock, n))//nice gpt function
                            {
                                decimal value = feature.CalculateReturn(stock, currentDate, n);
                                if (!db.CheckFieldPopulated(currentDate, stock, field))
                                {
                                    db.UpdateValue(currentDate, stock, field, value);
                                }
                            }
                        }
                        Dictionary<string, int> volatilities = new Dictionary<string, int>() { { "Volatility5", 5 }, { "Volatility20", 20 }, { "Volatility40", 40 } };
                        foreach (KeyValuePair<string, int> keyValuePair in volatilities)
                        {
                            string field = keyValuePair.Key;
                            int n = keyValuePair.Value;
                            if (feature.CheckValidReturns(currentDate, stock, n))
                            {
                                decimal value = feature.CalculateVolatility(stock, currentDate, n);
                                if (!db.CheckFieldPopulated(currentDate, stock, field))
                                {
                                    db.UpdateValue(currentDate, stock, field, value);
                                }
                            }
                        }
                        if (db.CheckFieldPopulated(currentDate, stock, "Returns40")) //returns5 will have to be populated if returns40 is populated
                        {
                            decimal oscillatorPrice = feature.CalculateOscillator(stock, currentDate, "Price");
                            decimal oscillatorVolatility = feature.CalculateOscillator(stock, currentDate, "Volatility");
                            db.UpdateValue(currentDate, stock, "Oscillator_Price", oscillatorPrice);
                            db.UpdateValue(currentDate, stock, "Oscillator_Volatility", oscillatorVolatility);
                        }

                    }
                    currentDate = currentDate.AddDays(1);
                    if (currentDate > newestDate) //dates newer than newest date in database --> leave the while loop
                    {
                        checkDateRange = false;
                    }
                }
            }
            


            //db.UpdateValue(newestDate, stock, "Returns5",Returns5);

        }


        public DateTime ConvertToDateTime(string inputDate)
        {
            string dateFormat = "yyyy-MM-dd";
            DateTime date = DateTime.ParseExact(inputDate, dateFormat, CultureInfo.InvariantCulture);
            return date;
        }
/*        public List<string> ReadFile(string filePath)
        {
            List<string> stocks = new List<string>();
            // Check if the file exists
            if (File.Exists(filePath))
            {
                // Open the file with a StreamReader
                using (StreamReader reader = new StreamReader(filePath))
                {
                    // Read the line and split it by commas to get stock symbols
                    string line = reader.ReadLine();
                    if (line != null)
                    {
                        stocks.AddRange(line.Split(','));
                    }
                }
            }
            else
            {
                Console.WriteLine("File not found: " + filePath);
            }
            return stocks;
        }*/
        public List<string> ReadFile(string filePath) //need name for this method. reading file new line seperated as apose to comma seperated
        {
            List<string> stocks = new List<string>();
            // Check if the file exists
            if (File.Exists(filePath))
            {
                // Open the file with a StreamReader
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    // Read lines until the end of the file
                    while ((line = reader.ReadLine()) != null)
                    {
                        stocks.Add(line);
                    }
                }
            }
            else
            {
                Console.WriteLine("File not found: " + filePath);
            }
            return stocks;
        }
    }
}