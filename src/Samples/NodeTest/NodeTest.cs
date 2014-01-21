//#define FASTEST_POSSIBLE
#define FILL_RANDOMLY
#if FASTEST_POSSIBLE
#undef FILL_RANDOMLY
#endif

using Starcounter;
using Starcounter.Advanced;
using Starcounter.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Starcounter.TestFramework;
using System.Net.WebSockets;

namespace NodeTest
{
    class Settings
    {
        public enum AsyncModes
        {
            ModeSync,
            ModeAsync,
            ModeRandom
        }

        public enum ProtocolTypes
        {
            ProtocolHttpV1,
            ProtocolWebSockets
        }

        public const String ServerNodeTestHttpRelativeUri = "/echotest";

        public const String ServerNodeTestWsRelativeUri = "/echotestws";

        public String ServerIp = "127.0.0.1";

        public UInt16 ServerPort = 8080;

        public static String CompleteHttpUri;

        public static String CompleteWebSocketUri;

        public Int32 NumWorkers = 3;

        public Int32 MinEchoBytes = 1;

        public Int32 MaxEchoBytes = 10000;

        public Int32 NumEchoesPerWorker = 30000;

        public Int32 NumSecondsToWait = 5000;

        public AsyncModes AsyncMode = AsyncModes.ModeRandom;

        public ProtocolTypes ProtocolType = ProtocolTypes.ProtocolHttpV1;

        public Boolean UseAggregation = false;

        public Boolean ConsoleDiag = false;

        public void Init(string[] args)
        {
            foreach (String arg in args)
            {
                if (arg.StartsWith("-ServerIp="))
                {
                    ServerIp = arg.Substring("-ServerIp=".Length);
                }
                else if (arg.StartsWith("-ServerPort="))
                {
                    ServerPort = UInt16.Parse(arg.Substring("-ServerPort=".Length));
                }
                else if (arg.StartsWith("-ProtocolType="))
                {
                    String protocolTypeParam = arg.Substring("-ProtocolType=".Length);

                    if (protocolTypeParam == "ProtocolHttpV1")
                        ProtocolType = ProtocolTypes.ProtocolHttpV1;
                    else if (protocolTypeParam == "ProtocolWebSockets")
                        ProtocolType = ProtocolTypes.ProtocolWebSockets;
                }
                else if (arg.StartsWith("-NumWorkers="))
                {
                    NumWorkers = Int32.Parse(arg.Substring("-NumWorkers=".Length));
                }
                else if (arg.StartsWith("-MinEchoBytes="))
                {
                    MinEchoBytes = Int32.Parse(arg.Substring("-MinEchoBytes=".Length));
                }
                else if (arg.StartsWith("-MaxEchoBytes="))
                {
                    MaxEchoBytes = Int32.Parse(arg.Substring("-MaxEchoBytes=".Length));
                }
                else if (arg.StartsWith("-NumEchoesPerWorker="))
                {
                    NumEchoesPerWorker = Int32.Parse(arg.Substring("-NumEchoesPerWorker=".Length));
                }
                else if (arg.StartsWith("-NumSecondsToWait="))
                {
                    NumSecondsToWait = Int32.Parse(arg.Substring("-NumSecondsToWait=".Length));
                }
                else if (arg.StartsWith("-AsyncMode="))
                {
                    String asyncParam = arg.Substring("-AsyncMode=".Length);

                    if (asyncParam == "ModeSync")
                        AsyncMode = AsyncModes.ModeSync;
                    else if (asyncParam == "ModeAsync")
                        AsyncMode = AsyncModes.ModeAsync;
                    else if (asyncParam == "ModeRandom")
                        AsyncMode = AsyncModes.ModeRandom;
                }
                else if (arg.StartsWith("-UseAggregation="))
                {
                    UseAggregation = Boolean.Parse(arg.Substring("-UseAggregation=".Length));
                }
                else if (arg.StartsWith("-ConsoleDiag="))
                {
                    ConsoleDiag = Boolean.Parse(arg.Substring("-ConsoleDiag=".Length));
                }
            }

            CompleteHttpUri = "http://" + ServerIp + ":" + ServerPort + ServerNodeTestHttpRelativeUri;
            CompleteWebSocketUri = "ws://" + ServerIp + ":" + ServerPort + ServerNodeTestWsRelativeUri;
        }
    }

