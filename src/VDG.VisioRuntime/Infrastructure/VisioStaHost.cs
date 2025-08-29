using System;
using System.Collections.Concurrent;
using System.Threading;
using VDG.VisioRuntime.Services;

namespace VDG.VisioRuntime.Infrastructure
{
    /// <summary>
    /// Hosts a single‑threaded apartment (STA) for all Visio COM calls.
    /// </summary>
    public sealed class VisioStaHost : IDisposable
    {
        private readonly BlockingCollection<JobBase> _queue = new();
        private readonly Thread _thread;

        public VisioStaHost(bool visible = true)
        {
            _thread = new Thread(() =>
            {
                using var svc = new VisioService();
                svc.AttachOrCreateVisio(visible);
                foreach (var job in _queue.GetConsumingEnumerable())
                {
                    try { job.Run(svc); }
                    catch (Exception ex) { job.SetError(ex); }
                    finally { job.Complete(); }
                }
            });
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Invoke(Action<IVisioService> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var job = new ActionJob(action);
            _queue.Add(job);
            job.Wait();
        }

        public T Invoke<T>(Func<IVisioService, T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var job = new FuncJob<T>(func);
            _queue.Add(job);
            job.Wait();
            return job.Result!; // set by the STA thread before completion
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
        }

        private abstract class JobBase
        {
            private readonly ManualResetEventSlim _done = new(false);
            private Exception? _err;
            public void Wait()
            {
                _done.Wait();
                if (_err != null) throw _err;
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
