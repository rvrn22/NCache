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
using Alachisoft.NCache.Tools.Common;

namespace Alachisoft.NCache.Tools.StressTestTool
{
    /// <summary>
    ///     Main application class
    /// </summary>
    internal class Application
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                StressTestTool.Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    /// <summary>
    ///     Summary description for StressTool.
    /// </summary>
    public class StressTestToolParam : CommandLineParamsBase
    {
        [Argument("", "")]
        public string CacheId { get; set; } = "";

        [Argument(@"/m", @"/item-size")]
        public int DataSize { get; set; } = 1024;

        [Argument(@"/e", @"/sliding-expiration")]
        public int Expiration { get; set; } = 60;

        [Argument(@"/g", @"/gets-per-iteration")]
        public int GetsPerIteration { get; set; } = 1;

        [Argument(@"/r", @"/reporting-interval")]
        public int ReportingInterval { get; set; } = 5000;

        [Argument(@"/d", @"/test-case-iteration-delay")]
        public int TestCaseIterationDelay { get; set; } = 0;

        [Argument(@"/i", @"/test-case-iterations")]
        public int TestCaseIterations { get; set; } = 20;

        [Argument(@"/t", @"/thread-count")]
        public int ThreadCount { get; set; } = 1;

        [Argument(@"/n", @"/item-count")]
        public int TotalLoopCount { get; set; } = 0;

        [Argument(@"/u", @"/updates-per-iteration")]
        public int UpdatesPerIteration { get; set; } = 1;
    }

    internal sealed class StressTestTool
    {
        private static StressTestToolParam cParam = new StressTestToolParam();

        /// <summary>
        ///     The main entry point for the tool.
        /// </summary>
        public static void Run(string[] args)
        {
            try
            {
                object param = new StressTestToolParam();
                CommandLineArgumentParser.CommandLineParser(ref param, args);
                cParam = (StressTestToolParam) param;
                if (cParam.IsUsage)
                {
                    AssemblyUsage.PrintLogo(cParam.IsLogo);
                    AssemblyUsage.PrintUsage();
                    return;
                }

                if (!ApplyParameters(args))
                {
                    return;
                }
                //if (!ValidateParameters()) return;


                Console.WriteLine("cacheId = {0}, total-loop-count = {1}, test-case-iterations = {2}, testCaseIterationDelay = {3}, gets-per-iteration = {4}, updates-per-iteration = {5}, data-size = {6}, expiration = {7}, thread-count = {8}, reporting-interval = {9}.", cParam.CacheId, cParam.TotalLoopCount, cParam.TestCaseIterations, cParam.TestCaseIterationDelay, cParam.GetsPerIteration, cParam.UpdatesPerIteration, cParam.DataSize, cParam.Expiration, cParam.ThreadCount, cParam.ReportingInterval);
                Console.WriteLine("-------------------------------------------------------------------\n");
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;

                var threadTest = new ThreadTest(cParam.CacheId, cParam.TotalLoopCount, cParam.TestCaseIterations, cParam.TestCaseIterationDelay, cParam.GetsPerIteration, cParam.UpdatesPerIteration, cParam.DataSize, cParam.Expiration, cParam.ThreadCount, cParam.ReportingInterval, cParam.IsLogo);
                threadTest.Test();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(e.ToString());
            }
        }

        /// <summary>
        ///     Sets the application level parameters to those specified at the command line.
        /// </summary>
        /// <param name="args">array of command line parameters</param>
        private static bool ApplyParameters(string[] args)
        {
            try
            {
                if (cParam.CacheId == string.Empty || cParam.CacheId == null)
                {
                    Console.Error.WriteLine("Error: cache name not specified");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while parsing input parameters. Please verify all given parameters are in correct format.");
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                return false;
            }
            AssemblyUsage.PrintLogo(cParam.IsLogo);
            return true;
        }
    }
}