    class NodeTestInstance
    {
        UInt64 unique_id_;

        Int32 num_echo_bytes_;

        Boolean async_;

        Boolean useNodeX_;

#if !FASTEST_POSSIBLE
        Byte[] correct_hash_;
#endif

        Byte[] body_bytes_;

        Byte[] resp_bytes_;

        Settings settings_;

        Worker worker_;

        // Initializes new test instance.
        public void Init(
            Settings settings,
            Worker worker,
            UInt64 unique_id,
            Boolean async,
            Int32 num_echo_bytes)
        {
            settings_ = settings;
            worker_ = worker;
            unique_id_ = unique_id;
            async_ = async;

            if (settings.UseAggregation)
            {
                useNodeX_ = false;
                async_ = true;
            }
            else
            {
                useNodeX_ = ((num_echo_bytes_ % 2) == 0) ? true : false;
            }

            num_echo_bytes_ = num_echo_bytes;

            body_bytes_ = new Byte[num_echo_bytes_];
            resp_bytes_ = new Byte[num_echo_bytes_];

#if FILL_RANDOMLY
            // Generating random bytes.
            worker_.Rand.NextBytes(body_bytes_);
#else

            // Filling bytes continuously between 0 and 9
            Byte k = 0;
            for (Int32 i = 0; i < num_echo_bytes_; i++)
            {
                body_bytes_[i] = (Byte)('1' + k);
                //k++;
                //if (k >= 10) k = 0;
            }

#endif

#if !FASTEST_POSSIBLE
            // Calculating SHA1 hash.
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            correct_hash_ = sha1.ComputeHash(body_bytes_);
#endif
        }

        /// <summary>
        /// Performs a session of WebSocket echoes.
        /// </summary>
        /// <param name="bodyBytes"></param>
        /// <param name="respBytes"></param>
        /// <returns></returns>
        public async Task PerformSyncWebSocketEcho(Byte[] bodyBytes, Byte[] respBytes)
        {
            using (ClientWebSocket ws = new ClientWebSocket())
            {
                // Establishing WebSockets connection.
                Uri serverUri = new Uri(Settings.CompleteWebSocketUri);
                await ws.ConnectAsync(serverUri, CancellationToken.None);

                // Within one connection performing several echoes.
                for (Int32 x = 0; x < worker_.Rand.Next(10); x++)
                {
                    ArraySegment<byte> bytesToSend = new ArraySegment<byte>(bodyBytes);

                    // Sending data in preferred format: text or binary.
                    await ws.SendAsync(
                        bytesToSend, WebSocketMessageType.Binary,
                        true, CancellationToken.None);

                    ArraySegment<byte> bytesReceived = new ArraySegment<byte>(respBytes, 0, respBytes.Length);

                    WebSocketReceiveResult result = await ws.ReceiveAsync(bytesReceived, CancellationToken.None);

                    // Checking if need to continue accumulation.
                    if (result.Count < bodyBytes.Length)
                    {
                        Int32 totalReceived = result.Count;

                        // Accumulating until all data is received.
                        while (totalReceived < bodyBytes.Length)
                        {
                            bytesReceived = new ArraySegment<byte>(respBytes, totalReceived, respBytes.Length - totalReceived);

                            result = await ws.ReceiveAsync(bytesReceived, CancellationToken.None);

                            totalReceived += result.Count;
                        }
                    }

                    // Creating response from received byte array.
                    Response resp = new Response { BodyBytes = resp_bytes_ };

                    // Checking the response and generating an error if a problem found.
                    if (!CheckResponse(resp))
                        throw new Exception("Incorrect WebSocket response of length: " + resp_bytes_.Length);
                }
            }
        }

