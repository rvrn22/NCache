// Copyright (c) 2015 Alachisoft
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Threading;
using Alachisoft.NCache.Runtime;
using Alachisoft.NCache.Web.Caching;
using Factory = Alachisoft.NCache.Web.Caching.NCache;


namespace Alachisoft.NCache.Tools.StressTestTool
{
    /// <summary>
    ///     ThreadTest class, that contains all the logic used to instantiate multiple threads
    ///     on the basis of given parameters.
    /// </summary>
    internal sealed class ThreadTest
    {
        private readonly string _cacheId = "";
        private readonly int _dataSize = 1024;
        private readonly int _expiration = 300;
        private readonly int _getsPerIteration = 1;
        private readonly int _reportingInterval = 5000;
        private readonly int _testCaseIterationDelay;
        private readonly int _testCaseIterations = 10;
        private readonly int _threadCount = 1;
        private readonly int _totalLoopCount;
        private readonly int _updatesPerIteration = 1;

        /// <summary>
        ///     Overriden constructor that uses all user supplied parameters
        /// </summary>
        public ThreadTest(string cacheId, int totalLoopCount, int testCaseIterations, int testCaseIterationDelay, int getsPerIteration, int updatesPerIteration, int dataSize, int expiration, int threadCount, int reportingInterval, bool noLogo)
        {
            _cacheId = cacheId;
            _totalLoopCount = totalLoopCount;
            _testCaseIterations = testCaseIterations;
            _testCaseIterationDelay = testCaseIterationDelay;
            _getsPerIteration = getsPerIteration;
            _updatesPerIteration = updatesPerIteration;
            _dataSize = dataSize;
            _expiration = expiration;
            _threadCount = threadCount;
            _reportingInterval = reportingInterval;
        }

        /// <summary>
        ///     Main test starting point. This method instantiate multiple threads and keeps track of
        ///     all of them.
        /// </summary>
        public void Test()
        {
            try
            {
                var threads = new Thread[_threadCount];

                var cache = Factory.InitializeCache(_cacheId);
                cache.ExceptionsEnabled = true;

                var pid = Process.GetCurrentProcess().Id.ToString();

                for (var threadIndex = 0; threadIndex < _threadCount; threadIndex++)
                {
                    var tc = new ThreadContainer(cache, _totalLoopCount, _testCaseIterations, _testCaseIterationDelay, _getsPerIteration, _updatesPerIteration, _dataSize, _expiration, _threadCount, _reportingInterval, threadIndex);
                    ThreadStart threadDelegate = tc.DoTest;
                    threads[threadIndex] = new Thread(threadDelegate) {Name = "ThreadIndex: " + threadIndex};
                    threads[threadIndex].Start();
                }

                //--- wait on threads to complete their work before finishing
                foreach (var t in threads)
                {
                    t.Join();
                }

                cache.Dispose();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error :- " + e.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(e.ToString());
            }
        }
    }

    /// <summary>
    ///     ThreadContainer class.
    ///     This class is being used to be run under each thread.
    /// </summary>
    internal class ThreadContainer
    {
        private readonly Cache _cache;
        private readonly int _dataSize = 1024;
        private readonly int _expiration = 300;
        private readonly int _getsPerIteration = 1;
        private int _pid;
        private readonly int _reportingInterval = 5000;
        private readonly int _testCaseIterationDelay;
        private readonly int _testCaseIterations = 10;
        private int _threadCount = 1;
        private int _threadIndex;
        private readonly int _totalLoopCount;
        private readonly int _updatesPerIteration = 1;
        private readonly int maxErrors = 1000;

        private int numErrors;

        /// <summary>
        ///     Constructor
        /// </summary>
        public ThreadContainer(Cache cache, int totalLoopCount, int testCaseIterations, int testCaseIterationDelay, int getsPerIteration, int updatesPerIteration, int dataSize, int expiration, int threadCount, int reportingInterval, int threadIndex)
        {
            _cache = cache;
            _totalLoopCount = totalLoopCount;
            _testCaseIterations = testCaseIterations;
            _testCaseIterationDelay = testCaseIterationDelay;
            _getsPerIteration = getsPerIteration;
            _updatesPerIteration = updatesPerIteration;
            _dataSize = dataSize;
            _expiration = expiration;
            _threadCount = threadCount;
            _reportingInterval = reportingInterval;
            _threadIndex = threadIndex;

            _pid = Process.GetCurrentProcess().Id;
        }

        /// <summary>
        ///     Test starting call
        /// </summary>
        public void DoTest()
        {
            DoGetInsert();
        }

        /// <summary>
        ///     Perform Get/Insert operations on cache, bsed on user given input.
        /// </summary>
        private void DoGetInsert()
        {
            var data = new byte[_dataSize];

            if (_totalLoopCount <= 0)
            {
                // this means an infinite loop. user will have to do Ctrl-C to stop the program
                for (long totalIndex = 0;; totalIndex++)
                {
                    ProcessGetInsertIteration(data);
                    if (totalIndex >= _reportingInterval)
                    {
                        var count = _cache.Count;
                        Console.WriteLine(DateTime.Now + ": Cache count: " + count);
                        totalIndex = 1;
                    }
                }
            }
            for (long totalIndex = 0; totalIndex < _totalLoopCount; totalIndex++)
            {
                ProcessGetInsertIteration(data);
                if (totalIndex >= _reportingInterval)
                {
                    var count = _cache.Count;
                    Console.WriteLine(DateTime.Now + ": Cache count: " + count);
                }
            }
        }

        /// <summary>
        ///     Perform Get/Insert task on cache.
        ///     Called by DoGetInsert method
        /// </summary>
        private void ProcessGetInsertIteration(byte[] data)
        {
            var guid = Guid.NewGuid().ToString(); //create a unique key to be inserted in store.

            for (long testCaseIndex = 0; testCaseIndex < _testCaseIterations; testCaseIndex++)
            {
                var key = guid;

                for (var getsIndex = 0; getsIndex < _getsPerIteration; getsIndex++)
                {
                    try
                    {
                        var obj = _cache.Get(key);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("GET Error: Key: " + key + ", Exception: " + e + "\n");
                        numErrors++;
                        if (numErrors > maxErrors)
                        {
                            Console.Error.WriteLine("Too many errors. Press any key to exit ...");
                            Console.ReadKey(true);
                            Environment.Exit(0);
                            //This causes the programe to crash
                            //throw e;
                        }
                    }
                }

                for (var updatesIndex = 0; updatesIndex < _updatesPerIteration; updatesIndex++)
                {
                    try
                    {
                        _cache.Insert(key, data,
                            Cache.NoAbsoluteExpiration,
                            new TimeSpan(0, 0, 0, _expiration),
                            CacheItemPriority.Default);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("INSERT Error: Key: " + key + ", Exception: " + e + "\n");
                        numErrors++;
                        if (numErrors > maxErrors)
                        {
                            Console.Error.WriteLine("Too many errors. Press any key to exit ...");
                            Console.ReadKey(true);
                            Environment.Exit(0);
                            //This causes the programe to crash
                            //throw e;
                        }
                    }
                }

                if (_testCaseIterationDelay > 0)
                {
                    // Sleep for this many seconds
                    Thread.Sleep(_testCaseIterationDelay * 1000);
                }
            }
        }
        //}
        //    return Environment.MachineName + "key:" + Thread.CurrentThread.Name + ":" + index;
        //{
        //private string GetKey(long index)
        /// </summary>
        /// Although not been called by any method.
        /// Creates and returns a unique key on the basis of thread index 

        /// <summary>
    }
}