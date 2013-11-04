using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Core.Infrastructure
{
    internal static class OperationManager
    {
        private const int DefaultRetries = 3;
        private const int DefaultDelayBeforeRetry = 250; // 250 ms

        public static void Attempt(Action action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            OperationManager.Attempt<object>(() =>
            {
                action();
                return null;
            }, retries, delayBeforeRetry);
        }

        public static T Attempt<T>(Func<T> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry, Func<Exception, bool> shouldRetry = null)
        {
            T result = default(T);

            while (retries > 0)
            {
                try
                {
                    result = action();
                    break;
                }
                catch (Exception ex)
                {
                    if (shouldRetry != null && !shouldRetry(ex))
                    {
                        throw;
                    }

                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }

                Thread.Sleep(delayBeforeRetry);
            }

            return result;
        }

        public static Task AttemptAsync(Func<Task> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            return AttemptAsync(async () =>
            {
                await action();
                return true;
            }, retries, delayBeforeRetry);
        }

        public static async Task<TVal> AttemptAsync<TVal>(Func<Task<TVal>> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            TVal result = default(TVal);

            while (retries > 0)
            {
                try
                {
                    result = await action();
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }

                await Task.Delay(delayBeforeRetry);
            }

            return result;
        }
    }
}