        /// <summary>
        /// Performs a session of WebSocket4Net echoes.
        /// </summary>
        /// <param name="bodyBytes"></param>
        /// <param name="respBytes"></param>
        /// <returns></returns>
        public void PerformSyncWebSocket4NetEcho(Byte[] bodyBytes, Byte[] respBytes)
        {
            WebSocket4Net.WebSocket ws = new WebSocket4Net.WebSocket(Settings.CompleteWebSocketUri);

            Int32 numRuns = worker_.Rand.Next(10) + 1;

            AutoResetEvent allDataReceivedEvent = new AutoResetEvent(false);

            ws.DataReceived += (s, e) => 
            {
                e.Data.CopyTo(respBytes, 0);

                // Creating response from received byte array.
                Response resp = new Response { BodyBytes = respBytes };

                // Checking the response and generating an error if a problem found.
                if (!CheckResponse(resp))
                {
                    Console.WriteLine("Incorrect WebSocket response of length: " + respBytes.Length);
                    NodeTest.WorkersMonitor.FailTest();
                }

                // Sending data again if number of runs is not exhausted.
                numRuns--;
                if (numRuns <= 0)
                    allDataReceivedEvent.Set();
                else
                    ws.Send(bodyBytes, 0, bodyBytes.Length);
            };

            ws.Opened += (s, e) => 
            {
                ws.Send(bodyBytes, 0, bodyBytes.Length);
            };

            ws.Error += (s, e) =>
            {
                Console.WriteLine(e.Exception.ToString());
                NodeTest.WorkersMonitor.FailTest();
            };

            Boolean closedGracefully = false;

            ws.Closed += (s, e) =>
            {
                if (!closedGracefully)
                    throw new Exception("WebSocket connection was closed unexpectedly!");
            };

            // Starting the handshake.
            ws.Open();

            // Waiting for all tests to finish.
            if (!allDataReceivedEvent.WaitOne(3000))
            {
                throw new Exception("Failed to get WebSocket response in time!");
            }

            // If we are here that means the connection is closed properly.
            closedGracefully = true;

            ws.Close();
        }

        // Sends data, gets the response, and checks its correctness.
        public Boolean PerformTest(Node node)
        {
            if (!async_)
            {
                switch (settings_.ProtocolType)
                {
                    case Settings.ProtocolTypes.ProtocolHttpV1:
                    {
                        if (useNodeX_)
                        {
                            Response resp = X.POST(Settings.CompleteHttpUri, body_bytes_, null);
                            return CheckResponse(resp);
                        }
                        else
                        {
                            Response resp = node.POST(Settings.CompleteHttpUri, body_bytes_, null);
                            return CheckResponse(resp);
                        }
                    }

                    case Settings.ProtocolTypes.ProtocolWebSockets:
                    {
                        Boolean runNativeDotNetWebSockets = false;
                        if (runNativeDotNetWebSockets)
                        {
                            Task t = PerformSyncWebSocketEcho(body_bytes_, resp_bytes_);
                            t.Wait();
                        }
                        else
                        {
                            PerformSyncWebSocket4NetEcho(body_bytes_, resp_bytes_);
                        }

                        return true;
                    }

                    default:
                        throw new ArgumentException("Unknown protocol type!");
                }
            }
            else
            {
                if (useNodeX_)
                {
                    X.POST(Settings.CompleteHttpUri, body_bytes_, null, null, (Response resp, Object userObject) =>
                    {
                        CheckResponse(resp);
                    });
                }
                else
                {
                    node.POST(Settings.CompleteHttpUri, body_bytes_, null, null, (Response resp, Object userObject) =>
                    {
                        CheckResponse(resp);
                    });
                }

                return true;
            }
        }

