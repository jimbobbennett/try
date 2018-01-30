﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Clockwise;
using MLS.Agent.Tools;
using OmniSharp.Client;
using OmniSharp.Client.Events;
using Pocket;
using Recipes;
using static Pocket.Logger<WorkspaceServer.Servers.OmniSharp.OmniSharpServer>;

namespace WorkspaceServer.Servers.OmniSharp
{
    public class OmniSharpServer : IDisposable
    {
        private static readonly Lazy<FileInfo> _omniSharpPath = new Lazy<FileInfo>(MLS.Agent.Tools.OmniSharp.GetPath);

        private bool _ready;
        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private int _seq;
        private readonly Lazy<Process> _process;

        public OmniSharpServer(
            DirectoryInfo projectDirectory,
            string pluginPath = null,
            bool logToPocketLogger = false)
        {
            var standardOutput = new ReplaySubject<string>();
            var standardError = new ReplaySubject<string>();

            StandardOutput = standardOutput;
            StandardError = standardError;

            if (logToPocketLogger)
            {
                disposables.Add(StandardOutput.Subscribe(e => Log.Info("{message}", args: e)));
                disposables.Add(StandardError.Subscribe(e => Log.Error("{message}", args: e)));
            }

            _process = new Lazy<Process>(() =>
            {
                var process =
                    CommandLine.StartProcess(
                        _omniSharpPath.Value.FullName,
                        string.IsNullOrWhiteSpace(pluginPath)
                            ? ""
                            : $"-pl {pluginPath}",
                        projectDirectory,
                        standardOutput.OnNext,
                        standardError.OnNext);

                disposables.Add(() =>
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                });
                disposables.Add(process);

                return process;
            });
        }

        public IObservable<string> StandardOutput { get; }

        public IObservable<string> StandardError { get; }

        public StreamWriter StandardInput => _process.Value.StandardInput;

        public void Dispose() => disposables.Dispose();

        public int NextSeq() => Interlocked.Increment(ref _seq);

        public async Task WorkspaceReady(CancellationToken? cancellationToken = null)
        {
            if (_ready)
            {
                return;
            }

            cancellationToken = cancellationToken ?? Clock.Current.CreateCancellationToken(TimeSpan.FromSeconds(30));

            var _ = _process.Value;

            using (var operation = Log.OnEnterAndConfirmOnExit())
            {
                await StandardOutput
                      .AsOmniSharpMessages()
                      .OfType<OmniSharpEventMessage<ProjectAdded>>()
                      .FirstAsync()
                      .ToTask(cancellationToken);

                _ready = true;

                operation.Succeed();
            }
        }
    }
}
