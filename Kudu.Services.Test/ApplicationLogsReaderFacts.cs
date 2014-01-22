﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Diagnostics;
using Kudu.Core;
using Kudu.Services.Diagnostics;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{    
    public class ApplicationLogsReaderFacts
    {
        [Fact]
        public void ApplicationLogsReaderStartsWithMostRecentlyWrittenFile()
        {
            var fs = new MockFileSystem();

            fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is the most recent log",
            DateTimeOffset.Parse("2013-12-06T00:29:22+00:00")
);
            fs.AddLogFile("log-2.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log",
            DateTimeOffset.Parse("2013-12-06T00:29:20+00:00")
);

            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.RootPath).Returns("");
            var reader = new ApplicationLogsReader(fs, environmentMock.Object);

            var results = reader.GetRecentLogs(1).ToList();

            Assert.Equal(1, results.Count);
            results[0].AssertLogEntry("2013-12-06T00:29:20+00:00", "Information", "this is the most recent log");
        }

        [Fact]
        public void ApplicationLogsReaderMergesResultsFromMultipleLogFiles()
        {
            var fs = new MockFileSystem();

            fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
);
            fs.AddLogFile("log-2.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
);

            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.RootPath).Returns("");
            var reader = new ApplicationLogsReader(fs, environmentMock.Object);

            var results = reader.GetRecentLogs(6).ToList();

            Assert.Equal(6, results.Count);
            results[0].AssertLogEntry("2013-12-06T00:29:22+00:00", "Error", "this is an error");
            results[1].AssertLogEntry("2013-12-06T00:29:22+00:00", "Error", "this is an error");
            results[2].AssertLogEntry("2013-12-06T00:29:21+00:00", "Warning", "this is a warning");
            results[3].AssertLogEntry("2013-12-06T00:29:21+00:00", "Warning", "this is a warning");
            results[4].AssertLogEntry("2013-12-06T00:29:20+00:00", "Information", "this is a log");
            results[5].AssertLogEntry("2013-12-06T00:29:20+00:00", "Information", "this is a log");
        }

        [Fact]
        public void ApplicationLogsReaderMissingLogMessageIsValidEntry()
        {
            var fs = new MockFileSystem();

            fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:21  PID[20108] Warning 
2013-12-06T00:29:22  PID[20108] Error       this is an error"
);

            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.RootPath).Returns("");
            var reader = new ApplicationLogsReader(fs, environmentMock.Object);

            var results = reader.GetRecentLogs(6).ToList();
            
            Assert.Equal(4, results.Count);
            results[1].AssertLogEntry("2013-12-06T00:29:21+00:00", "Warning", "");
        }

        [Fact]
        public void ApplicationLogsReaderIncompleteLogEntryIsTreatedAsContentsOfLastValidEntry()
        {
            // This is really just a variation on the multiline tests but is here to demonstrate that any 'junk' lines 
            // will just be treated as part of the preceeding log message

            var fs = new MockFileSystem();

            fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:21  PID[20108]
2013-12-06T00:29:22  PID[20108] Error       this is an error"
);
            
            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.RootPath).Returns("");
            var reader = new ApplicationLogsReader(fs, environmentMock.Object);

            var results = reader.GetRecentLogs(6).ToList();            
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public void ApplicationLogsReaderReturnsTheCorrectResultsWhenAccessedByMultipleThreads()
        {
            var fs = new FileSystem();
            using (var dir = new TemporaryApplicationLogDirectory(fs))
            {
                dir.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
                );

                dir.AddLogFile("log-2.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
                );

                dir.AddLogFile("log-3.txt", @"2014-01-09T00:18:30 NOT A VALID LOG FILE");

                dir.AddLogFile("abc-123-logging-errors.txt",
@"2014-01-09T00:18:30
System.ApplicationException: The trace listener AzureTableTraceListener is disabled. 
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.GetCloudTableClientFromEnvironment()
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.RefreshConfig()
   --- End of inner exception stack trace ---"
                );

                dir.AddLogFile("log-4.txt",
@"2013-12-06T00:29:23  PID[20108] Information this is a log
2013-12-06T00:29:24  PID[20108] Information this is a log
2013-12-06T00:29:25  PID[20108] Information this is a log
2013-12-06T00:29:26  PID[20108] Information this is a log
2013-12-06T00:29:27  PID[20108] Information this is a log
2013-12-06T00:29:28  PID[20108] Information this is a log
2013-12-06T00:29:29  PID[20108] Warning     this is a warning
2013-12-06T00:29:30  PID[20108] Error       this is an error"
                );

                var environmentMock = new Mock<IEnvironment>();
                environmentMock.Setup(e => e.RootPath).Returns(dir.RootDir);
                var reader = new ApplicationLogsReader(fs, environmentMock.Object);
                       
                var loopResult = Parallel.For(0, 10, (i) =>
                {
                    var results = reader.GetRecentLogs(20).ToList();
                    Assert.Equal(14, results.Count);
                });

                Assert.True(loopResult.IsCompleted);                
            }    
        }

        [Fact]
        public void ApplicationLogsReaderWillNotOpenUnlimitedNumberOfFiles()
        {                        
            // This test would preferrably use a mocked file system but the System.IO.Abstractions.TestHelpers have
            // some issues with returning the correct LastWrittenUtc and so this needs to run against a real file system for now.
            var fs = new FileSystem();            
            using (var dir = new TemporaryApplicationLogDirectory(fs))
            {
                var logFileCount = 100;
                var timestamp = DateTimeOffset.UtcNow;
                for (int i = 0; i < logFileCount; i++)
                {
                    timestamp = timestamp.AddSeconds(1);
                    dir.AddLogFile(
                        string.Format("log-{0}.txt", i),
                        string.Format("{0:s}  PID[20108] Information this is a log\r\n", timestamp)
                    );
                }

                var environmentMock = new Mock<IEnvironment>();
                environmentMock.Setup(e => e.RootPath).Returns(dir.RootDir);
                var reader = new ApplicationLogsReader(fs, environmentMock.Object);

                var results = reader.GetRecentLogs(logFileCount).ToList();

                // In this degenerate case with a large number of log files with only one line each, the limit
                // on the number of log files that can be read will be hit and so not all 100 log entries will
                // be returned.
                Assert.Equal(ApplicationLogsReader.FileOpenLimit, results.Count);
                results[0].AssertLogEntry(timestamp.ToString("s"), "Information", "this is a log");
            }
        }

    }

    public class ResumableLogReaderFacts
    {
        [Fact]
        public void ResumableLogReaderLogEntriesAreReturnedInReverseOrder()
        {
            var fs = new MockFileSystem();
            var logFile = fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
            );

            using (var reader = new ApplicationLogsReader.ResumableLogFileReader(logFile))
            {
                var entry1 = reader.ReadNextBatch(1).Single();
                var entry2 = reader.ReadNextBatch(1).Single();
                var entry3 = reader.ReadNextBatch(1).Single();

                entry1.AssertLogEntry("2013-12-06T00:29:22+00:00", "Error", "this is an error");
                entry2.AssertLogEntry("2013-12-06T00:29:21+00:00", "Warning", "this is a warning");
                entry3.AssertLogEntry("2013-12-06T00:29:20+00:00", "Information", "this is a log");                                
            }
        }

        [Fact]
        public void ResumableLogReaderSeveralSmallBatchesAreEquivalentToOneLargeBatchInTheCaseOfSingleLogFile()
        {
            var fs = new MockFileSystem();
            var logFile = fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
            );

            using (var reader = new ApplicationLogsReader.ResumableLogFileReader(logFile))
            using (var reader2 = new ApplicationLogsReader.ResumableLogFileReader(logFile))
            {
                var result1 = reader.ReadNextBatch(3).ToList();
                var result2 = reader2.ReadNextBatch(1)
                    .Concat(reader2.ReadNextBatch(1))
                    .Concat(reader2.ReadNextBatch(1))
                    .ToList();

                Assert.Equal(reader.LastTime, reader2.LastTime);
                Assert.Equal(3, result1.Count);
                Assert.Equal(3, result2.Count);
                Assert.Equal(result1[0].TimeStamp, result2[0].TimeStamp);
                Assert.Equal(result1[1].TimeStamp, result2[1].TimeStamp);
                Assert.Equal(result1[2].TimeStamp, result2[2].TimeStamp);
            }
        }


        [Fact]
        public void ResumableLogReaderLastTimeIsSetToTimeOfLastReadLogEntry()
        {
            var fs = new MockFileSystem();
            var logFile = fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
            );

            using(var reader = new ApplicationLogsReader.ResumableLogFileReader(logFile))
            {
                reader.ReadNextBatch(2);
                Assert.Equal(DateTimeOffset.Parse("2013-12-06T00:29:21+00:00"), reader.LastTime);
            }            
        }

        [Fact]
        public void ResumableLogReaderMultilineMessagesAreMergedIntoOneEntry()
        {
            var fs = new MockFileSystem();
            var logFile = fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
that spans
several lines
2013-12-06T00:29:22  PID[20108] Error       this is an error"
            );

            using (var reader = new ApplicationLogsReader.ResumableLogFileReader(logFile))
            {
                var results = reader.ReadNextBatch(3);
                Assert.Equal(3, results.Count);
                results[1].AssertLogEntry("2013-12-06T00:29:21+00:00", "Warning", "this is a warning\r\nthat spans\r\nseveral lines");                
            }                 
        }

        [Fact]
        public void ResumableLogReaderWillNotEnumerateEntireFile()
        {
            using (var reader = new ApplicationLogsReader.ResumableLogFileReader(DateTimeOffset.UtcNow, InfiniteLines))
            {
                var results = reader.ReadNextBatch(100);
                Assert.Equal(100, results.Count);                
            }      
        }

        private IEnumerable<string> InfiniteLines
        {
            get
            {
                var timestamp = DateTimeOffset.UtcNow;
                while (true)
                {
                    timestamp = timestamp.AddSeconds(1);
                    yield return string.Format("{0:s}  PID[20108] Information this is a log\r\n", timestamp);
                }
            }
        }
    }

    public class LogFileFinderFacts
    {
        [Fact]
        public void LogFileFinderCanHandleDirectoryDoesNotExist()
        {
            MockFileSystem fs = new MockFileSystem();            
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath);
            var results = fileFinder.FindLogFiles().ToList();
            Assert.Equal(0, results.Count);
        }
        
        [Fact]
        public void LogFileFinderNoLogFilesFoundForEmptyDirectory()
        {
            MockFileSystem fs = new MockFileSystem();
            fs.AddDirectory(Constants.ApplicationLogFilesPath);            
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath);
            var results = fileFinder.FindLogFiles().ToList();
            Assert.Equal(0, results.Count);
        }

        [Fact]
        public void LogFileFinderFindsSingleLogFile()
        {
            var fs = new MockFileSystem();            
            fs.AddLogFile("log-1.txt",            
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"                         
            );
            
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath);
            var results = fileFinder.FindLogFiles().ToList();

            Assert.Equal(1, results.Count);
            Assert.Equal(1, fileFinder.IncludedFiles.Count);            
        }

        [Fact]
        public void LogFileFinderIgnoresFilesNotInStandardLogFormat()
        {
            var fs = new MockFileSystem();            
            fs.AddLogFile("logging-errors.txt",
@"2014-01-09T00:18:30
System.ApplicationException: The trace listener AzureTableTraceListener is disabled. 
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.GetCloudTableClientFromEnvironment()
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.RefreshConfig()
   --- End of inner exception stack trace ---"         
            );
            
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath);
            var results = fileFinder.FindLogFiles().ToList();

            Assert.Equal(0, results.Count);
            Assert.Equal(1, fileFinder.ExcludedFiles.Count);
        }

        [Fact]
        public void LogFileFinderIgnoredFilesAreOnlyReadOnce()
        {
            var fs = new MockFileSystem();
            fs.AddLogFile("logging-errors.txt",
@"2014-01-09T00:18:30
System.ApplicationException: The trace listener AzureTableTraceListener is disabled. 
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.GetCloudTableClientFromEnvironment()
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.RefreshConfig()
   --- End of inner exception stack trace ---"
            );

            var stats = new ApplicationLogsReader.LogFileAccessStats();     
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath, stats);
            var results = fileFinder.FindLogFiles().ToList();

            Assert.Equal(0, results.Count);
            Assert.Equal(1, stats.GetOpenTextCount("logging-errors.txt"));

            results = fileFinder.FindLogFiles().ToList();

            Assert.Equal(0, results.Count);
            Assert.Equal(1, stats.GetOpenTextCount("logging-errors.txt"));
        }

        [Fact]
        public void LogFileFinderValidFilesAreOnlyReadOnce()
        {
            var fs = new MockFileSystem();
            fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
            );

            var stats = new ApplicationLogsReader.LogFileAccessStats();  
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath, stats);
            
            fileFinder.FindLogFiles();

            fs.AddLogFile("log-2.txt", @"2013-12-06T00:29:20  PID[20108] Information this is a log");
            fileFinder.FindLogFiles();

            fs.AddLogFile("log-3.txt", @"2013-12-06T00:29:20  PID[20108] Information this is a log");
            var results = fileFinder.FindLogFiles().ToList();

            Assert.Equal(3, results.Count);
            Assert.Equal(1, stats.GetOpenTextCount("log-1.txt"));
            Assert.Equal(1, stats.GetOpenTextCount("log-2.txt"));
            Assert.Equal(1, stats.GetOpenTextCount("log-3.txt"));  
        }

        [Fact]
        public void LogFileFinderFilesAreNotTrackedOnceDeleted()
        {
            var fs = new MockFileSystem();
            fs.AddLogFile("log-1.txt",
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
2013-12-06T00:29:22  PID[20108] Error       this is an error"
            );
            fs.AddLogFile("log-2.txt",
@"2014-01-09T00:18:30
System.ApplicationException: The trace listener AzureTableTraceListener is disabled. 
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.GetCloudTableClientFromEnvironment()
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.RefreshConfig()
   --- End of inner exception stack trace ---"
            );

            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath);

            fileFinder.FindLogFiles();            
            Assert.Equal(1, fileFinder.IncludedFiles.Count);
            Assert.Equal(1, fileFinder.ExcludedFiles.Count);

            fs.RemoveLogFile("log-1.txt");
            fs.RemoveLogFile("log-2.txt");

            fileFinder.FindLogFiles();
            Assert.Equal(0, fileFinder.IncludedFiles.Count);
            Assert.Equal(0, fileFinder.ExcludedFiles.Count);
        }

        [Fact]
        public void LogFileFinderEmptyFilesAreNotReturned()
        {
            var fs = new MockFileSystem();
            fs.AddLogFile("log-1.txt", "");
        
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath);
            var results = fileFinder.FindLogFiles();

            Assert.Equal(0, results.Count());
            Assert.Equal(0, fileFinder.IncludedFiles.Count);
            Assert.Equal(0, fileFinder.ExcludedFiles.Count); 
        }

        [Fact]
        public void LogFileFinderEmptyFilesAreReadEachTime()
        {
            var fs = new MockFileSystem();
            fs.AddLogFile("log-1.txt", "");

            var stats = new ApplicationLogsReader.LogFileAccessStats();              
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath, stats);

            fileFinder.FindLogFiles();            
            fileFinder.FindLogFiles();            
            Assert.Equal(2, stats.GetOpenTextCount("log-1.txt"));                       
        }

        [Fact]
        public void LogFileFinderLoggingErrorFilesAreSkippedWithoutReading()
        {
            var fs = new MockFileSystem();
            fs.AddLogFile("abc-123-logging-errors.txt",
@"2014-01-09T00:18:30
System.ApplicationException: The trace listener AzureTableTraceListener is disabled. 
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.GetCloudTableClientFromEnvironment()
   at Microsoft.WindowsAzure.WebSites.Diagnostics.AzureTableTraceListener.RefreshConfig()
   --- End of inner exception stack trace ---"
            );

            var stats = new ApplicationLogsReader.LogFileAccessStats();
            var fileFinder = new ApplicationLogsReader.LogFileFinder(fs, Constants.ApplicationLogFilesPath, stats);

            var results = fileFinder.FindLogFiles();
            Assert.Equal(0, stats.GetOpenTextCount("abc-123-logging-errors.txt"));
            Assert.Equal(0, stats.GetOpenReadCount("abc-123-logging-errors.txt"));
        }

    }

    internal class TemporaryApplicationLogDirectory : IDisposable
    {
        private IFileSystem _fs;
        public string RootDir { get; private set; }
        public string LogDir { get; private set; }

        public TemporaryApplicationLogDirectory(IFileSystem fs)
        {
            _fs = fs;

            RootDir = _fs.Path.GetTempFileName();
            _fs.File.Delete(RootDir);

            LogDir = Path.Combine(RootDir, Constants.ApplicationLogFilesPath);
            _fs.Directory.CreateDirectory(LogDir);
        }

        public FileInfoBase AddLogFile(string name, string contents)
        {
            var path = Path.Combine(LogDir, name);
            _fs.File.WriteAllText(path, contents);
            return _fs.FileInfo.FromFileName(path);
        }

        public void Dispose()
        {
            _fs.Directory.Delete(RootDir, true);
        }
    }

    internal static class LogFileTestExtensions
    {
        public static FileInfoBase AddLogFile(this MockFileSystem fs, string name, string contents)
        {
            return AddLogFile(fs, name, contents, DateTimeOffset.UtcNow);
        }

        public static FileInfoBase AddLogFile(this MockFileSystem fs, string name, string contents, DateTimeOffset lastWriteTime)
        {
            var path = Path.Combine(Constants.ApplicationLogFilesPath, name);
            fs.AddFile(path, new MockFileData(contents) { LastWriteTime = lastWriteTime });
            return fs.FileInfo.FromFileName(path);
        }

        public static void RemoveLogFile(this MockFileSystem fs, string name)
        {
            var path = Path.Combine(Constants.ApplicationLogFilesPath, name);
            fs.RemoveFile(path);
        }

        public static void AssertLogEntry(this ApplicationLogEntry entry, string timeStamp, string level, string message)
        {
            AssertLogEntry(entry, DateTimeOffset.Parse(timeStamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), level, message);            
        }

        public static void AssertLogEntry(this ApplicationLogEntry entry, DateTimeOffset timeStamp, string level, string message)
        {
            Assert.Equal(timeStamp, entry.TimeStamp);
            Assert.Equal(level, entry.Level);
            Assert.Equal(message, entry.Message);
        }
    }    

}