        // Checks response correctness.
        Boolean CheckResponse(Response resp)
        {
#if FASTEST_POSSIBLE

            NodeTest.WorkersMonitor.IncrementNumFinishedTests();
            return true;
#else

            if (settings_.ConsoleDiag)
                Console.WriteLine(worker_.Id + ": echoed: " + num_echo_bytes_ + " bytes");

            Byte[] resp_body = resp.BodyBytes;
            if (resp_body.Length != num_echo_bytes_)
            {
                Console.WriteLine("Wrong echo size! Correct echo size: " + num_echo_bytes_ + ", wrong: " + resp_body.Length + " [Async=" + async_ + "]");
                Console.WriteLine("Incorrect response: " + resp.Body);
                NodeTest.WorkersMonitor.FailTest();
                return false;
            }

            try
            {
                // Calculating SHA1 hash.
                SHA1 sha1 = new SHA1CryptoServiceProvider();
                Byte[] received_hash_ = sha1.ComputeHash(resp_body);

                // Checking that hash is the same.
                for (Int32 i = 0; i < received_hash_.Length; i++)
                {
                    if (received_hash_[i] != correct_hash_[i])
                    {
                        for (Int32 k = 0; k < resp_body.Length; k++)
                        {
                            if (resp_body[k] != body_bytes_[k])
                            {
                                //Debugger.Launch();
                            }
                        }

                        Console.WriteLine("Wrong echo contents! Correct echo size: " + num_echo_bytes_ + " [Async=" + async_ + "]");
                        NodeTest.WorkersMonitor.FailTest();
                        return false;
                    }
                }

                // Incrementing number of finished tests.
                NodeTest.WorkersMonitor.IncrementNumFinishedTests();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
                throw exc;
            }

            return true;
#endif
        }
    }

    class Worker
    {
        public Int32 Id;

        public Random Rand;

        Settings settings_;

        Node worker_node_;

        public Node WorkerNode
        {
            get { return worker_node_; }
        }

        public void Init(Settings settings, Int32 id)
        {
            Id = id;
            Rand = new Random(id);
            settings_ = settings;

            worker_node_ = new Node(settings_.ServerIp, settings_.ServerPort, 0, settings_.UseAggregation);
        }

        /// <summary>
        /// Initializes specific new test for worker.
        /// </summary>
        /// <returns></returns>
        NodeTestInstance CreateNewTest()
        {
            UInt64 id = ((UInt64)Rand.Next() << 32);
            Int32 num_echo_bytes = Rand.Next(settings_.MinEchoBytes, settings_.MaxEchoBytes);

            NodeTestInstance test = new NodeTestInstance();

            Boolean async = true;
            switch (settings_.AsyncMode)
            {
                case Settings.AsyncModes.ModeSync:
                {
                    async = false;
                    break;
                }

                case Settings.AsyncModes.ModeAsync:
                {
                    async = true;
                    break;
                }

                case Settings.AsyncModes.ModeRandom:
                {
                    if (Rand.Next(0, 2) == 0)
                        async = true;
                    else
                        async = false;

                    break;
                }
            }

            test.Init(settings_, this, id, async, num_echo_bytes);

            return test;
        }

        /// <summary>
        /// Main worker test loop.
        /// </summary>
        public void WorkerLoop()
        {
            Console.WriteLine(Id + ": test started..");

            try
            {
                for (Int32 j = 0; j < settings_.NumEchoesPerWorker; j++)
                {
                    NodeTestInstance test = CreateNewTest();

                    if (!test.PerformTest(worker_node_))
                        return;

                    // Checking if tests has already failed.
                    if (NodeTest.WorkersMonitor.HasTestFailed)
                        return;
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(Id + ": test crashed: " + exc.ToString());

                NodeTest.WorkersMonitor.FailTest();
            }
        }
    }

    class GlobalObserver
    {
        Settings settings_;

        Int64 num_finished_tests_;

        Worker[] workers_;

        public void Init(Settings settings, Worker[] workers)
        {
            settings_ = settings;
            workers_ = workers;
        }

        /// <summary>
        /// Increment global number of finished tests.
        /// </summary>
        public void IncrementNumFinishedTests()
        {
            Interlocked.Increment(ref num_finished_tests_);
        }

        volatile Boolean all_tests_succeeded_ = true;

        /// <summary>
        /// Returns True if tests failed.
        /// </summary>
        public Boolean HasTestFailed
        {
            get { return !all_tests_succeeded_; }
        }

        /// <summary>
        /// Indicate that some test failed.
        /// </summary>
        public void FailTest()
        {
            all_tests_succeeded_ = false;
        }

