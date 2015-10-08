﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public static class KuduXunitTestRunnerUtils
    {
        public const int MaxParallelThreads = 4;

        public static Task<RunSummary> RunTestAsync(XunitTestRunner runner,
                                                    IMessageBus messageBus,
                                                    ExceptionAggregator aggregator,
                                                    bool disableRetry)
        {
            // fork non-SynchronizationContext thread
            var result = Task.Factory.StartNew(
                            () => RunTestAsyncCore(runner, messageBus, aggregator, disableRetry).Result,
                            new CancellationToken(),
                            TaskCreationOptions.None,
                            TaskScheduler.Default).Result;
            return Task.FromResult(result);
        }

        private static async Task<RunSummary> RunTestAsyncCore(XunitTestRunner runner,
                                                               IMessageBus messageBus,
                                                               ExceptionAggregator aggregator,
                                                               bool disableRetry)
        {
            try
            {
                DelayedMessageBus delayedMessageBus = null;
                RunSummary summary = null;

                // First run
                if (!disableRetry)
                {
                    // This is really the only tricky bit: we need to capture and delay messages (since those will
                    // contain run status) until we know we've decided to accept the final result;
                    delayedMessageBus = new DelayedMessageBus(messageBus);

                    runner.SetMessageBus(delayedMessageBus);
                    summary = await RunTestInternalAsync(runner);

                    // if succeeded
                    if (summary.Failed == 0 || aggregator.HasExceptions)
                    {
                        delayedMessageBus.Flush(false);
                        return summary;
                    }
                }

                // Final run
                runner.SetMessageBus(new KuduTraceMessageBus(messageBus));
                summary = await RunTestInternalAsync(runner);

                // flush delay messages
                if (delayedMessageBus != null)
                {
                    delayedMessageBus.Flush(summary.Failed == 0 && !aggregator.HasExceptions);
                }

                return summary;
            }
            catch (Exception ex)
            {
                // this is catastrophic
                messageBus.QueueMessage(new TestFailed(runner.GetTest(), 0, null, ex));

                return new RunSummary { Failed = 1, Total = 1 };
            }
            finally
            {
                // set to original
                runner.SetMessageBus(messageBus);
            }
        }

        private static async Task<RunSummary> RunTestInternalAsync(XunitTestRunner runner)
        {
            TestContext.InitializeContext(runner.GetTest());
            try
            {
                return await runner.RunAsync();
            }
            finally
            {
                TestContext.FreeContext();

                // reset FileSystem mockup
                FileSystemHelpers.Instance = null;
            }
        }

        // making sure all texts are Xml valid
        public static IMessageSinkMessage SanitizeXml(this IMessageSinkMessage message)
        {
            var failed = message as TestFailed;
            if (failed != null)
            {
                return new TestFailed(failed.Test,
                                      failed.ExecutionTime,
                                      XmlUtility.Sanitize(failed.Output),
                                      failed.ExceptionTypes,
                                      failed.Messages == null ? null : failed.Messages.Select(m => XmlUtility.Sanitize(m)).ToArray(),
                                      failed.StackTraces,
                                      failed.ExceptionParentIndices);
            }

            var skipped = message as TestSkipped;
            if (skipped != null)
            {
                skipped = new TestSkipped(skipped.Test, XmlUtility.Sanitize(skipped.Reason));
                skipped.SetOutput(XmlUtility.Sanitize(skipped.Output));
                return skipped;
            }

            var passed = message as TestPassed;
            if (passed != null)
            {
                return new TestPassed(passed.Test, passed.ExecutionTime, XmlUtility.Sanitize(passed.Output));
            }

            return message;
        }

        public class DelayedMessageBus : IMessageBus
        {
            private readonly IMessageBus _innerBus;
            private readonly List<IMessageSinkMessage> _messages;

            public DelayedMessageBus(IMessageBus innerBus)
            {
                _innerBus = innerBus;
                _messages = new List<IMessageSinkMessage>();
            }

            public bool QueueMessage(IMessageSinkMessage message)
            {
                var result = message as TestResultMessage;
                if (result != null && String.IsNullOrEmpty(result.Output))
                {
                    result.SetOutput(TestTracer.GetTraceString());
                }

                lock (_messages)
                {
                    _messages.Add(message);
                }

                // No way to ask the inner bus if they want to cancel without sending them the message, so
                // we just go ahead and continue always.
                return true;
            }

            public void Dispose()
            {
            }

            public void Flush(bool retrySucceeded)
            {
                foreach (var message in _messages)
                {
                    // in case of retry succeeded, convert all failure to skip (ignored)
                    if (retrySucceeded && message is TestFailed)
                    {
                        var failed = (TestFailed)message;
                        var reason = new StringBuilder();
                        reason.AppendLine(String.Join(Environment.NewLine, failed.ExceptionTypes));
                        reason.AppendLine(String.Join(Environment.NewLine, failed.Messages));
                        reason.AppendLine(String.Join(Environment.NewLine, failed.StackTraces));

                        // xunit does not report output if skipped (ignored) tests.
                        // put it as reason instead.
                        reason.AppendLine("====================================================================================");
                        reason.AppendLine(failed.Output);

                        var skipped = new TestSkipped(failed.Test, reason.ToString());
                        _innerBus.QueueMessage(skipped.SanitizeXml());
                    }
                    else
                    {
                        _innerBus.QueueMessage(message.SanitizeXml());
                    }
                }
            }
        }
    }
}
