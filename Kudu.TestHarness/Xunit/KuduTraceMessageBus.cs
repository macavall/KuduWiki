﻿using System;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public class KuduTraceMessageBus : IMessageBus
    {
        private readonly IMessageBus _innerBus;

        public bool TestSkipped { get; private set; }

        public KuduTraceMessageBus(IMessageBus innerBus)
        {
            _innerBus = innerBus;
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            var result = message as TestResultMessage;
            if (result != null && String.IsNullOrEmpty(result.Output))
            {
                result.SetOutput(TestTracer.GetTraceString());
            }

            var testFailed = result as TestFailed;
            if (testFailed != null && testFailed.ExceptionTypes.LastOrDefault() == typeof(KuduXunitTestSkippedException).FullName)
            {
                TestSkipped = true;
                message = new TestSkipped(result.Test, testFailed.Messages.LastOrDefault() ?? "unknown");
            }

            return _innerBus.QueueMessage(message.SanitizeXml());
        }

        public void Dispose()
        {
            _innerBus.Dispose();
        }
    }
}
