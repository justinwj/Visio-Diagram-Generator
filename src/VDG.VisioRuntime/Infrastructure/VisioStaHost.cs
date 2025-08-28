using System;
using System.Collections.Concurrent;
using System.Threading;
using VDG.VisioRuntime.Services;

namespace VDG.VisioRuntime.Infrastructure
{
    /// <summary>
    /// Hosts a single‑threaded apartment (STA) for all Visio COM calls.
    /// COM objects must only be used on the thread that created them.
    /// This host spins up a background STA thread and processes queued
    /// actions.  Callers can synchronously invoke work on the STA via
    /// <see cref="Invoke(Action{IVisioService})"/> or <see cref="Invoke{T}(Func{IVisioService, T})"/>.
    /// </summary>
    public sealed class VisioStaHost : IDisposable
    {
        private readonly BlockingCollection<JobBase> _queue = new();
        private readonly Thread _thread;

        /// <summary>
        /// Construct the host.  A new thread is created in STA mode and
        /// initialises the <see cref="VisioService"/>.  All work is
        /// processed serially on this thread.  The thread terminates
        /// when <see cref="Dispose"/> is called.
        /// </summary>
        /// <param name="visible">Whether the Visio UI should be visible.</param>
        public VisioStaHost(bool visible = true)
        {
            _thread = new Thread(() =>
            {
                using var svc = new VisioService();
                svc.AttachOrCreateVisio(visible);
                foreach (var job in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        job.Run(svc);
                    }
                    catch (Exception ex)
                    {
                        job.SetError(ex);
                    }
                    finally
                    {
                        job.Complete();
                    }
                }
            });
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        /// <summary>
        /// Invoke an action on the STA thread.  The call will block
        /// until the action has completed.  Exceptions thrown by the
        /// action are rethrown on the calling thread.
        /// </summary>
        /// <param name="action">The work to perform with the service.</param>
        public void Invoke(Action<IVisioService> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var job = new ActionJob(action);
            _queue.Add(job);
            job.Wait();
        }

        /// <summary>
        /// Invoke a function on the STA thread and return its result.
        /// The call blocks until the function completes.  Exceptions
        /// thrown by the function are rethrown on the calling thread.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <returns>The result of the function.</returns>
        public T Invoke<T>(Func<IVisioService, T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var job = new FuncJob<T>(func);
            _queue.Add(job);
            job.Wait();
            return job.Result;
        }

        /// <summary>
        /// Dispose the host.  Completes the work queue and waits for
        /// the STA thread to finish.  Disposes the underlying service.
        /// </summary>
        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
        }

        // Base class for queued jobs
        private abstract class JobBase
        {
            private readonly ManualResetEventSlim _done = new(false);
            private Exception? _err;
            public void Wait()
            {
                _done.Wait();
                if (_err != null)
                    throw _err;
            }
            public void SetError(Exception ex) => _err = ex;
            public void Complete() => _done.Set();
            public abstract void Run(IVisioService svc);
        }

        private sealed class ActionJob : JobBase
        {
            private readonly Action<IVisioService> _action;
            public ActionJob(Action<IVisioService> action) => _action = action;
            public override void Run(IVisioService svc) => _action(svc);
        }

        private sealed class FuncJob<T> : JobBase
        {
            private readonly Func<IVisioService, T> _func;
            public T? Result { get; private set; }
            public FuncJob(Func<IVisioService, T> func) => _func = func;
            public override void Run(IVisioService svc) => Result = _func(svc);
        }
    }
}