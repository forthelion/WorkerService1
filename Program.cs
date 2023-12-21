
await host.RunAsync();
using Amazon.Runtime;
using Amazon.SQS.Model;
using Amazon.SQS;
using System.Text.Json;
using System.ServiceProcess;
using Amazon;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Xml;
using WorkerService1;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();




await host.RunAsync();
namespace DMVService
{
    public class Worker : BackgroundService
    {
        // you need to change your credtiantianl when running this part
        private const string AwsAccessKey = "ASIAVWS2Z4LH44V6TAMQ";
        private const string AwsSecretKey = "QzNOzmOiRRKqPhjWsPaDgU4AbbP5aUE22r4Ujvlw";
        private const string AwsSessionToken = "FwoGZXIvYXdzEOz//////////wEaDI3v7iv9hZ9OGReVGiLRAb+6NqP3bt2lETskThsovXHEmncwxw08RgubboPMdq5Xgau8m7wP/ytoll8Ijc+pOJ/kldyN7AA4qbeIM4o7PmAHAyNDyV6edwANNyOYItfMP2i8W3gDmJWYtMKR9ExZEm4lt4Bmv7/C6ipbveRg9m2VApe8Ohjv5WchmOqlx7kWkRq25HyslqOuX0LxhZjMO8Lk6Jd3uGr5SJrHFu2LgljHm6xrbEUMKdjElQLt4Jg27M+EVT1wHrnuKWWCCqvTKdsdCjCWS0TtQkOX6SHN3fTTKOSa9aMGMi2ysjr5VE5MuMubqR6hJU6nfDCKF/TlH/fCfb8z6ZJlJc+wKm59xU1XoWlZINI=";

        private readonly ILogger<Worker> _logger;