        /// <summary>
        /// Wait until all tests succeed, fail or timeout.
        /// </summary>
        public Boolean MonitorState()
        {
            Int64 num_ms_passed = 0, num_ms_max = settings_.NumSecondsToWait * 1000;

            Int64 num_tests_all_workers = settings_.NumEchoesPerWorker * settings_.NumWorkers;

            Int32 delay_counter = 0;
            Int64 prev_num_finished_tests = 0;

            // Looping until either tests succeed, fail or timeout.
            while (
                (num_finished_tests_ < num_tests_all_workers) &&
                (num_ms_passed < num_ms_max) &&
                (true == all_tests_succeeded_))
            {
                Thread.Sleep(10);
                num_ms_passed += 10;

                delay_counter++;
                if (delay_counter >= 100)
                {
                    Console.WriteLine("Finished echoes: " + num_finished_tests_ + " out of " + num_tests_all_workers + ", last second: " + (num_finished_tests_ - prev_num_finished_tests));
                    
                    for (Int32 i = 0; i < workers_.Length; i++)
                        Console.WriteLine("Worker " + i + " send-recv balance: " + workers_[i].WorkerNode.SentReceivedBalance);

                    prev_num_finished_tests = num_finished_tests_;
                    delay_counter = 0;
                }
            }

            if (!all_tests_succeeded_)
            {
                Console.Error.WriteLine("Test failed: incorrect echo received.");
                return false;
            }

            if (num_ms_passed >= num_ms_max)
            {
                Console.Error.WriteLine("Test failed: took too long time.");
                FailTest();
                return false;
            }

            return true;
        }
    }

    class NodeTest
    {
        public static GlobalObserver WorkersMonitor = new GlobalObserver();

        static Int32 Main(string[] args)
        {
            //Debugger.Launch();

            Settings settings = new Settings();
            settings.Init(args);

            Console.WriteLine("Node test settings!");
            Console.WriteLine("ServerIp: " + settings.ServerIp);
            Console.WriteLine("ServerPort: " + settings.ServerPort);
            Console.WriteLine("ProtocolType: " + settings.ProtocolType);
            Console.WriteLine("NumWorkers: " + settings.NumWorkers);
            Console.WriteLine("MinEchoBytes: " + settings.MinEchoBytes);
            Console.WriteLine("MaxEchoBytes: " + settings.MaxEchoBytes);
            Console.WriteLine("NumEchoesPerWorker: " + settings.NumEchoesPerWorker);
            Console.WriteLine("NumSecondsToWait: " + settings.NumSecondsToWait);
            Console.WriteLine("AsyncMode: " + settings.AsyncMode);
            Console.WriteLine("UseAggregation: " + settings.UseAggregation);

            // Starting all workers.
            Worker[] workers = new Worker[settings.NumWorkers];
            Thread[] worker_threads = new Thread[settings.NumWorkers];

            WorkersMonitor.Init(settings, workers);

            for (Int32 w = 0; w < settings.NumWorkers; w++)
            {
                Int32 id = w;
                workers[w] = new Worker();
                workers[w].Init(settings, w);

                worker_threads[w] = new Thread(() => { workers[id].WorkerLoop(); });
                worker_threads[w].Start();
            }

            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Waiting for all workers to succeed or fail.
            if (!WorkersMonitor.MonitorState())
                Environment.Exit(1);

            timer.Stop();

            Console.WriteLine("Test succeeded, took ms: " + timer.ElapsedMilliseconds);

            Double echoesPerSecond = ((settings.NumWorkers * settings.NumEchoesPerWorker) * 1000.0) / timer.ElapsedMilliseconds;
            TestLogger.ReportStatistics(
                String.Format("nodetest_{0}_workers_{1}_echo_minbytes_{2}_maxbytes_{3}__echoes_per_second",
                    settings.ProtocolType,
                    settings.NumWorkers,
                    settings.MinEchoBytes,
                    settings.MaxEchoBytes),

                echoesPerSecond);

            Console.WriteLine("Echoes/second: " + echoesPerSecond);

            // Forcing quiting.
            Environment.Exit(0);

            // Waiting for all worker threads to finish.
            for (Int32 w = 0; w < settings.NumWorkers; w++)
                worker_threads[w].Join();

            return 0;
        }
    }
}