        // Change here to fit local file mangement 
        //private const string logPath = @"H:\Temp\InsuranceDataService.log";
        private const string logPath = @"C:\Users\Igor\Documents\AWSSERVERFILE\log.txt";
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                //while (true) {
                await pollFromQueue();
                //}
                await Task.Delay(1000, stoppingToken);
                //await uploadToQueue();
            }
        }
        public static void WriteToLog(string message)
        {
            string text = String.Format("{0}:\t{1}", DateTime.Now, message);
            using (StreamWriter writer = new StreamWriter(logPath, append: true))
            {
                writer.WriteLine(text);

            }
        }

        public static async Task pollFromQueue()
        {
            try
            {
                string downQueue = "https://sqs.us-east-1.amazonaws.com/392106205903/downwardQueue";

                AWSCredentials credentials = new SessionAWSCredentials(AwsAccessKey, AwsSecretKey, AwsSessionToken);

                IAmazonSQS client = new AmazonSQSClient(credentials, RegionEndpoint.USEast1);

                var msg = await GetMessage(client, downQueue, 0);

                if (msg.Messages.Count == 0)
                {
                    return;
                }
                string carInformationJson = msg.Messages[0].Body;

                WriteToLog("Read message: " + carInformationJson);

                List<string> carInformationExtracted = ParseJSON(carInformationJson);

                WriteToLog("id is: " + carInformationExtracted);

                string carInformation = null;
                string location = null;
                string date = null;
                string type_Of_Violation = null;
                if (carInformationExtracted.Count >= 2)
                {
                    location = carInformationExtracted[0];
                    date = carInformationExtracted[1];
                    type_Of_Violation = carInformationExtracted[2];
                    carInformation = carInformationExtracted[3];
                }





                List<(string plate, string make, string model, string color, string ownerName, string ownerContact)> vehicleList = xmlXpath();

                if (msg.Messages.Count == 0)
                {
                    // No messages in the queue
                    // Handle this condition 
                    WriteToLog("No messages in the down queue.");
                    await Task.Delay(1000);

                }
                else
                {

                    await DeleteMessage(client, msg.Messages[0], downQueue);


                    bool driverFound = false;

                    foreach (var vehicle in vehicleList)
                    {
                        string plateNumberFromTuple = vehicle.plate;
                        if (carInformation.Contains(plateNumberFromTuple))
                        {
                            // Perform your desired actions when a match is found
                            string make = vehicle.make;
                            string model = vehicle.model;
                            string color = vehicle.color;
                            string ownerName = vehicle.ownerName;
                            string ownerContact = vehicle.ownerContact;

                            Console.WriteLine("Plate: " + plateNumberFromTuple);
                            Console.WriteLine("Make: " + make);
                            Console.WriteLine("Model: " + model);
                            Console.WriteLine("Color: " + color);
                            Console.WriteLine("Owner Name: " + ownerName);
                            Console.WriteLine("Owner Contact: " + ownerContact);

                            int ticketPrice = 0;

                            switch (type_Of_Violation)
                            {
                                case "no_stop":
                                    ticketPrice = 300;
                                    break;
                                case "no_full_stop_on_right":
                                    ticketPrice = 75;
                                    break;
                                case "no_right_on_red":
                                    ticketPrice = 125;
                                    break;
                                default:
                                    ticketPrice = 0; // If the violation type is not recognized, assign a default value or handle the case accordingly.
                                    break;
                            }


                            var data = new
                            {
                                owner = ownerName,
                                ownerContact = ownerContact,

                                Vehicle = new
                                {
                                    Color = color,
                                    Make = make,
                                    Model = model
                                },
                                LicensePlate = vehicle.plate,
                                Date = date,
                                ViolationAddress = location,
                                ViolationType = type_Of_Violation,
                                TicketAmount = ticketPrice
                            };


                            string messageForEmail = JsonConvert.SerializeObject(data);

                            await uploadToQueue(messageForEmail);
                            WriteToLog("Posted message: " + messageForEmail);
                            driverFound = true;
                            break; // If you only need to find the first match, you can exit the loop
                        }
                    }

                    if (!driverFound)
                    {
                        Console.WriteLine("Driver not found!");
                    }

                    //var json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                    //await uploadToQueue(json);
                    //WriteToLog("Posted message: " + json);

                    //patientIDextracted = msg.Messages[0].Body;




                }



            }
            catch (Exception ex)
            {
                // Handle the exception
                WriteToLog($"Exception occurred: {ex}");
                await Task.Delay(1000);
            }
        }


        public static List<string> ParseJSON(string carinfodmv)
        {

            List<string> extractedData = new List<string>();
            JObject jsonObject = JObject.Parse(carinfodmv);

            extractedData.Add((string)jsonObject["location"]);
            extractedData.Add((string)jsonObject["date"]);
            extractedData.Add((string)jsonObject["type_of_violation"]);
            extractedData.Add((string)jsonObject["plateInformation"]);

            return extractedData;
        }

        static List<(string plate, string make, string model, string color, string ownerName, string ownerContact)> xmlXpath()
        {
            Console.WriteLine("start");
            // change the code here to  to get your xml database
            //string file_path = ;



            // create the list of patientIDds
            List<(string plate, string make, string model, string color, string ownerName, string ownerContact)> vehicleList = new List<(string, string, string, string, string, string)>();



            XmlDocument xmlDoc = new XmlDocument();

            // change here for local storage 
            xmlDoc.Load("C:\\Users\\Igor\\Downloads\\DMVDatabase.xml");
            // errer here 
            XmlNodeList patientNodes = xmlDoc.SelectNodes("/insuranceDatabase/patient");

            XmlNodeList vehicleNodes = xmlDoc.SelectNodes("/dmv/vehicle");
            foreach (XmlNode vehicleNode in vehicleNodes)
            {
                string plate = vehicleNode.Attributes["plate"].Value;
                string make = vehicleNode.SelectSingleNode("make").InnerText;
                string model = vehicleNode.SelectSingleNode("model").InnerText;
                string color = vehicleNode.SelectSingleNode("color").InnerText;
                string ownerName = vehicleNode.SelectSingleNode("owner/name").InnerText;
                string ownerContact = vehicleNode.SelectSingleNode("owner/contact").InnerText;

                vehicleList.Add((plate, make, model, color, ownerName, ownerContact));
            }

            // Access and print the vehicle information from the XML database
            foreach (var vehicle in vehicleList)
            {
                Console.WriteLine($"Plate: {vehicle.plate}");
                Console.WriteLine($"Make: {vehicle.make}");
                Console.WriteLine($"Model: {vehicle.model}");
                Console.WriteLine($"Color: {vehicle.color}");
                Console.WriteLine($"Owner Name: {vehicle.ownerName}");
                Console.WriteLine($"Owner Contact: {vehicle.ownerContact}");
                Console.WriteLine();
            }
            return vehicleList;
        }









        private static async Task<ReceiveMessageResponse> GetMessage(
            IAmazonSQS sqsClient, string qUrl, int waitTime = 0)
        {
            return await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = qUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = waitTime
                // (Could also request attributes, set visibility timeout, etc.)
            });
        }

        private static async Task DeleteMessage(
            IAmazonSQS sqsClient, Message message, string qUrl)
        {
            WriteToLog($"\nDeleting message {message.MessageId} from queue...");
            await sqsClient.DeleteMessageAsync(qUrl, message.ReceiptHandle);
        }

        public static async Task uploadToQueue(string message)
        {
            WriteToLog("in uploadToQueue");
            // update credtaintional to work 
            string queueUrl = "https://sqs.us-east-1.amazonaws.com/392106205903/UpwardQueue";

            AWSCredentials credentials = new SessionAWSCredentials(AwsAccessKey, AwsSecretKey, AwsSessionToken);

            WriteToLog("creadentials created");

            // Create an Amazon SQS client object using the
            // default user. If the AWS Region you want to use
            // is different, supply the AWS Region as a parameter.
            IAmazonSQS client = new AmazonSQSClient(credentials, RegionEndpoint.USEast1);

            var request = new SendMessageRequest
            {
                MessageBody = message,
                QueueUrl = queueUrl,
            };

            var response = await client.SendMessageAsync(request);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                WriteToLog($"Successfully sent message. Message ID: {response.MessageId}");
            }
            else
            {
                WriteToLog("Could not send message.");
            }
        }


    }
}

